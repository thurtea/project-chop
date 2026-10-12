using Chopper.Core;
using Chopper.Player;
using Chopper.World;
using Chopper.World.Building;
using UnityEngine;
using UnityEngine.AI;

namespace Chopper.Enemies
{
    // Server only brain (plan D19, T21). An awake treant goes for the nearest standing player inside aggro range in
    // its own forest or the hub, otherwise for the hut, and hits whatever it reaches. Clients never run this; they
    // only see the synced transform.
    // Against players it fights as a group (CombatCircle): far off it runs in; close by it circles the player on its
    // own spot of a ring, spread round the sides and back; only a couple at a time take a turn to close in and swing,
    // and after its swing (or when staggered) an enemy backs off and gives up its turn. Bosses just come straight on.
    [RequireComponent(typeof(Treant))]
    public class TreantAI : MonoBehaviour
    {
        // How close to the house's walls an enemy must be to hit the hut (bosses reach further).
        private const float HutWallReach = 1.8f;
        // How close an enemy must be to a building piece to hit it.
        private const float PieceReach = 1.6f;
        private const float RetargetInterval = 0.35f;
        private const float PlayerReachBase = 1.8f;
        private const float PlayerReachPerSize = 0.4f;
        private const float BossPlayerReach = 4.5f;
        private const float PlayerStopFraction = 0.8f;
        private const int BossDamageMultiplier = 2;

        // The waiting ring sits this far outside the enemy's reach; within RingEngage of it, an enemy starts to circle.
        private const float RingGap = 2.6f;
        private const float RingEngage = 5f;
        private const float CircleSpeedFraction = 0.65f;
        private const float BackOffSpeedFraction = 0.85f;
        private const float BackOffMinSeconds = 0.8f;
        private const float BackOffMaxSeconds = 1.5f;
        // A turn not used within this long (it could not reach the player) goes to another enemy.
        private const float TurnTimeout = 4f;
        // A short, different pause for each enemy before it reacts to a free turn, so they do not move in lockstep.
        private const float ReactionMin = 0.15f;
        private const float ReactionMax = 0.7f;
        // After backing off, an enemy first walks round to its ring spot (the player's side or back) before it may
        // take another turn: it must be this close to the spot, or have waited FlankPatience longer.
        private const float RegroupMin = 1.2f;
        private const float RegroupMax = 2.4f;
        private const float OnSpotDistance = 1.6f;
        private const float FlankPatience = 2.5f;
        private const float TurnDegreesPerSecond = 360f;

        [SerializeField] private GameConfig _config;

        private enum Mode { Approach, Circle, Attack, BackOff }

        private Treant _treant;
        private float _nextRetarget;
        private float _nextAttack;
        private float _nextSlam;
        private PlayerHealth _playerTarget;
        private bool _windingUp;
        private float _windupEnd;
        private Mode _mode = Mode.Approach;
        private float _modeUntil;
        private float _turnTakenAt;
        private float _readyAt;
        private bool _wasStaggered;
        private NavMeshPath _path;

        // The grove warden: roots waiting to burst, when the next ones are called, and how long its grove has been empty.
        private const float WardenRiseSeconds = 2.5f;
        private const float WardenResetSeconds = 8f;
        private struct PendingRoots
        {
            public Vector3 Position;
            public float At;
        }
        private readonly System.Collections.Generic.List<PendingRoots> _roots = new System.Collections.Generic.List<PendingRoots>();
        private float _nextRoots;
        private float _alone;

        private void Awake()
        {
            _treant = GetComponent<Treant>();
            _path = new NavMeshPath();
        }

        private void OnDisable()
        {
            CombatCircle.Leave(this);
        }

        private void Update()
        {
            if (!_treant.IsServerInitialized || !_treant.IsActive || !_treant.Agent.enabled || !_treant.Agent.isOnNavMesh) return;

            // A staggered enemy is stunned: it stops, loses its wind up, and cannot attack. Afterwards it backs off.
            bool staggered = _treant.IsStaggered;
            if (staggered) _windingUp = false;
            _treant.Agent.isStopped = staggered || _windingUp;
            if (staggered)
            {
                _wasStaggered = true;
                return;
            }
            if (_wasStaggered)
            {
                _wasStaggered = false;
                if (_playerTarget != null && !_treant.IsBoss) BeginBackOff();
            }

            // The warden heaves itself out of the ground before it fights.
            if (_treant.IsWarden && Time.time < _treant.ActivatedAt + WardenRiseSeconds)
            {
                _treant.Agent.isStopped = true;
                return;
            }

            if (_treant.IsBoss) TrySlam();
            if (_treant.IsWarden)
            {
                TryRoots();
                TryResetWarden();
            }

            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + RetargetInterval;
                Retarget();
            }

            FaceTargetWhileManoeuvring();
            TryAttack();
        }

