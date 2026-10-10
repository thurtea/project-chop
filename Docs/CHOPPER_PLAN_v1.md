# Chopper: MVP Foundation Plan

Status: planning, no code yet.
Style rule for this project: no emojis and no em dashes anywhere (docs, code comments, commit messages, in-game text).
Do not implement the entire networking architecture upfront**. First prove that Unity, FishNet, the assemblies, and a two-player connection work. The rest depends on that foundation.
---

## 0. Understanding of Constraints

- Engine: Unity 6 LTS, C# MonoBehaviours and GameObjects only. No ECS/DOTS.
- Networking: FishNet (v4.x). Server-authoritative. Host mode (one player is client + server) for 1-4 players.
- Art: none. Unity primitives only (cubes, cylinders, capsules, spheres) with flat colored materials. Element identity comes purely from color and emission.
- World: a Valheim-style open meadow as the central hub (hut + pond, no trees near the hut), ringed by four elemental forests densely filled with small, medium, and large trees.
  - North = Water, East = Earth, South = Fire, West = Wind.
- Loop: Day, players chop trees. Felled trees drop seedlings on the ground. Night, every seedling grows into a walking tree enemy that paths to the hut. Dawn, loop repeats.
- Trees: standing trees are static and non-networked with a simple collider and no Rigidbody. Tree death = one small ObserversRpc. Each client spawns its own local, un-networked falling physics dummy. No transform sync for falling logs.
- Enemies: server-only NavMesh AI. Clients receive interpolated transforms only.
- Elements: enemy element is rolled from the forest's spawn table (about 55% forest element). Killing an elemental enemy grants the killer's axe a limited number of elemental hit charges. Charges of different elements stack.
- Scaling: enemy count, tree density, and seedling drop rates scale on a soft curve with player count, capped at 5 players.
- Out of scope: crafting, inventory beyond the axe, building, runtime procedural generation, VFX, sound polish, UI beyond minimum HUD, mobile, 2D, ECS, imported art assets.

Terminology: the design doc said "saplings", this plan uses **Seedling** everywhere (code, prefabs, UI).

---

## 1. Critical Early Decisions (resolve before coding)

Each item has a recommended default. If nobody objects, the default is the decision.

| # | Decision | Recommended default | Why |
|---|----------|--------------------|-----|
| D1 | When do seedlings drop? | On fell only. Small = 1, Medium = 2, Large = 3 (times scaling, rounded). | Per-hit drops make seedling count depend on swing count, not tree choice. Easy to change later in config. |
| D2 | Player movement authority | Owner-authoritative NetworkTransform with server-side speed/teleport sanity check. Full FishNet CSP (Replicate/Reconcile) deferred. | 1-4 player co-op, no PvP. CSP for a CharacterController is the single biggest time sink and bug source in FishNet. Valheim itself is client-authoritative for movement. |
| D3 | Axe hit authority | Client plays swing and hit flash instantly (prediction of feedback only). Client sends `ServerRpc SwingAxe(aimDir)`. Server runs its own overlap from the server-side player position and applies damage. | Client never names a target, so no spoofed hits. Melee range tolerance absorbs latency. |
| D4 | How do non-networked trees get identified? | Trees are baked into the scene by an editor scatter tool that assigns each a stable `int TreeId`. `TreeRegistry` maps id to tree on every peer. | Stable ids make the "one tiny RPC" possible. Editor-time bake is not runtime procedural generation. |
| D5 | Late joiners | On client join, server sends one `TargetRpc` with the bitset of felled tree ids plus current damage of damaged trees. | Only way late joiners see the correct forest without syncing every tree. |
| D6 | Do trees come back? | At dawn the server regrows felled trees up to the density target for the current player count. | Otherwise forests deplete and the loop ends. |
| D7 | Seedling vs enemy object | One networked `Treant` prefab with two states: `Seedling` (small, still, colored by element) and `Active` (full size, walking). Pooled via FishNet object pool. | Seedling on the ground shows players tonight's threat by color. One object, one pool, no handoff. |
| D8 | Element counters | Cycle: Water beats Fire, Fire beats Wind, Wind beats Earth, Earth beats Water. Opposite forests (N/S, E/W) are not a counter pair except Water/Fire. | Simple 4-cycle, easy to read. Water vs Fire across the map is intuitive. |
| D9 | Do buffs affect trees? | No. Buffs only affect enemies in the MVP. | Keeps tree chopping at fixed 1/2/3 hits as specified. |
| D10 | End of night | Night ends on timer. Surviving enemies wither (despawn to pool). Hut damage persists between nights. Hut at 0 = game over screen, host can restart. | Clear fail state, no win state needed for MVP. |
| D11 | Transport | Tugboat (LAN / direct IP) for the vertical slice. Steam or relay later. | Zero setup. Transport is swappable in FishNet. |
| D12 | Local multiplayer testing | Unity Multiplayer Play Mode (MPPM package) for 2-4 virtual players in one editor. | Official, no project duplication needed. |
| D13 | Render pipeline | URP, one shared `Lit` material per element, emission enabled for elemental objects. | Cheap emission, SRP batcher friendly. |

