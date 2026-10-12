using System.Collections.Generic;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.UI;
using Chopper.World.Generation;
using FishNet;
using UnityEngine;

namespace Chopper.World
{
    // The practice yard (TutorialSession). Added to the world by WorldBuilder once the land is built, on every machine.
    // Each machine puts up its own signs, one per station (TutorialStations), and keeps their text in the player's
    // current keys. The server repairs the hut, holds the clock at day, hands every player everything, and keeps a
    // training tree at the fighting station that comes back a few seconds after it is felled.
    public class TutorialYard : MonoBehaviour
    {
        private const float DayFraction = 0.3f;
        private const float DummyRespawnSeconds = 4f;
        private const float DummyLeash = 9f;
        private const float TextRefreshSeconds = 1f;
        private const int GiveTorches = 5;
        private const int GiveBandages = 5;
        private const int GiveHerbs = 6;
        private const int GiveFiber = 6;
        private const int GiveIron = 4;
        private const int GiveShards = 4;
        private const int GiveWood = 20;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static TutorialYard Instance { get; private set; }

        private readonly List<(TutorialStations.Station station, Sign sign)> _signs = new List<(TutorialStations.Station, Sign)>();
        private readonly HashSet<PlayerHealth> _equipped = new HashSet<PlayerHealth>();
        private bool _serverReady;
        private Treant _dummy;
        private float _dummyDueAt;
        private float _nextText;

        public int SignCount => _signs.Count;
        // Server: the training tree, while it stands.
        public Treant Dummy => _dummy != null && _dummy.IsSpawned ? _dummy : null;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            WorldGen gen = WorldSession.Gen;
            if (gen == null) return;
            Material material = SignMaterial();
            for (int i = 0; i < TutorialStations.Count; i++)
            {
                var station = (TutorialStations.Station)i;
                Vector2 at = TutorialStations.Position(station, gen.PondCenter, WorldGen.PondOuterRadius);
                _signs.Add((station, BuildSign(station, Ground(at), material)));
            }
            RefreshText();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextText)
            {
                _nextText = Time.unscaledTime + TextRefreshSeconds;
                RefreshText();
            }
            if (InstanceFinder.IsServerStarted) ServerTick();
        }

        // ---------- Server ----------

        private void ServerTick()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null || Hut.Instance == null || EnemyDirector.Instance == null) return;
            if (!_serverReady)
            {
                _serverReady = true;
                Hut.Instance.ServerSetRepaired(true);
                cycle.ServerDebugSetClock(DayPhase.Day, DayFraction);
                cycle.ServerFrozen = true;
                _dummyDueAt = Time.time + 1f;
            }

            foreach (PlayerHealth player in PlayerHealth.All)
            {
                if (player == null || !_equipped.Add(player)) continue;
                Equip(player);
            }

            if (Dummy == null)
            {
                if (_dummy != null)
                {
                    _dummy = null;
                    _dummyDueAt = Time.time + DummyRespawnSeconds;
                }
                if (Time.time >= _dummyDueAt) SpawnDummy();
            }
        }

        // Everything a player can use, at once.
        private static void Equip(PlayerHealth player)
        {
            PlayerAxe axe = player.GetComponent<PlayerAxe>();
            if (axe != null) axe.ServerSetHasAxe(true);
            // The practice yard skips the arrival drop (W9-01): dressed from the start.
            PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
            if (upgrades != null) upgrades.ServerSetDressed(true);
            PlayerFishing fishing = player.GetComponent<PlayerFishing>();
            if (fishing != null && fishing.Rod <= 0) fishing.ServerSetGear(1, 1);
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.ServerGrant(GiveTorches, GiveBandages);
                inventory.ServerAddHerbs(GiveHerbs);
                inventory.ServerAddFabric(GiveFiber);
                inventory.ServerAddRelics(GiveIron, GiveShards);
            }
            PlayerWood wood = player.GetComponent<PlayerWood>();
            if (wood != null) wood.ServerAddWood(GiveWood);
        }

        private void SpawnDummy()
        {
            WorldGen gen = WorldSession.Gen;
            _dummyDueAt = Time.time + DummyRespawnSeconds;
            if (gen == null) return;
            Vector3 at = Ground(TutorialStations.DummyPosition(gen.PondCenter, WorldGen.PondOuterRadius));
            Treant dummy = EnemyDirector.Instance.ServerSpawn(Element.Neutral, TreeSize.Small, false, at);
            dummy.ServerLeash(at, DummyLeash);
            if (dummy.ServerActivate()) _dummy = dummy;
        }

        // ---------- Signs ----------

        private void RefreshText()
        {
            foreach ((TutorialStations.Station station, Sign sign) in _signs)
            {
                if (sign != null) sign.SetText(TutorialStations.Title(station), TutorialStations.Body(station, KeyName));
            }
        }

        private static string KeyName(string action)
        {
            return System.Enum.TryParse(action, out KeyAction key) ? InputHints.Name(key) : action;
        }

        private Sign BuildSign(TutorialStations.Station station, Vector3 at, Material material)
        {
            var root = new GameObject("TutorialSign_" + station);
            root.transform.SetParent(transform, false);
            root.transform.position = at;
            // Face the hut, so the board reads from the yard.
            Vector3 toHut = -at;
            toHut.y = 0f;
            if (toHut.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(toHut);
            // The concept 2.0 voxel sign (ChopperPropDress), board toward +Z. The old primitive sign if it is missing.
            GameObject voxel = Resources.Load<GameObject>("Props/prop_sign_voxel");
            if (voxel != null)
            {
                Instantiate(voxel, root.transform, false);
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.8f, 0f);
                box.size = new Vector3(0.3f, 1.6f, 0.3f);
            }
            else
            {
                Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 1.4f, 0.12f), material, new Color(0.36f, 0.24f, 0.14f));
                Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.35f, 0.05f), new Vector3(0.95f, 0.55f, 0.07f), material, new Color(0.52f, 0.38f, 0.22f));
            }
            Sign sign = root.AddComponent<Sign>();
            sign.Configure(TutorialStations.Title(station), string.Empty, false);
            return sign;
        }

        private static void Piece(Transform parent, PrimitiveType type, Vector3 local, Vector3 scale, Material material, Color tint)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, tint);
            renderer.SetPropertyBlock(block);
        }

        // The character's gear material: it is in every build and takes a tint (as the fishing rod does).
        private static Material SignMaterial()
        {
            CharacterCatalog catalog = Resources.Load<CharacterCatalog>("CharacterCatalog");
            return catalog != null ? catalog.GearMaterial : null;
        }

        private static Vector3 Ground(Vector2 at)
        {
            var p = new Vector3(at.x, 0f, at.y);
            bool hasGround = Chopper.World.WorldGround.Exists;
            if (hasGround) p.y = Chopper.World.WorldGround.HeightAt(p);
            return p;
        }
    }
}