        // For tests and tours: what the enemy is doing (Approach, Circle, Attack or BackOff).
        public string ModeName => _mode.ToString();

        private float HitMultiplier => (int)_treant.Size + 1;

        private float PlayerReach => _treant.IsBoss ? BossPlayerReach : PlayerReachBase + PlayerReachPerSize * (int)_treant.Size;

        private float RingRadius => PlayerReach + RingGap;

        private void Retarget()
        {
            NavMeshAgent agent = _treant.Agent;
            PlayerHealth previous = _playerTarget;
            _playerTarget = FindNearestPlayer();
            if (_playerTarget != previous)
            {
                CombatCircle.Leave(this);
                _mode = Mode.Approach;
            }

            if (_playerTarget != null)
            {
                if (_treant.IsBoss)
                {
                    ChasePlayer(agent);
                    return;
                }
                // A player it cannot walk up to (behind built walls): charge, and break what is in the way.
                if (!CanReach(_playerTarget.transform.position))
                {
                    CombatCircle.Leave(this);
                    _mode = Mode.Approach;
                    ChasePlayer(agent);
                    return;
                }
                CombatCircle.Engage(_playerTarget, this);
                UpdateGroupFight(agent);
                return;
            }
            agent.updateRotation = true;

            // A crypt guardian with nobody near walks back to where it stood watch, and the warden and its guards to
            // their grove: they never go for the hut.
            if (_treant.IsGuardian || _treant.HasLeash)
            {
                agent.stoppingDistance = 0.5f;
                agent.SetDestination(_treant.Home);
                _treant.Agent.speed = _treant.BaseSpeed;
                return;
            }

            Hut hut = Hut.Instance;
            if (hut == null) return;
            BuildManager pieces = BuildManager.Instance;
            if (pieces != null && pieces.ServerRaidTarget(transform.position, out Vector3 raid))
            {
                agent.stoppingDistance = 1.2f;
                agent.SetDestination(raid);
                SetChargeSpeed(raid);
                return;
            }
            // To the nearest wall of the house: the room itself is off the NavMesh.
            Vector3 wall = hut.ClosestWallPoint(transform.position);
            agent.stoppingDistance = Mathf.Max(0.5f, HutWallReach - 0.6f);
            agent.SetDestination(wall);
            SetChargeSpeed(wall);
        }

        private void ChasePlayer(NavMeshAgent agent)
        {
            agent.updateRotation = true;
            agent.stoppingDistance = PlayerReach * PlayerStopFraction;
            agent.SetDestination(_playerTarget.transform.position);
            SetChargeSpeed(_playerTarget.transform.position);
        }

        // Approach, circle, take a turn, back off.
        private void UpdateGroupFight(NavMeshAgent agent)
        {
            Vector3 toPlayer = _playerTarget.transform.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            switch (_mode)
            {
                case Mode.BackOff:
                    if (Time.time < _modeUntil)
                    {
                        Vector3 away = distance > 0.01f ? -toPlayer / distance : -transform.forward;
                        MoveTo(agent, transform.position + away * RingGap, _treant.BaseSpeed * BackOffSpeedFraction, 0.2f, false);
                        return;
                    }
                    _mode = Mode.Circle;
                    _readyAt = Time.time + Random.Range(RegroupMin, RegroupMax);
                    break;
                case Mode.Attack:
                    if (!CombatCircle.HasTurn(_playerTarget, this) || Time.time - _turnTakenAt > TurnTimeout)
                    {
                        CombatCircle.EndTurn(this);
                        _mode = Mode.Circle;
                        _readyAt = Time.time + Random.Range(ReactionMin, ReactionMax);
                        break;
                    }
                    ChasePlayer(agent);
                    return;
            }

            // Far away: run in on the path. Close: circle on its own ring spot until a turn is free.
            if (distance > RingRadius + RingEngage)
            {
                _mode = Mode.Approach;
                ChasePlayer(agent);
                return;
            }
            _mode = Mode.Circle;
            Vector3 spot = CombatCircle.RingSpot(_playerTarget, this, RingRadius);
            Vector3 toSpot = spot - transform.position;
            toSpot.y = 0f;
            bool placed = toSpot.magnitude <= OnSpotDistance || Time.time >= _readyAt + FlankPatience;
            if (Time.time >= _readyAt && placed && CombatCircle.TryTakeTurn(_playerTarget, this))
            {
                _mode = Mode.Attack;
                _turnTakenAt = Time.time;
                ChasePlayer(agent);
                return;
            }
            // Walking round to a spot far off goes at full pace; small corrections on the ring are a slow drift.
            MoveTo(agent, spot, _treant.BaseSpeed * (toSpot.magnitude > 3f ? 1f : CircleSpeedFraction), 0.4f, false);
        }