---

## 2. Project Structure

Unity project location: `E:\Projects\3D-Game\Chopper\` (Unity Hub wants its own folder). Planning docs stay in `E:\Projects\3D-Game\Docs\`.

```
Assets/
  _Chopper/
    Scenes/
      Boot.unity              NetworkManager, main menu (host / join IP)
      World.unity             Terrain plane, hub, forests, baked trees, NavMesh
    Scripts/
      Chopper.Runtime.asmdef
      Core/                   GameConfig, Element enums, scaling, constants
      Networking/             Boot flow, connection UI glue, player spawner
      Player/                 Movement, camera, axe, buffs
      Trees/                  Tree data, registry, manager, falling dummy
      Enemies/                Treant state, AI, spawner, spawn tables
      World/                  Day/night, hut, forest zones
      Pooling/                Local (non-networked) pool
      UI/                     Minimal HUD
      Editor/
        Chopper.Editor.asmdef
        TreeScatterTool.cs
    Prefabs/
      Player/
      Trees/                  Tree_Small, Tree_Medium, Tree_Large, TreeFallDummy_*
      Enemies/                Treant
      World/                  Hut
    Materials/
      Elements/               M_Neutral, M_Water, M_Fire, M_Earth, M_Wind
      World/                  M_Ground, M_Trunk, M_Hut, M_Pond
    Config/                   ScriptableObject instances (GameConfig, SpawnTables, ElementPalette)
  Tests/
    EditMode/                 Chopper.Tests.EditMode.asmdef
