# 02 Game rules the code must keep

The full design is `Chopper/Docs/gamedesigndoc.md`. Read only the section you need.

- Co-op 1 to 4 players. The server decides gameplay. A client asks with a `[ServerRpc]`; the server changes a `SyncVar`.
- Weapons: Axe, Sword, Mace, Spear only (`Core/WeaponRules.cs`). No ranged weapons.
- The tree chop (logging) is separate from axe combat. Do not merge them.
- Hotbar (11 slots): 1 Shield, 2 Axe, 3 Torch, 4 Bandage, 5 Rod, 6 Fish, 7 Hammer, 8 Pick, 9 Sword, 0 Mace, - Spear (`UI/Keybinds.cs`, `UI/PadHotbar.cs`, `UI/GameUi.cs`).
- Elements: Water beats Fire, Fire beats Wind, Wind beats Earth, Earth beats Water (`Core/ElementRules.cs`).
- Enemy weapons follow the element: earth mace, fire sword, water spear, wind axe, neutral random.
- Blocking is Valheim style: block cuts damage, a parry (block just before the hit) staggers, staggered enemies take double damage (`Core/CombatRules.cs`).
- Sprint and crouch are toggles. Swimming holsters everything held.
- Only felled forest trees plant saplings; the hub plants none.
- Numbers players tune live in `Core/GameConfig.cs` (with a default) and are read as `_config.Name`. Do not hard-code a new number in gameplay code.