        private bool CanReach(Vector3 target)
        {
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, _path) && _path.status == NavMeshPathStatus.PathComplete;
        }

        // Sends the agent to a reachable point near the spot. With faceMove off, the enemy keeps its eyes on its
        // target (FaceTargetWhileManoeuvring) instead of turning its back to walk.
        private static void MoveTo(NavMeshAgent agent, Vector3 spot, float speed, float stop, bool faceMove)
        {
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 3f, NavMesh.AllAreas)) spot = hit.position;
            agent.updateRotation = faceMove;
            agent.speed = speed;
            agent.stoppingDistance = stop;
            agent.SetDestination(spot);
        }

        private void FaceTargetWhileManoeuvring()
        {
            if (_playerTarget == null || _treant.Agent.updateRotation) return;
            Vector3 look = _playerTarget.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), TurnDegreesPerSecond * Time.deltaTime);
        }

        private void BeginBackOff()
        {
            CombatCircle.EndTurn(this);
            _mode = Mode.BackOff;
            _modeUntil = Time.time + Random.Range(BackOffMinSeconds, BackOffMaxSeconds);
            _nextRetarget = 0f;
        }

        // Far from the target the enemy runs, so a night that starts deep in a forest still reaches the fight in time.
        private void SetChargeSpeed(Vector3 targetPosition)
        {
            Vector3 offset = targetPosition - transform.position;
            offset.y = 0f;
            bool far = offset.magnitude > _config.TreantChargeDistance;
            _treant.Agent.speed = _treant.BaseSpeed * (far ? _config.TreantChargeMultiplier : 1f) * ZoneBossRules.SpeedMultiplier(_treant.WardenPhase);
        }

        // The enraged warden slams and swings more often.
        private float IntervalScale => _treant.IsWarden ? ZoneBossRules.IntervalMultiplier(_treant.WardenPhase) : 1f;

        private PlayerHealth FindNearestPlayer()
        {
            PlayerHealth best = null;
            float bestSqr = _config.TreantAggroRange * _config.TreantAggroRange;
            for (int i = 0; i < PlayerHealth.All.Count; i++)
            {
                PlayerHealth candidate = PlayerHealth.All[i];
                if (candidate.IsDown) continue;
                if (!InMyGround(candidate.transform.position)) continue;
                float sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                // A leashed enemy fights anyone inside its circle, however far from it they stand, and nobody outside.
                if (_treant.HasLeash)
                {
                    if (!InLeash(candidate.transform.position)) continue;
                    if (best == null || sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = candidate;
                    }
                    continue;
                }
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }
            return best;
        }

        // A forest's enemies keep to their own forest and the hub: a player who crosses into another forest is let go,
        // and the enemy heads for the hut through its own pass. Crypt guardians and arena enemies stay below ground.
        private bool InMyGround(Vector3 position)
        {
            if (_treant.IsGuardian || _treant.Arena >= 0 || Chopper.World.Generation.CryptWorld.IsUnderground(transform.position)) return true;
            Element zone = ZoneMap.ElementAt(position, _config.HubRadius);
            return zone == Element.Neutral || zone == _treant.Element;
        }

        private const float WindupSlackReach = 1.4f;
        private const float WindupPerSize = 0.08f;

        // Only bosses and enemies holding a turn may swing at a player; the others wait their turn on the ring.
        private bool MaySwingAtPlayer => _treant.IsBoss || _mode == Mode.Attack;

        private void TryAttack()
        {
            // A wind up is under way: the hit lands at its end, but only if the player is still in reach. Rolling
            // out of range (or through it), blocking and parrying all beat it.
            if (_windingUp)
            {
                if (Time.time < _windupEnd) return;
                _windingUp = false;
                _treant.Agent.isStopped = false;
                if (_playerTarget != null && !_playerTarget.IsDown)
                {
                    Vector3 away = _playerTarget.transform.position - transform.position;
                    away.y = 0f;
                    if (away.magnitude <= PlayerReach * WindupSlackReach)
                    {
                        int damage = _config.TreantPlayerDamage * (int)HitMultiplier * (_treant.IsBoss ? BossDamageMultiplier : 1);
                        _playerTarget.ServerDamage(damage, _treant);
                        Swing();
                        if (!_treant.IsBoss) BeginBackOff();
                        return;
                    }
                }
                _nextAttack = Time.time + _config.TreantAttackInterval * 0.5f;
                if (!_treant.IsBoss) BeginBackOff();
                return;
            }

            if (Time.time < _nextAttack) return;

            if (_playerTarget != null && !_playerTarget.IsDown && MaySwingAtPlayer)
            {
                Vector3 offset = _playerTarget.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude <= PlayerReach)
                {
                    _windingUp = true;
                    _windupEnd = Time.time + _config.TreantWindupSeconds + WindupPerSize * (int)_treant.Size;
                    _treant.Agent.isStopped = true;
                    if (offset.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(offset);
                    _treant.ServerPlayWindup();
                    return;
                }
            }
            // Circling or backing off from a player: buildings and the hut can wait.
            if (_playerTarget != null && _mode != Mode.Approach) return;

            // Next anything players built in the way (a wall, a door frame, a fire): the closest piece in reach.
            BuildManager pieces = BuildManager.Instance;
            if (pieces != null && pieces.ServerNearest(transform.position, PieceReach + (int)_treant.Size * 0.3f, out int pieceId))
            {
                int pieceDamage = _config.TreantHutDamage * (int)HitMultiplier * (_treant.IsBoss ? BossDamageMultiplier : 1);
                pieces.ServerDamage(pieceId, pieceDamage);
                Swing();
                return;
            }

            Hut hut = Hut.Instance;
            if (hut == null || hut.Health <= 0) return;
            if (hut.DistanceToWalls(transform.position) <= HutWallReach * (_treant.IsBoss ? 1.6f : 1f))
            {
                int damage = _config.TreantHutDamage * (int)HitMultiplier * (_treant.IsBoss ? BossDamageMultiplier : 1);
                hut.ServerDamage(damage);
                Swing();
            }
        }

        private bool InLeash(Vector3 position)
        {
            Vector3 offset = position - _treant.Home;
            offset.y = 0f;
            return offset.magnitude <= _treant.LeashRadius;
        }

        // Phase 2 and 3: roots burst out of the ground under every player in the grove, after a short warning. Moving
        // off the spot (or rolling) avoids them.
        private void TryRoots()
        {
            for (int i = _roots.Count - 1; i >= 0; i--)
            {
                if (Time.time < _roots[i].At) continue;
                Vector3 spot = _roots[i].Position;
                _roots.RemoveAt(i);
                _treant.ServerBurstRoots(spot);
                float radius = _config.WardenRootRadius;
                for (int p = 0; p < PlayerHealth.All.Count; p++)
                {
                    PlayerHealth player = PlayerHealth.All[p];
                    Vector3 offset = player.transform.position - spot;
                    offset.y = 0f;
                    if (!player.IsDown && offset.magnitude <= radius) player.ServerDamage(_config.WardenRootDamage, _treant);
                }
            }

            if (!ZoneBossRules.RootsActive(_treant.WardenPhase) || Time.time < _nextRoots) return;
            _nextRoots = Time.time + _config.WardenRootInterval * IntervalScale;
            for (int p = 0; p < PlayerHealth.All.Count; p++)
            {
                PlayerHealth player = PlayerHealth.All[p];
                if (player.IsDown || !InLeash(player.transform.position)) continue;
                Vector3 spot = player.transform.position;
                _roots.Add(new PendingRoots { Position = spot, At = Time.time + _config.WardenRootWarning });
                _treant.ServerWarnRoots(spot, _config.WardenRootRadius);
            }
        }

        // Nobody left in the grove for a while: the warden heals, sheds its guards and starts over.
        private void TryResetWarden()
        {
            if (_playerTarget != null)
            {
                _alone = 0f;
                return;
            }
            _alone += Time.deltaTime;
            if (_alone < WardenResetSeconds) return;
            _alone = 0f;
            _roots.Clear();
            _nextRoots = 0f;
            if (EnemyDirector.Instance != null) EnemyDirector.Instance.ServerResetWarden(_treant);
        }

        private void Swing()
        {
            _nextAttack = Time.time + _config.TreantAttackInterval * IntervalScale;
            _treant.ServerPlayAttack();
        }

        private void TrySlam()
        {
            if (Time.time < _nextSlam) return;

            bool playerNear = false;
            for (int i = 0; i < PlayerHealth.All.Count; i++)
            {
                Vector3 offset = PlayerHealth.All[i].transform.position - transform.position;
                offset.y = 0f;
                if (!PlayerHealth.All[i].IsDown && offset.magnitude <= _config.BossSlamRadius) playerNear = true;
            }

            Hut hut = Hut.Instance;
            bool hutNear = false;
            if (hut != null)
            {
                hutNear = hut.DistanceToWalls(transform.position) <= _config.BossSlamRadius + HutWallReach;
            }
            if (!playerNear && !hutNear) return;

            _nextSlam = Time.time + _config.BossSlamInterval * IntervalScale;
            for (int i = 0; i < PlayerHealth.All.Count; i++)
            {
                PlayerHealth player = PlayerHealth.All[i];
                Vector3 offset = player.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude <= _config.BossSlamRadius) player.ServerDamage(_config.BossSlamPlayerDamage);
            }
            if (hutNear && hut != null) hut.ServerDamage(_config.BossSlamHutDamage);
            _treant.ServerPlaySlam();
        }
    }
}