Packages/manifest.json        FishNet as a UPM git dependency pinned to tag 4.7.3 (do not edit package source)
```

### Naming conventions

- Namespaces: `Chopper.Core`, `Chopper.Player`, `Chopper.Trees`, etc. matching folders.
- One public type per file, file name = type name.
- Classes, methods, properties: `PascalCase`. Private fields: `_camelCase`. Serialized private fields: `[SerializeField] private float _swingRange;`.
- Networked behaviours end with nothing special, but server-only methods are prefixed `Server` (e.g. `ServerApplyDamage`) and client-only with `Client` (e.g. `ClientPlayFall`). RPCs are suffixed `Rpc`: `SwingAxeServerRpc`, `TreeFelledObserversRpc`, `SyncFelledTreesTargetRpc`.
- Prefabs: `PascalCase` with size suffix (`Tree_Large`). Materials: `M_` prefix. ScriptableObject assets: `SO_` prefix.
- No magic numbers in behaviours: tunables live in `GameConfig` or a per-system ScriptableObject.

---

## 3. Core Scripts

### Core
| Script | Responsibility |
|--------|---------------|
| `Element.cs` | Enum `Neutral, Water, Fire, Earth, Wind` plus static `ElementRules.Beats(a, b)` implementing the counter cycle. |
| `TreeSize.cs` | Enum `Small, Medium, Large` with helpers for hit count (1/2/3) and base seedling drops. |
| `GameConfig.cs` | ScriptableObject holding every tunable (day/night lengths, damage, ranges, charges, scaling curves). |
| `PlayerScaling.cs` | Static helper that returns soft multipliers for enemies, tree density, and seedlings given player count, capped at 5. |
| `ElementPalette.cs` | ScriptableObject mapping each Element to a material and color, used by every visual. |

### Networking
| Script | Responsibility |
|--------|---------------|
| `BootMenu.cs` | Minimal Host / Join (IP field) buttons that start FishNet server+client or client only and load World. |
| `PlayerSpawner.cs` | Spawns the player prefab for each connection at a hub spawn point (or use FishNet's built-in PlayerSpawner component). |

### Player
| Script | Responsibility |
|--------|---------------|
| `PlayerMotor.cs` | Owner-only CharacterController movement, jump, gravity; server checks max speed per tick. |
| `PlayerCamera.cs` | Owner-only third-person orbit camera; disabled on non-owner instances. |
| `PlayerAxe.cs` | Handles swing input, cooldown, local feedback, `SwingAxeServerRpc`, and server-side overlap + damage resolution. |
| `AxeBuffs.cs` | Server-owned `SyncDictionary<Element, int>` of remaining charges; grants, consumes, and computes damage multiplier vs a target element. |
| `PlayerHealth.cs` | Optional in slice; SyncVar health so treants can hit players (can be stubbed as no damage). |

### Trees
| Script | Responsibility |
|--------|---------------|
| `ChoppableTree.cs` | Plain MonoBehaviour on each standing tree holding `TreeId`, `TreeSize`, `ZoneElement`, server-side health, and show/hide helpers. |
| `TreeRegistry.cs` | Plain MonoBehaviour that builds the `TreeId -> ChoppableTree` lookup on every peer at scene load. |
| `TreeManager.cs` | Single scene NetworkBehaviour that owns all tree RPCs: hit, felled, late-join sync, and dawn regrowth. |
| `TreeFallDummy.cs` | Local-only Rigidbody log that is pooled, gets an impulse in the fall direction, and returns to the pool after a few seconds. |

### Enemies
| Script | Responsibility |
|--------|---------------|
| `Treant.cs` | Networked enemy with SyncVars for `Element` and `State` (Seedling/Active/Dead), health, and visual updates on change. |
| `TreantAI.cs` | Server-only NavMeshAgent logic: walk to hut, attack hut (and players in range if enabled). |
| `SeedlingSpawner.cs` | Server-only; on tree fell, rolls element from the zone spawn table and spawns pooled Treants in Seedling state around the stump. |
| `SpawnTable.cs` | ScriptableObject of weighted element odds per forest (55% zone, 25% neutral, 20% split across others). |
| `EnemyDirector.cs` | Server-only; at dusk activates all seedlings (applying enemy cap by scaling), at dawn withers survivors. |

### World
| Script | Responsibility |
|--------|---------------|
| `DayNightCycle.cs` | NetworkBehaviour with SyncVar phase and phase start tick; server advances phases, clients drive sun light and ambient from it. |
| `ForestZone.cs` | Trigger volume that tags its region with an Element; used by the scatter tool and by `AxeBuffs` for HUD hints. |
| `Hut.cs` | NetworkBehaviour with SyncVar health, server-side damage, and game over event at zero. |
| `GameStateManager.cs` | Server-side flow: waiting, playing, game over, restart. |

### Pooling / UI / Editor
| Script | Responsibility |
|--------|---------------|
| `LocalPool.cs` | Generic simple pool for non-networked objects (fall dummies, hit flashes). |
| `Hud.cs` | Text only: phase + timer, hut health, active axe charges by element, seedlings tonight. |
| `TreeScatterTool.cs` | Editor window that places tree prefabs in each ForestZone with a fixed seed, density, and size mix, and assigns sequential TreeIds. |

Networked object pooling uses FishNet's built-in pool (`DefaultObjectPool` + `Despawn(DespawnType.Pool)`) with prewarm, not custom code.

---

## 4. FishNet Setup Steps

1. Create a Unity 6 LTS project with the **Universal 3D (URP)** template at `E:\Projects\3D-Game\Chopper`.
2. Install packages: **AI Navigation** (`com.unity.ai.navigation`), **Multiplayer Play Mode** (`com.unity.multiplayer.playmode`), **Input System**.
3. Import **FishNet** (Asset Store, "FishNet: Networking Evolved", or its git UPM url). Accept the define symbol prompt.
   - Pinned (T04): `com.firstgeargames.fishnet` from `https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.3`. The tag's package.json and `NetworkManager.FISHNET_VERSION` still read 4.7.2; the tag and lock file hash are authoritative. Define symbols `FISHNET;FISHNET_V4` are added automatically.
   - Boot and World are generated by `Chopper > Setup > Rebuild Network Scenes` (`NetworkScenesBuilder`). The online flow lives in `BootMenu` on the persistent NetworkManager object.
4. `Boot.unity`: add a GameObject `NetworkManager` with `NetworkManager`, `Tugboat` transport, `ServerManager`, `ClientManager`, `TimeManager` (tick rate 30), `SceneManager`, `ObserverManager`, `DefaultObjectPool`.
5. Set the Observer default condition to none (or a large `DistanceCondition`) for the slice. With 1-4 players in one map, global observation is fine.
6. Register prefabs: FishNet auto-populates `DefaultPrefabObjects`; confirm Player and Treant are listed.
7. Scene loading: after `ServerManager.StartConnection`, server calls `SceneManager.LoadGlobalScenes(new SceneLoadData("World"))`.
8. `World.unity`: contains `TreeManager`, `DayNightCycle`, `EnemyDirector`, `Hut`, `GameStateManager` as scene NetworkObjects (scene objects are auto-spawned by FishNet).
9. MPPM: enable 1 to 3 virtual players. Main editor hosts, virtual players join 127.0.0.1.

### Pattern: Player (owner-auth movement, server-resolved axe)

```csharp
public class PlayerAxe : NetworkBehaviour
{
    [SerializeField] private float _swingCooldown = 0.6f;
    [SerializeField] private float _swingRange = 2.2f;
    [SerializeField] private float _swingRadius = 0.8f;
    [SerializeField] private LayerMask _hitMask;

