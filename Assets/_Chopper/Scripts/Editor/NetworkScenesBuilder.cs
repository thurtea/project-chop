using Chopper.Core;
using Chopper.Networking;
using Chopper.Player;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Object;
using FishNet.Managing.Observing;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using FishNet.Utility.Performance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.Editor
{
    // Regenerates the Player prefab, Boot.unity and World.unity, and registers the scenes in Build Settings
    // (plan section 4, T04 to T07). Menu: Chopper > Setup > Rebuild Network Scenes.
    // Batch: -executeMethod Chopper.Editor.NetworkScenesBuilder.Build
    public static class NetworkScenesBuilder
    {
        private const string ScenesFolder = "Assets/_Chopper/Scenes";
        private const string BootPath = ScenesFolder + "/Boot.unity";
        private const string WorldPath = ScenesFolder + "/World.unity";
        private const string PlayerPrefabPath = "Assets/_Chopper/Prefabs/Player/Player.prefab";
        private const string PalettePath = "Assets/_Chopper/Config/SO_ElementPalette.asset";
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";
        private const string DefaultPrefabsPath = "Assets/DefaultPrefabObjects.asset";

        private const ushort TickRate = 30;
        private const ushort Port = 7770;
        private const string LocalAddress = "127.0.0.1";
        private const ushort NetworkTransformInterpolation = 2;

        // Chopper's short, broad collision shape has its pivot at his feet.
        private const float PlayerHeight = 1.7f;
        private const float PlayerRadius = 0.5f;

        [MenuItem("Chopper/Setup/Rebuild Network Scenes")]
        public static void Build()
        {
            // FishNet regenerates its prefab list on every asset import, which floods the log while this builder
            // writes many prefabs. Pause it, then do one full regeneration at the end.
            SetPrefabGeneratorPaused(true);
            try
            {
                BuildAll();
            }
            finally
            {
                SetPrefabGeneratorPaused(false);
            }
            RegeneratePrefabList();
            AssetDatabase.SaveAssets();
            Debug.Log("[Chopper] FishNet prefab list regenerated.");

        }

        // FishNet's generator class is internal, so it is reached by reflection.
        private static System.Type GeneratorType =>
            typeof(NetworkObject).Assembly.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator");

        private static void SetPrefabGeneratorPaused(bool paused)
        {
            System.Reflection.FieldInfo field = GeneratorType?.GetField("IgnorePostProcess");
            if (field != null) field.SetValue(null, paused);
        }

        private static void RegeneratePrefabList()
        {
            System.Reflection.MethodInfo method = GeneratorType?.GetMethod("GenerateFull");
            if (method == null)
            {
                Debug.LogWarning("[Chopper] Could not find FishNet's prefab generator; run Tools > Fishnet > Refresh Default Prefabs.");
                return;
            }
            // Arguments: settings (default), forced, initializeAdded.
            method.Invoke(null, new object[] { null, true, true });
        }

        private static void BuildAll()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            // Writes any newly added GameConfig fields into the asset with their defaults.
            AssetDatabase.ForceReserializeAssets(new[] { GameConfigPath });

            NetworkObject playerPrefab = BuildPlayerPrefab(config);
            NetworkObject[] worldPrefabs = MeadowBuilder.BuildAssetsAndPrefabs(config);
            DefaultPrefabObjects defaultPrefabs = GetOrCreateDefaultPrefabs();
            foreach (NetworkObject worldPrefab in worldPrefabs) defaultPrefabs.AddObject(worldPrefab, checkForDuplicates: true);
            defaultPrefabs.AddObject(MeadowBuilder.TreantPrefab, checkForDuplicates: true);
            defaultPrefabs.AddObject(MeadowBuilder.WoodPilePrefab, checkForDuplicates: true);
            EditorUtility.SetDirty(defaultPrefabs);
            BuildBoot(playerPrefab, worldPrefabs);
            BuildWorld(config);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(WorldPath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Chopper] Player prefab, Boot and World rebuilt; scenes registered in Build Settings.");
        }

        [MenuItem("Chopper/Art/Use Chopper Player")]
        public static void UseChopperPlayer()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            BuildPlayerPrefab(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[Chopper] Player prefab rebuilt with the Chopper model.");
        }

        private static NetworkObject BuildPlayerPrefab(GameConfig config)
        {
            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();

            bool chopper = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.ChopperModelPath) != null;
            float height = chopper ? 1.8f : PlayerHeight;
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = PlayerRadius;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            // Animated mannequin with an axe in the right hand (see CharacterSetup).
            CharacterSetup.Result character = CharacterSetup.AttachModel(root);
            Transform axePivot = character != null ? character.AxeGrip : root.transform;
            Renderer axeHead = character != null ? character.AxeRenderer : null;

            var cameraGo = new GameObject("PlayerCamera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(root.transform, false);
            Camera camera = cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            LightingSetup.EnableCameraEffects(camera);
            cameraGo.SetActive(false);

            PlayerCamera playerCamera = root.AddComponent<PlayerCamera>();
            PlayerMotor motor = root.AddComponent<PlayerMotor>();
            PlayerMovementValidator validator = root.AddComponent<PlayerMovementValidator>();
            PlayerWood wood = root.AddComponent<PlayerWood>();
            PlayerStamina stamina = root.AddComponent<PlayerStamina>();
            PlayerInventory inventory = root.AddComponent<PlayerInventory>();
            PlayerHealth playerHealth = root.AddComponent<PlayerHealth>();
            PlayerBuffs buffs = root.AddComponent<PlayerBuffs>();
            PlayerUpgrades upgrades = root.AddComponent<PlayerUpgrades>();
            root.AddComponent<PlayerCrypt>();
            PlayerMining mining = root.AddComponent<PlayerMining>();
            SetSerialized(mining, "_pickPivot", p => p.objectReferenceValue = character != null ? character.PickaxeGrip : null);
            root.AddComponent<PlayerIdentity>();
            PlayerAxe axe = root.AddComponent<PlayerAxe>();
            PlayerAnimator playerAnimator = root.AddComponent<PlayerAnimator>();
            PlayerCombat combat = root.AddComponent<PlayerCombat>();
            SetSerialized(combat, "_config", p => p.objectReferenceValue = config);
            root.AddComponent<PlayerDevTools>();
            PlayerBoons boons = root.AddComponent<PlayerBoons>();
            SetSerialized(boons, "_config", p => p.objectReferenceValue = config);
            // Each player's character creator look, synced to everyone.
            if (character != null && character.Outfitter != null)
            {
                PlayerAppearance appearance = root.AddComponent<PlayerAppearance>();
                SetSerialized(appearance, "_outfitter", p => p.objectReferenceValue = character.Outfitter);
            }
            PlayerFishing fishing = root.AddComponent<PlayerFishing>();
            SetSerialized(fishing, "_config", p => p.objectReferenceValue = config);
            // The bed (sleeping through the night) and the hammer (building pieces).
            root.AddComponent<PlayerRest>();
            root.AddComponent<PlayerBuilder>();
            // Client authoritative movement (plan D2), interpolation 2, no scale sync.
            // CharacterController configuration makes FishNet enable the controller only for the owner.
            SetSerialized(networkTransform, "_clientAuthoritative", p => p.boolValue = true);
            SetSerialized(networkTransform, "_interpolation", p => p.intValue = NetworkTransformInterpolation);
            SetSerialized(networkTransform, "_synchronizeScale", p => p.boolValue = false);
            SetSerialized(networkTransform, "_componentConfiguration",
                p => p.intValue = (int)NetworkTransform.ComponentConfigurationType.CharacterController);

            SetSerialized(playerCamera, "_config", p => p.objectReferenceValue = config);
            SetSerialized(playerCamera, "_camera", p => p.objectReferenceValue = camera);
            SetSerialized(motor, "_config", p => p.objectReferenceValue = config);
            SetSerialized(motor, "_playerCamera", p => p.objectReferenceValue = playerCamera);
            SetSerialized(validator, "_config", p => p.objectReferenceValue = config);
            SetSerialized(validator, "_networkTransform", p => p.objectReferenceValue = networkTransform);
            SetSerialized(wood, "_config", p => p.objectReferenceValue = config);
            SetSerialized(stamina, "_config", p => p.objectReferenceValue = config);
            SetSerialized(inventory, "_config", p => p.objectReferenceValue = config);
            SetSerialized(inventory, "_torchVisual", p => p.objectReferenceValue = character != null ? character.TorchVisual : null);
            SetSerialized(playerHealth, "_config", p => p.objectReferenceValue = config);
            SetSerialized(upgrades, "_config", p => p.objectReferenceValue = config);
            SetSerialized(buffs, "_config", p => p.objectReferenceValue = config);
            SetSerialized(buffs, "_palette", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ElementPalette>(PalettePath));
            SetSerialized(buffs, "_axeHead", p => p.objectReferenceValue = axeHead);
            SetSerialized(axe, "_config", p => p.objectReferenceValue = config);
            SetSerialized(axe, "_playerCamera", p => p.objectReferenceValue = playerCamera);
            SetSerialized(axe, "_axePivot", p => p.objectReferenceValue = axePivot);
            // With a character animator the chop comes from the animation, not from rotating the axe in code.
            SetSerialized(axe, "_useScriptedSwing", p => p.boolValue = character == null);
            SetSerialized(playerAnimator, "_config", p => p.objectReferenceValue = config);
            SetSerialized(playerAnimator, "_animator", p => p.objectReferenceValue = character != null ? character.Animator : null);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            NetworkObject prefab = saved.GetComponent<NetworkObject>();
            DefaultPrefabObjects prefabs = GetOrCreateDefaultPrefabs();
            prefabs.AddObject(prefab, checkForDuplicates: true);
            EditorUtility.SetDirty(prefabs);
            return prefab;
        }

        private static void BuildBoot(NetworkObject playerPrefab, NetworkObject[] worldPrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(Vector3.zero, Quaternion.identity, CameraClearFlags.SolidColor);

            var go = new GameObject("NetworkManager");
            NetworkManager networkManager = go.AddComponent<NetworkManager>();
            TransportManager transportManager = go.AddComponent<TransportManager>();
            Tugboat tugboat = go.AddComponent<Tugboat>();
            // Steam relays for playing with friends (no IP addresses); NetworkBootstrap picks one transport at start.
            var steam = go.AddComponent<FishySteamworks.FishySteamworks>();
            SetSerialized(steam, "_peerToPeer", p => p.boolValue = true);
            SetSerialized(steam, "_maximumClients", p => p.intValue = Chopper.Networking.SteamLobby.MaxPlayers);
            go.AddComponent<ServerManager>();
            go.AddComponent<ClientManager>();
            TimeManager timeManager = go.AddComponent<TimeManager>();
            go.AddComponent<SceneManager>();
            go.AddComponent<ObserverManager>();
            DefaultObjectPool pool = go.AddComponent<DefaultObjectPool>();
            go.AddComponent<BootMenu>();
            PlayerSpawner spawner = go.AddComponent<PlayerSpawner>();
            // The NetworkManager outlives every scene change: it keeps one listener on while the scenes swap.
            go.AddComponent<AudioListener>();
            go.AddComponent<Chopper.Audio.ListenerKeeper>();

            networkManager.SpawnablePrefabs = GetOrCreateDefaultPrefabs();
            transportManager.Transport = tugboat;
            tugboat.SetPort(Port);
            tugboat.SetClientAddress(LocalAddress);
            timeManager.SetTickRate(TickRate);

            SetSerialized(networkManager, "_objectPool", p => p.objectReferenceValue = pool);
            SetSerialized(timeManager, "_tickRate", p => p.intValue = TickRate);
            // No default observer conditions: every client observes every object (plan section 4, step 5).
            SetSerialized(go.GetComponent<ObserverManager>(), "_defaultConditions", p => p.arraySize = 0);
            SetSerialized(spawner, "_playerPrefab", p => p.objectReferenceValue = playerPrefab);
            SetSerialized(spawner, "_worldPrefabs", p =>
            {
                p.arraySize = worldPrefabs.Length;
                for (int i = 0; i < worldPrefabs.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = worldPrefabs[i];
            });

            // The NetworkManager wakes up only once NetworkBootstrap has chosen the transport.
            go.SetActive(false);
            var bootstrapGo = new GameObject("NetworkBootstrap");
            NetworkLatencySimulation latencySimulation = bootstrapGo.AddComponent<NetworkLatencySimulation>();
            NetworkBootstrap bootstrap = bootstrapGo.AddComponent<NetworkBootstrap>();
            SetSerialized(bootstrap, "_networkManager", p => p.objectReferenceValue = go);
            SetSerialized(bootstrap, "_steam", p => p.objectReferenceValue = steam);
            SetSerialized(bootstrap, "_local", p => p.objectReferenceValue = tugboat);
            SetSerialized(bootstrap, "_latencySimulation", p => p.objectReferenceValue = latencySimulation);

            EditorUtility.SetDirty(go);
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        private static void BuildWorld(GameConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MeadowBuilder.PopulateScene(config);
            EditorSceneManager.SaveScene(scene, WorldPath);
        }

        private static void CreateCamera(Vector3 position, Quaternion rotation, CameraClearFlags clear)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = clear;
            camera.backgroundColor = Color.black;
            go.AddComponent<AudioListener>();
            go.transform.SetPositionAndRotation(position, rotation);
        }

        private static DefaultPrefabObjects GetOrCreateDefaultPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(DefaultPrefabObjects));
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            var prefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
            AssetDatabase.CreateAsset(prefabs, DefaultPrefabsPath);
            return prefabs;
        }

        private static void SetSerialized(Object target, string propertyPath, System.Action<SerializedProperty> apply)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property == null)
            {
                Debug.LogError($"[Chopper] {target.GetType().Name} has no serialized field {propertyPath}.");
                return;
            }
            apply(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
