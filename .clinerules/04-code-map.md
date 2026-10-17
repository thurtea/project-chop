# 04 Where things are (Assets/_Chopper/Scripts)

| Folder | What | Kind |
|---|---|---|
| `Core/` | Pure rules and numbers: `CombatRules`, `WeaponRules`, `ElementRules`, `ElementBuffRules`, `FishingRules`, `MiningRules`, `SteerRules`, `SkillRules`, `GameConfig` | static classes, no scene code, tested in `Assets/Tests/EditMode` |
| `Player/` | One component per job on the player: `PlayerMotor` (move), `PlayerAxe` (swings, weapon keys, shield key), `PlayerWeapons` (sword/mace/spear owned and shown), `PlayerCombat` (block, parry, roll), `PlayerHealth`, `PlayerUpgrades`, `PlayerBuffs` (element charges), `PlayerFishing`, `PlayerMining`, `PlayerBoat`, `PlayerAnimator`, `PlayerOutfit` | NetworkBehaviour / MonoBehaviour |
| `Enemies/` | `Treant` (tree enemy: health, hit, stagger), `TreantAI`, `EnemyDirector` (spawns, saplings, bosses) | NetworkBehaviour |
| `Trees/` | `TreeManager` (find, hit, fell trees), remains | NetworkBehaviour |
| `World/` | `Hut`, `TutorialYard`, `DayNightCycle`, building (`Building/BuildManager`), saving (`Saving/`), generation (`Generation/`) | mixed |
| `UI/` | `GameUi` (HUD, hotbar), `Keybinds`, `PadHotbar`, `InventoryView`, `FishingView`, `HotbarIcons` | built in code, no prefabs |
| `Networking/` | `ShotCapture` (screenshot tours), `BootMenu`, `SmokeTest` | test and boot code |
| `Editor/` | Editor batch setup methods (`WeaponAnimSetup`, `CharacterSetup`) | only run through `homework.ps1 unity <Class.Method>` |

## Copy these patterns (they are the real APIs here)

Synced value (FishNet 4, generic SyncVar, no attribute):
```csharp
private readonly SyncVar<int> _level = new SyncVar<int>();
public int Level => _level.Value;
```
Client asks, server does:
```csharp
public void RequestThing(int value)
{
    if (!IsOwner) return;
    ThingServerRpc(value);
}

[ServerRpc]
private void ThingServerRpc(int value) => _level.Value = Mathf.Max(0, value);
```
New tunable number in `Core/GameConfig.cs` (field plus property, keep both):
```csharp
[SerializeField, Min(0f)] private float _thingSeconds = 2f;
public float ThingSeconds => _thingSeconds;
```
Key press (keys are `KeyAction` values in `UI/Keybinds.cs`):
```csharp
if (Keybinds.WasPressed(KeyAction.Shield)) RequestShieldToggle();
```
EditMode test (NUnit, namespace `Chopper.Tests`):
```csharp
[Test]
public void StaggeredEnemiesTakeDouble()
{
    Assert.AreEqual(8f, CombatRules.StaggeredDamage(4f, true, 2f), 0.0001f);
}
```

## Facts that trip models up
- There is no `UnityEngine.Networking`, no `[SyncVar]` attribute, no `NetworkVariable`. Use the patterns above.
- `IsServerInitialized` means "running on the server". `IsOwner` means "this is my player".
- Put new pure logic in a `Core/` static class with a test; keep MonoBehaviours thin.
- The player prefab already has every component. Get one with `GetComponent<PlayerAxe>()`; do not `AddComponent` a NetworkBehaviour.