    private float _nextSwingTime;
    private float _serverNextSwingTime;
    private AxeBuffs _buffs;

    public override void OnStartClient()
    {
        base.OnStartClient();
        _buffs = GetComponent<AxeBuffs>();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.time < _nextSwingTime) return;

        _nextSwingTime = Time.time + _swingCooldown;
        Vector3 aim = transform.forward;
        ClientPlaySwing();              // instant local animation and hit flash
        SwingAxeServerRpc(aim);
    }

    [ServerRpc]
    private void SwingAxeServerRpc(Vector3 aim)
    {
        // Server validation: cooldown with small latency tolerance.
        if (Time.time < _serverNextSwingTime - 0.1f) return;
        _serverNextSwingTime = Time.time + _swingCooldown;

        aim.y = 0f;
        if (aim.sqrMagnitude < 0.01f) return;
        aim.Normalize();

        Vector3 center = transform.position + Vector3.up + aim * _swingRange * 0.5f;
        // Resolve nearest valid target: Treant first, then tree.
        // Treant: damage = config.axeDamage * _buffs.ServerConsumeAndGetMultiplier(treant.Element)
        // Tree:   TreeManager.Instance.ServerHitTree(tree.TreeId, aim)
        ServerRunSwing(center, aim);

        SwingObserversRpc();            // other clients play the swing animation
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void SwingObserversRpc() => ClientPlaySwing();
}
```

Player prefab components: `NetworkObject`, `NetworkTransform` (Client Authoritative on, interpolation 2), `CharacterController`, `PlayerMotor`, `PlayerCamera`, `PlayerAxe`, `AxeBuffs`.

### Pattern: Trees (static, non-networked, one RPC per event)

```csharp
// Plain MonoBehaviour on every standing tree. No NetworkObject, no Rigidbody.
public class ChoppableTree : MonoBehaviour
{
    [SerializeField] private int _treeId;            // baked by TreeScatterTool
    [SerializeField] private TreeSize _size;
    [SerializeField] private Element _zoneElement;

