using System.Collections.Generic;
using Chopper.Audio;
using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using Chopper.World.Generation;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.Player
{
    // Mining, a pastime like fishing: the finds carried and the biggest of each kind (both kept in the save), the
    // portals and ladders of the mines, and working a vein. The owner engages a vein and strikes to RhythmRules'
    // beat, the vein flashing the beats, and the server adds it up: a few clean strikes free the find, a full bar
    // counts double, a rough one can crack it. Each player may
    // work every vein once a day (MineManager). Fishing feeds in too: a glowworm carried is bait, and a landed fish
    // now and then brings up a geode or an old pick head.
    public class PlayerMining : NetworkBehaviour
    {
        // The pickaxe stays out this long after the last strike, then goes away and the axe comes back.
        private const float PickOutSeconds = 4f;

        [SerializeField] private Transform _pickPivot;

        private readonly SyncList<int> _finds = new SyncList<int>();
        private readonly SyncList<float> _records = new SyncList<float>();
        private readonly SyncVar<int> _skill = new SyncVar<int>();
        private readonly SyncVar<bool> _pickDrawn = new SyncVar<bool>();
        private float _pickAskedAt = -10f;

        private PlayerAxe _axe;
        private PlayerHealth _health;
        private PlayerCombat _combat;
        private readonly System.Random _rng = new System.Random();

        // Owner and observers: when the pickaxe was last swung here.
        private float _lastStrikeAt = float.NegativeInfinity;
        private float _nextStrikeAt;

        // Server: the vein being worked and how far along it is.
        private int _serverVein = -1;
        private float _serverProgress;
        private int _serverStrikes;
        private int _serverClean;
        private float _serverNextStrike;
        private int _engagedVein = -1;
        private float _rhythmStart;
        private float _engagedActionAt;
        private int _rhythmBar = -1;
        private int _rhythmMask;

        public static PlayerMining Local { get; private set; }

        // Raised on every client when this player swings the pickaxe (the animator plays the chop).
        public event System.Action StrikePlayed;

        public bool PickOut => _pickDrawn.Value || Engaged || Time.time - _lastStrikeAt < PickOutSeconds;

        public bool PickDrawn => _pickDrawn.Value;

        public void RequestTogglePick()
        {
            if (!IsOwner || Time.time - _pickAskedAt < 0.25f) return;
            PlayerMotor motor = GetComponent<PlayerMotor>();
            if (!_pickDrawn.Value && !SwimHolster.MayDraw(motor != null && motor.IsSwimming)) return;
            _pickAskedAt = Time.time;
            TogglePickServerRpc();
        }

        [ServerRpc]
        private void TogglePickServerRpc() => _pickDrawn.Value = !_pickDrawn.Value;

        private void PickKeys()
        {
            if (!IsOwner) return;
            PlayerMotor motor = GetComponent<PlayerMotor>();
            if (_pickDrawn.Value && motor != null && motor.IsSwimming) { RequestTogglePick(); return; }
            if (UiState.GameplayBlocked) return;
            if (Keybinds.WasPressed(KeyAction.Pick)) RequestTogglePick();
            else if (_pickDrawn.Value && (PlayerAxe.WeaponKeyPressed() || Keybinds.WasPressed(KeyAction.Unequip) || Keybinds.WasPressed(KeyAction.Rod) || Keybinds.WasPressed(KeyAction.Build) || Keybinds.WasPressed(KeyAction.Torch))) RequestTogglePick();
        }

        public bool Engaged => _engagedVein >= 0;
        public int EngagedVein => _engagedVein;
        public float RhythmStart => _rhythmStart;

        public static event System.Action<int, RhythmHit, bool> RhythmCue;

        // One word that pops up and fades, like fishing's: Clean, Good, Rough, Cracked, or the find. Owner only.
        public string CueWord { get; private set; } = string.Empty;
        public bool CueGood { get; private set; }
        public float CueAt { get; private set; } = float.NegativeInfinity;

        private void Awake()
        {
            _axe = GetComponent<PlayerAxe>();
            _health = GetComponent<PlayerHealth>();
            _combat = GetComponent<PlayerCombat>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            while (_finds.Count < MiningRules.Kinds) _finds.Add(0);
            while (_records.Count < MiningRules.Kinds) _records.Add(0f);
            if (MineManager.Instance != null) MineManager.Instance.ServerSendWorked(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) Local = this;
            if (_pickPivot != null) _pickPivot.gameObject.SetActive(false);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
        }

        // ---------- Finds ----------

        public int FindCount(MineFindKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < _finds.Count ? _finds[i] : 0;
        }

        public float Record(MineFindKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < _records.Count ? _records[i] : 0f;
        }

        public bool HasPickHead => FindCount(MineFindKind.OldPickHead) > 0;

        public int MineSkill => _skill.Value;

        // Server only. Loading a saved player's mining skill (0 in older saves and for new players).
        public void ServerLoadSkill(int skill) => _skill.Value = SkillRules.Clamp(skill);

        public int[] CopyFinds()
        {
            var counts = new int[MiningRules.Kinds];
            for (int i = 0; i < counts.Length && i < _finds.Count; i++) counts[i] = _finds[i];
            return counts;
        }

        public float[] CopyRecords()
        {
            var records = new float[MiningRules.Kinds];
            for (int i = 0; i < records.Length && i < _records.Count; i++) records[i] = _records[i];
            return records;
        }

        // Server only. Adds delta (negative removes) and returns how much changed.
        public int ServerChangeFind(MineFindKind kind, int delta)
        {
            int i = (int)kind;
            if (i < 0 || i >= _finds.Count) return 0;
            int before = _finds[i];
            int after = Mathf.Clamp(before + delta, 0, PlayerItems.MiningFindCap);
            if (after != before) _finds[i] = after;
            return after - before;
        }

        // Server only. A saved player's finds and records (missing in older saves).
        public void ServerLoad(int[] counts, float[] records)
        {
            while (_finds.Count < MiningRules.Kinds) _finds.Add(0);
            while (_records.Count < MiningRules.Kinds) _records.Add(0f);
            for (int i = 0; i < MiningRules.Kinds; i++)
            {
                int count = counts != null && i < counts.Length ? Mathf.Clamp(counts[i], 0, PlayerItems.MiningFindCap) : 0;
                float best = records != null && i < records.Length ? Mathf.Max(0f, records[i]) : 0f;
                if (_finds[i] != count) _finds[i] = count;
                if (_records[i] != best) _records[i] = best;
            }
        }

        // Server only. The game over restart: the finds carried are gone, the records are the player's own and stay.
        public void ServerReset()
        {
            for (int i = 0; i < _finds.Count; i++) if (_finds[i] != 0) _finds[i] = 0;
            _skill.Value = 0;
        }

        // ---------- Portals and ladders ----------

        // Owner only. Handles the interact press at a mine portal or the ladder out.
        public bool TryInteract()
        {
            if (!IsOwner) return false;
            if (Engaged)
            {
                Disengage();
                return true;
            }
            Vector3 position = transform.position;
            int near = MineWorld.VeinInReach(position, transform.forward);
            if (near >= 0 && !MineWorld.SpentHere(near))
            {
                Engage(near);
                return true;
            }
            if (MineWorld.DoorInReach(position) >= 0)
            {
                EnterServerRpc();
                return true;
            }
            if (MineWorld.ExitInReach(position) >= 0)
            {
                ExitServerRpc();
                return true;
            }
            return false;
        }

        [ServerRpc]
        private void EnterServerRpc()
        {
            if (MineManager.Instance != null) MineManager.Instance.ServerTryEnter(GetComponent<PlayerCrypt>());
        }

        [ServerRpc]
        private void ExitServerRpc()
        {
            if (MineManager.Instance != null) MineManager.Instance.ServerTryExit(GetComponent<PlayerCrypt>());
        }

        // ---------- Striking a vein ----------

        // Owner only. The attack press, when a vein is in front: swing at its glint. True if the press was used here
        // (so the axe does not also swing).
        public bool TryStrike(Vector3 aim)
        {
            if (!IsOwner) return false;
            if (_health != null && _health.IsDown) return false;
            if (_combat != null && (_combat.IsBlocking || _combat.IsRolling)) return false;
            if (!Engaged)
            {
                int near = MineWorld.VeinInReach(transform.position, aim);
                if (near < 0) return TryStrikeRock(aim);
                if (!MineWorld.SpentHere(near)) Engage(near);
                return true;
            }
            int vein = _engagedVein;
            if (Time.time < _nextStrikeAt) return true;
            _nextStrikeAt = Time.time + RhythmRules.Beat * 0.6f;
            _engagedActionAt = Time.time;
            float since = Time.time - _rhythmStart;
            RhythmHit hit = RhythmRules.Judge(since);
            if (hit == RhythmHit.OffBeat && HasPickHead && Mathf.Abs(since - RhythmRules.BeatIndex(since) * RhythmRules.Beat) <= RhythmRules.Window * 2f)
                hit = RhythmHit.OnBeat;
            bool bar = RhythmRules.Register(ref _rhythmBar, ref _rhythmMask, since, hit);
            StrikeQuality quality = MiningRules.FromRhythm(hit);
            RhythmCue?.Invoke(vein, hit, bar);
            _lastStrikeAt = Time.time;
            StrikePlayed?.Invoke();
            StartCoroutine(StrikeOnContact(vein, quality, bar));
            return true;
        }

        public const float PickSwingSeconds = 0.5f;

        public int LastRockStone { get; private set; } = -1;
        public int LastRock { get; private set; } = -1;

        private bool TryStrikeRock(Vector3 aim)
        {
            if (!_pickDrawn.Value) return false;
            int rock = Chopper.World.RockField.InReach(transform.position, aim);
            if (rock < 0) return false;
            if (Time.time < _nextStrikeAt) return true;
            _nextStrikeAt = Time.time + PickSwingSeconds;
            _lastStrikeAt = Time.time;
            StrikePlayed?.Invoke();
            StartCoroutine(RockOnContact(rock, aim));
            return true;
        }

        private System.Collections.IEnumerator RockOnContact(int rock, Vector3 aim)
        {
            ToolPose pose = GetComponent<ToolPose>();
            if (pose == null) pose = gameObject.AddComponent<ToolPose>();
            pose.Play(SwingTool.Pick, 0, PickSwingSeconds);
            yield return new WaitForSeconds(PickSwingSeconds * ToolMotion.Contact(SwingTool.Pick));
            Vector3 at = transform.position + transform.forward * 0.9f + Vector3.up * 0.8f;
            if (PlayerCamera.Local != null) PlayerCamera.Local.AddShake(0.05f);
            RockServerRpc(rock, aim);
        }

        [ServerRpc]
        private void RockServerRpc(int rock, Vector3 aim)
        {
            ServerStrikeRock(rock, aim);
        }

        public int ServerStrikeRock(int rock, Vector3 aim)
        {
            if (rock < 0 || rock >= Chopper.World.RockField.Count) return 0;
            if (!StoneRules.InReach(transform.position, aim, Chopper.World.RockField.PositionOf(rock), Chopper.World.RockField.SizeOf(rock) + 1f)) return 0;
            LastRock = rock;
            int amount = Chopper.World.RockField.ServerTake(rock);
            int gained = amount > 0 ? PlayerItems.ServerChange(gameObject, ItemRules.Stone, amount) : 0;
            LastRockStone = gained;
            RockFxObserversRpc(rock, gained > 0 ? 0 : amount <= 0 ? 1 : 2);
            return gained;
        }

        [ObserversRpc]
        private void RockFxObserversRpc(int rock, int outcome)
        {
            if (rock < 0 || rock >= Chopper.World.RockField.Count) return;
            Vector3 face = Chopper.World.RockField.PositionOf(rock);
            Vector3 toPlayer = transform.position - face;
            toPlayer.y = 0f;
            Vector3 at = face + (toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.forward) * (Chopper.World.RockField.SizeOf(rock) * 0.45f) + Vector3.up * 0.8f;
            if (outcome == 0)
            {
                CombatFx.Chips(at, new Color(0.62f, 0.60f, 0.56f));
                CombatFx.Dust(at, new Color(0.70f, 0.68f, 0.64f), 4);
                Sfx.Play(ProceduralSfx.Chop, at, 0.8f, 0.72f, 0.08f);
            }
            else if (outcome == 1)
            {
                CombatFx.Dust(at, new Color(0.58f, 0.56f, 0.52f), 10);
                Sfx.Play(ProceduralSfx.Block, at, 0.7f, 0.5f, 0.05f);
            }
            else
            {
                CombatFx.Chips(at, new Color(0.62f, 0.60f, 0.56f));
                Sfx.Play(ProceduralSfx.UiClick, at, 0.5f, 0.55f, 0.05f);
            }
        }

        private System.Collections.IEnumerator StrikeOnContact(int vein, StrikeQuality quality, bool bar)
        {
            ToolPose pose = GetComponent<ToolPose>();
            if (pose == null) pose = gameObject.AddComponent<ToolPose>();
            pose.Play(SwingTool.Pick, 0, PickSwingSeconds);
            yield return new WaitForSeconds(PickSwingSeconds * ToolMotion.Contact(SwingTool.Pick));
            Vector3 at = transform.position + transform.forward * 0.9f + Vector3.up * 1.2f;
            if (quality == StrikeQuality.Clean) Sfx.Play(ProceduralSfx.Parry, at, 0.75f, 1.15f, 0.06f);
            else Sfx.Play(ProceduralSfx.Chop, at, 0.7f, quality == StrikeQuality.Good ? 0.9f : 0.75f, 0.08f);
            if (PlayerCamera.Local != null) PlayerCamera.Local.AddShake(0.06f);
            StrikeServerRpc(vein, (int)quality, bar);
        }

        private void Engage(int vein)
        {
            _engagedVein = vein;
            _rhythmStart = Time.time + RhythmRules.LeadIn;
            _engagedActionAt = Time.time;
            _rhythmBar = -1;
            _rhythmMask = 0;
            _lastStrikeAt = Time.time;
        }

        private void Disengage()
        {
            _engagedVein = -1;
        }

        private void Update()
        {
            PickKeys();
            if (!IsOwner || !Engaged) return;
            bool other = (_combat != null && (_combat.GuardRaised || _combat.IsRolling)) || Keybinds.WasPressed(KeyAction.Jump)
                         || (_health != null && _health.IsDown);
            bool away = !MineWorld.VeinNear(_engagedVein, transform.position, 2.5f);
            bool idle = Time.time - _engagedActionAt > RhythmRules.LeadIn + 6f;
            if (other || away || idle || MineWorld.SpentHere(_engagedVein)) Disengage();
        }

        private void PlayStrike(StrikeQuality quality)
        {
            _lastStrikeAt = Time.time;
            StrikePlayed?.Invoke();
            Vector3 at = transform.position + transform.forward * 0.9f + Vector3.up * 1.2f;
            if (quality == StrikeQuality.Clean) Sfx.Play(ProceduralSfx.Parry, at, 0.75f, 1.15f, 0.06f);
            else Sfx.Play(ProceduralSfx.Chop, at, 0.7f, quality == StrikeQuality.Good ? 0.9f : 0.75f, 0.08f);
        }

        [ServerRpc]
        private void StrikeServerRpc(int vein, int quality, bool bar)
        {
            ServerStrike(vein, (StrikeQuality)Mathf.Clamp(quality, 0, 2), false, bar);
        }

        // Server only. One strike at a vein, as judged by the owner. Returns the find freed (-1 for none yet, -2 when
        // the find cracked). ignoreChecks is for tests.
        public int ServerStrike(int vein, StrikeQuality quality, bool ignoreChecks, bool bar = false)
        {
            if (vein < 0 || vein >= MineWorld.Veins.Count) return -1;
            if (!ignoreChecks)
            {
                if (!MineWorld.VeinNear(vein, transform.position, 1.2f)) return -1;
                if (Time.time < _serverNextStrike) return -1;
            }
            MineManager manager = MineManager.Instance;
            if (manager == null || manager.HasWorked(vein, this)) return -1;
            _serverNextStrike = Time.time + RhythmRules.Beat * 0.5f;
            StrikeObserversRpc();

            if (vein != _serverVein)
            {
                _serverVein = vein;
                _serverProgress = 0f;
                _serverStrikes = 0;
                _serverClean = 0;
            }
            _serverStrikes++;
            if (quality == StrikeQuality.Clean) _serverClean++;
            if (MiningRules.Cracks(quality, _rng.NextDouble()))
            {
                manager.ServerMarkWorked(vein, this);
                _serverVein = -1;
                if (Owner.IsValid) ResultTargetRpc(Owner, vein, -1, 0f, false);
                return -2;
            }
            _serverProgress += MiningRules.RhythmProgress(quality, bar) * SkillRules.MineProgressMultiplier(_skill.Value);
            if (!SkillRules.MineFrees(_serverProgress, _serverStrikes)) return -1;
            _skill.Value = SkillRules.Gain(_skill.Value, SkillRules.MineGainPerFind);

            float cleanShare = _serverClean / (float)Mathf.Max(1, _serverStrikes);
            MineZone zone = MineWorld.Plans[MineWorld.Veins[vein].Mine].Zone;
            MineFindKind kind = MiningRules.Roll(zone, _rng.NextDouble(), cleanShare);
            float grams = MiningRules.Grams(kind, _rng.NextDouble(), cleanShare);
            bool record = ServerAward(kind, grams);
            manager.ServerMarkWorked(vein, this);
            _serverVein = -1;
            if (Owner.IsValid) ResultTargetRpc(Owner, vein, (int)kind, grams, record);
            return (int)kind;
        }

        // Server only. Adds a find and keeps it if it is the biggest yet. True for a new record.
        private bool ServerAward(MineFindKind kind, float grams)
        {
            ServerChangeFind(kind, 1);
            if (ZoneGoalRules.ZoneOfGem(kind, out Element goalZone) && Chopper.Enemies.EnemyDirector.Instance != null)
                Chopper.Enemies.EnemyDirector.Instance.ServerMarkZoneGoal(goalZone, ZoneGoal.Gem);
            int i = (int)kind;
            if (i < 0 || i >= _records.Count || !MiningRules.IsRecord(grams, _records[i])) return false;
            _records[i] = grams;
            return true;
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void StrikeObserversRpc()
        {
            _lastStrikeAt = Time.time;
            StrikePlayed?.Invoke();
        }

        [TargetRpc]
        private void ResultTargetRpc(NetworkConnection connection, int vein, int kind, float grams, bool record)
        {
            MineWorld.MarkSpentHere(vein);
            if (kind < 0)
            {
                RhythmCue?.Invoke(vein, RhythmHit.Rest, false);
                Sfx.Play(ProceduralSfx.Chop, transform.position + Vector3.up, 0.8f, 0.55f, 0.05f);
                return;
            }
            ShowFind((MineFindKind)kind, grams, record);
        }

        private void ShowFind(MineFindKind kind, float grams, bool record)
        {
            MineFind find = MiningRules.Finds[(int)kind];
            PickupFeed.Add(PickupFeed.Kind.Find, find.Name, 1, FindCount(kind) + 1, find.Icon);
            Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 0.55f, 1.1f, 0.05f);
        }

        // ---------- Fishing ----------

        // Server only. Takes one glowworm for a cast's bait. True if there was one.
        public bool ServerTakeGlowworm() => ServerChangeFind(MineFindKind.Glowworm, -1) < 0;

        // Server only. A landed fish brought something else up: a geode now and then, rarely an old pick head.
        public void ServerAfterCatch()
        {
            double roll = _rng.NextDouble();
            MineFindKind kind;
            if (roll < MiningRules.PickHeadOnCatchChance) kind = MineFindKind.OldPickHead;
            else if (roll < MiningRules.PickHeadOnCatchChance + MiningRules.GeodeOnCatchChance) kind = MineFindKind.Geode;
            else return;
            float grams = MiningRules.Grams(kind, _rng.NextDouble(), 0.5f);
            bool record = ServerAward(kind, grams);
            if (Owner.IsValid) FishedUpTargetRpc(Owner, (int)kind, grams, record);
        }

        [TargetRpc]
        private void FishedUpTargetRpc(NetworkConnection connection, int kind, float grams, bool record) => ShowFind((MineFindKind)kind, grams, record);

        // ---------- Joining ----------

        // Server only. These veins were worked by this player earlier today.
        public void ServerMarkWorked(int[] veins)
        {
            if (Owner.IsValid) MarkWorkedTargetRpc(Owner, veins);
        }

        [TargetRpc]
        private void MarkWorkedTargetRpc(NetworkConnection connection, int[] veins)
        {
            foreach (int vein in veins) MineWorld.MarkSpentHere(vein);
        }

        // ---------- The pickaxe ----------

        private void LateUpdate()
        {
            bool pickOut = PickOut && !(_health != null && _health.IsDown);
            if (_pickPivot != null && _pickPivot.gameObject.activeSelf != pickOut) _pickPivot.gameObject.SetActive(pickOut);
            if (_axe != null) _axe.SetMiningStowed(pickOut);
        }

        // Server only, tests: works every vein of a mine for this player as if struck cleanly; returns the finds.
        public List<int> ServerDebugWorkMine(int mine)
        {
            var found = new List<int>();
            for (int vein = 0; vein < MineWorld.Veins.Count; vein++)
            {
                if (MineWorld.Veins[vein].Mine != mine) continue;
                int result = -1;
                for (int i = 0; i < 6 && result == -1; i++) result = ServerStrike(vein, StrikeQuality.Clean, true);
                found.Add(result);
            }
            return found;
        }
    }
}
