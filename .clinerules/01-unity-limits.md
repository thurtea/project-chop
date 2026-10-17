# Unity 6 Framework Rules
- This is a Unity 6000.5.10f1 project using FishNet and URP.
- NEVER insert single-line '//' or multi-line '/* ... */' comments into any code.
- Append all logical explanations directly to `Chopper/Docs/TASKBOARD.md`.
- All NetworkBehaviours must reside on `Player.prefab` via editor setups and `NetworkScenesBuilder`.
- In-build vertex-color meshes must use the world material (`Resources/Props/prop_boat_voxel`).