    public int TreeId => _treeId;
    public TreeSize Size => _size;
    public Element ZoneElement => _zoneElement;
    public int ServerHitsRemaining { get; set; }     // only meaningful on the server
    public bool IsFelled { get; private set; }

    public void SetFelled(bool felled)
    {
        IsFelled = felled;
        gameObject.SetActive(!felled);
    }
}

// One scene NetworkObject that speaks for every tree.
public class TreeManager : NetworkBehaviour
{
    [SerializeField] private TreeRegistry _registry;
    [SerializeField] private SeedlingSpawner _seedlings;

    [Server]
    public void ServerHitTree(int treeId, Vector3 hitDir)
    {
        ChoppableTree tree = _registry.Get(treeId);
        if (tree == null || tree.IsFelled) return;

        tree.ServerHitsRemaining--;
        if (tree.ServerHitsRemaining > 0)
        {
            TreeHitObserversRpc(treeId, (byte)tree.ServerHitsRemaining);
            return;
        }

        tree.SetFelled(true);                        // host sees it immediately too
        _seedlings.ServerSpawnFor(tree);
        TreeFelledObserversRpc(treeId, hitDir);
    }

    [ObserversRpc]
    private void TreeHitObserversRpc(int treeId, byte hitsRemaining)
    {
        // Shake / flash only. No state needed on clients.
    }

    [ObserversRpc(RunLocally = false)]
    private void TreeFelledObserversRpc(int treeId, Vector3 fallDir)
    {
        ChoppableTree tree = _registry.Get(treeId);
        if (tree == null) return;
        tree.SetFelled(true);
        TreeFallDummy.Spawn(tree, fallDir);          // local pooled Rigidbody, never networked
    }

    public override void OnSpawnServer(NetworkConnection conn)
    {
        base.OnSpawnServer(conn);
        SyncFelledTreesTargetRpc(conn, _registry.BuildFelledBitset());
    }

    [TargetRpc]
    private void SyncFelledTreesTargetRpc(NetworkConnection conn, byte[] felledBits)
    {
        _registry.ApplyFelledBitset(felledBits);     // no fall animation for late joiners
    }
}
```

Note on host mode: in host mode the server code path already hid the tree, so the observers handler must tolerate an already-felled tree (spawn the dummy but skip double work). Check `IsServerStarted` or make `SetFelled` idempotent and have the host spawn its dummy in the ObserversRpc like everyone else.

### Pattern: Treant (networked, server AI, pooled)

- Components: `NetworkObject`, `NetworkTransform` (server authoritative, interpolation 2, sync scale off), `NavMeshAgent`, `Treant`, `TreantAI`, capsule collider.
- `Treant`: `readonly SyncVar<Element> _element`, `readonly SyncVar<TreantState> _state`, `readonly SyncVar<int> _health`. `OnChange` callbacks recolor via `ElementPalette` and scale the visual (seedling small, active full size).
- `TreantAI`: in `OnStartServer` enable agent; in `OnStartClient` when `!IsServerStarted` disable the agent component so clients never simulate.
- Pooling: `ServerManager.Spawn(pooledInstance)` and `ServerManager.Despawn(nob, DespawnType.Pool)`. Reset all SyncVars in `OnStartServer`, not `Awake`, because pooled objects skip `Awake` on reuse.

---

## 5. Scaling Curve

`multiplier(n) = 1 + 0.4 * log2(min(n, 5))`

| Players | Multiplier |
|---------|-----------|
| 1 | 1.00 |
| 2 | 1.40 |
| 3 | 1.63 |
| 4 | 1.80 |
| 5+ | 1.93 |

Separate coefficients per system in `GameConfig` (enemy cap, active tree fraction, seedling drop rate) so each can be tuned independently.

## 6. Element Spawn Table (default)

| Forest | Zone element | Neutral | Each other element |
|--------|-------------|---------|--------------------|
| North | Water 55% | 25% | about 6.7% |
| East | Earth 55% | 25% | about 6.7% |
| South | Fire 55% | 25% | about 6.7% |
| West | Wind 55% | 25% | about 6.7% |

## 7. Element Visuals (primitives only)

| Element | Base color | Emission | Seedling / Treant tell |
|---------|-----------|----------|------------------------|
| Neutral | Green `#4F8A3C` | none | plain green canopy |
| Water | Blue `#2F7FD8` | soft blue | blue canopy, blue glow |
| Fire | Orange-red `#E0521F` | strong orange | orange canopy, bright glow |
| Earth | Brown-olive `#7A6232` | none or faint | brown canopy, chunky |
| Wind | Pale cyan `#BFEFEA` | soft white | pale canopy, slight bob |

Forest trees in each zone also get a tinted canopy so players always know which forest they are in. The axe head color shows the element with the most charges; the HUD lists all.

Tree shapes: trunk = cylinder, canopy = cube (Small) / stacked cubes (Medium) / tall stacked cubes (Large). Treant = trunk capsule + cube canopy + two thin cube legs. Hut = cube with a pyramid-ish roof of rotated cubes. Pond = flat blue cylinder.

---

## 8. Task Packets

Rules for every packet: one packet per branch/commit, should compile and play in the editor at the end, no emojis or em dashes in code or comments, touch only files listed unless noted. Each packet lists acceptance criteria (AC) to verify manually or via test.

### Phase A: Project skeleton

**T01 Project creation**
- Create Unity 6 LTS URP project at `Chopper/`, add `.gitignore` (Unity template), `git init`, install AI Navigation, Input System, Multiplayer Play Mode.
- AC: project opens with no console errors; packages listed in manifest; first commit made.

**T02 Folder structure and assemblies**
- Create folder tree from section 2, `Chopper.Runtime.asmdef`, `Chopper.Editor.asmdef` (editor only), `Chopper.Tests.EditMode.asmdef`.
- AC: an empty test in EditMode runs green in Test Runner.

**T03 Core types and config**
- Add `Element`, `ElementRules`, `TreeSize`, `GameConfig`, `PlayerScaling`, `ElementPalette`, `SpawnTable`. Create `SO_GameConfig`, `SO_ElementPalette`, four `SO_SpawnTable_*` assets. Create 5 element materials.
- AC: EditMode tests pass for `ElementRules.Beats` (all 4 cycle pairs true, reverse false, Neutral never beats), `PlayerScaling` values match section 5 table within 0.01, `SpawnTable.Roll` over 100k seeded samples is within 1% of configured odds.

**T04 FishNet import and Boot scene**
- Import FishNet, build `Boot.unity` with NetworkManager setup from section 4, `BootMenu` with Host and Join(IP) buttons, empty `World.unity` with ground plane.
- AC: Host loads World. An MPPM virtual player can Join 127.0.0.1 and also load World. Disconnect returns client to Boot without errors.

### Phase B: Player

**T05 Player prefab and movement**
- Player prefab (capsule + cube "axe"), `PlayerMotor`, `PlayerCamera`, `NetworkTransform` client authoritative, spawn at hub.
- AC: 2 players see each other move smoothly. Each only controls their own. Camera follows only the owner.

**T06 Server movement sanity check**
- Server compares consecutive positions per tick; if speed exceeds `maxSpeed * 1.5` it logs a warning and snaps the player back (TargetRpc).
- AC: normal play never triggers it. A debug key that teleports 50m forward is corrected.

**T07 Axe swing (no damage yet)**
- `PlayerAxe` with input, cooldown, local swing animation (rotate axe cube), `SwingAxeServerRpc`, `SwingObserversRpc`.
- AC: owner swing animates with zero delay; other clients see the swing; spamming click respects cooldown on both client and server (server logs rejected swings).

### Phase C: Trees

**T08 Tree prefabs**
- `Tree_Small`, `Tree_Medium`, `Tree_Large` from primitives with one CapsuleCollider or BoxCollider on the root, static, `Trees` layer, `ChoppableTree` component.
- AC: prefabs have no Rigidbody, no NetworkObject, one collider each.

**T09 World layout blockout**
- In `World.unity`: large ground plane, hub meadow in the center with hut placeholder cube and pond, four `ForestZone` boxes (N/E/S/W) each tagged with element, spawn points in hub.
- AC: flying the scene camera shows the cross layout; each zone's gizmo shows its element color.

**T10 Tree scatter editor tool**
- `TreeScatterTool` editor window: per zone, seed, density, size mix (e.g. 50/35/15), min spacing, exclusion radius around hub. Places prefabs under `Trees/<Zone>`, sets `TreeId` sequentially, sets `ZoneElement`, tints canopy. Clear-and-rebake button.
- AC: rebaking with the same seed produces identical positions and ids. No two trees closer than min spacing. No tree inside hub radius. At least 150 trees per zone by default.

**T11 TreeRegistry**
- Builds dictionary at scene load; validates duplicate ids (error log). Felled bitset build/apply.
- AC: EditMode or PlayMode test: bitset round trip preserves felled states for 1000 ids.

**T12 TreeManager hit and fell**
- Server hit resolution from `PlayerAxe` overlap on `Trees` layer, hit counts 1/2/3 by size, `TreeHitObserversRpc` (shake), `TreeFelledObserversRpc`.
- AC: small tree falls in 1 hit, medium 2, large 3. All clients see the tree vanish on the same hit. Network profiler shows no per-frame tree traffic.

**T13 Local falling dummy + LocalPool**
- `LocalPool`, `TreeFallDummy_*` prefabs (same primitives + Rigidbody), impulse along fall direction, auto-return after 6 seconds, optional stump left behind (short cylinder, local only).
- AC: every client sees a physics fall; dummies are reused (no Instantiate after warmup, check with a counter log); no NetworkObject on the dummy.

**T14 Late-join tree sync**
- `SyncFelledTreesTargetRpc` on join.
- AC: host fells 10 trees, then a new client joins and sees exactly those 10 missing, with no fall animations.

### Phase D: Seedlings and enemies

**T15 Treant prefab and pooling**
- Networked Treant from primitives, SyncVars element/state/health, visual swaps on change, FishNet pool prewarm (e.g. 64).
- AC: debug key on host spawns a Treant of a chosen element in Seedling state; all clients see correct color and small size; despawn to pool and respawn reuses the instance with correct reset state.

**T16 SeedlingSpawner**
- On fell, spawn N seedlings (by size, times scaling) scattered within 2m of the stump, snapped to NavMesh, element rolled from the zone's SpawnTable.
- AC: large tree in Fire forest yields 3 seedlings (1 player); over many fells, Fire seedlings are clearly the majority. Seedlings on the ground are visible to all clients including late joiners.

**T17 NavMesh bake**
- `NavMeshSurface` on the ground, trees carve or are marked not walkable, hut has a NavMeshObstacle.
- AC: an agent from each forest's far edge can path to the hut.

**T18 TreantAI**
- Server-only agent; in Active state walk to hut, stop in attack range, hit hut every N seconds. Agents disabled on pure clients.
- AC: activated Treant walks to hut on host and remote clients see smooth interpolated motion; no NavMeshAgent is enabled on clients.

### Phase E: Loop

**T19 DayNightCycle**
- Phases Day, Dusk (short), Night, Dawn (short). SyncVar phase + start tick. Clients rotate directional light and change ambient color.
- AC: all clients agree on phase and remaining time within 0.2 seconds; late joiner gets correct phase and time.

**T20 EnemyDirector**
- At Night start, activate seedlings up to enemy cap (scaled); excess seedlings stay dormant for the next night or wither (decide in config). At Dawn, wither remaining Active Treants to pool. Regrow felled trees per D6.
- AC: 10 seedlings during day become 10 walking Treants at night; at dawn all Treants despawn; felled trees reappear on all clients at dawn.

**T21 Hut health and game over**
- `Hut` SyncVar health, damage from Treants, `GameStateManager` game over and host restart (reset hut, trees, pool).
- AC: letting enough Treants reach the hut ends the game for all clients; restart returns to Day 1 with full forests and no leftover Treants.

### Phase F: Combat and elements

**T22 Treant damage from axe**
- Axe overlap now also hits Treants (priority over trees), base damage from config, Treant death despawns to pool.
- AC: Treant takes the configured number of hits; kill is seen by all clients; dead Treant is never hit twice (server state check).

**T23 AxeBuffs**
- `SyncDictionary<Element, int>` charges. Kill of an elemental Treant grants charges of that element to the killer (config, e.g. 8). Each hit on a Treant consumes 1 charge of every active element. Damage multiplier: +50% for each active element that beats the target, -25% for each active element the target beats, floor 0.5. Charge cap per element (config, e.g. 24).
- AC: EditMode tests for multiplier math. In play: killing a Fire Treant shows Fire charges on the killer's HUD only; hitting a Wind Treant with Fire charges deals boosted damage; charges count down per hit and disappear at 0.

**T24 Axe element visual**
- Axe head recolors to the element with the most charges (neutral when none), emission on.
- AC: other players see the correct axe color on each player.

### Phase G: Polish to vertical slice

**T25 Minimal HUD**
- Phase + timer, hut health, own axe charges, seedlings pending tonight (from a server SyncVar count).
- AC: values correct on host and clients; all text uses plain ASCII punctuation.

**T26 Player count scaling wiring**
- Enemy cap, active tree fraction at dawn, and seedling multiplier read `PlayerScaling` with live connection count.
- AC: debug overlay shows current multipliers; joining a second player changes them at next dawn/fell as expected.

**T27 Four player soak test**
- 4 MPPM players, 3 full day/night cycles.
- AC: no console errors, no rubber-banding trees, no orphaned Treants, frame time stable, network profiler shows tree traffic only on hit/fell events.

---

## 9. Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| FishNet API drift between major versions (SyncVar syntax changed in v4) | Agent-written code fails to compile | Pin the FishNet version in T04 and state it in every task packet. |
| Pooled NetworkObjects keep stale state | Wrong colors or states on reuse | Reset in `OnStartServer`/`OnStartClient`, test in T15. |
| Host double-processing (host is server and client) | Trees fell twice, double dummies | Idempotent `SetFelled`, explicit host path in T12. |
| NavMesh with hundreds of tree colliders | Bake time, agents stuck | Treat trees as NavMeshObstacles only for large trees, or bake trees as not walkable and keep spacing wide. |
| Tree id mismatch between builds | Wrong tree falls on clients | Ids are baked into the scene; server and clients must run the same build. Add a scene hash check on connect later. |
| Axe hit feel with latency | Hits register late | Feedback is local and instant; only damage waits for server. Test at 150ms simulated latency (FishNet LatencySimulator). |
| Scope creep | Slice slips | Section 0 out of scope list is binding. |
