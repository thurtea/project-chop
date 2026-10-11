using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chopper.EditorTools
{
    /// <summary>
    /// Rerunnable import of the Blender exports (Chopper/Art/Export/*.fbx) and rebuild of the
    /// materials, Animator Controller, Chopper.prefab and the TestScene placement.
    ///   Unity.exe -batchmode -nographics -quit -projectPath Chopper -executeMethod Chopper.EditorTools.ChopperImport.ImportAndBuild
    /// </summary>
    public static class ChopperImport
    {
        public const string Root = "Assets/Game/Characters/Chopper";
        public const string ModelsDir = Root + "/Models";
        public const string MaterialsDir = Root + "/Materials";
        public const string AnimationDir = Root + "/Animation";
        public const string CharFbx = ModelsDir + "/chopper.fbx";
        public const string AxeFbx = ModelsDir + "/chopper_axe.fbx";
        public const string ShieldFbx = ModelsDir + "/chopper_shield.fbx";
        public const string MatChopperPath = MaterialsDir + "/M_Chopper.mat";
        public const string MatGearPath = MaterialsDir + "/M_Chopper_Gear.mat";
        public const string ControllerPath = AnimationDir + "/Chopper.controller";
        public const string PrefabPath = Root + "/Chopper.prefab";
        public const string WorkerPrefabPath = Root + "/Chopper_Worker.prefab";
        public const string ElitePrefabPath = Root + "/Chopper_Elite.prefab";
        public const string AtlasPath = Root + "/Textures/chopper_atlas.png";
        public const string PineFbx = "Assets/Game/Props/prop_pine_voxel.fbx";
        public const string PineMatPath = "Assets/Game/Props/M_Pine.mat";
        public const string PinePrefabPath = "Assets/Game/Props/prop_pine_voxel.prefab";
        public const string ShaderName = "Chopper/VertexColorLit";
        public const string WeaponSocket = "Weapon_R";
        public const string ShieldSocket = "Shield_L";
        public const float CharacterHeight = 1.8f;
        public const float SpeedThreshold = 0.1f;
        public const float RunThreshold = 1.5f;

        static readonly string[] ArmBones =
        {
            "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
            "Weapon_R", "Shield_L"
        };

        static string ExportDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Art", "Export"));

        [MenuItem("Chopper/Import And Build Character")]
        public static void ImportAndBuild()
        {
            var log = new StringBuilder();
            bool ok = true;
            try
            {
                CopyExports(log);
                var matChopper = EnsureMaterial(MatChopperPath, "M_Chopper");
                var matGear = EnsureMaterial(MatGearPath, "M_Chopper_Gear");
                ConfigureCharacter(matChopper, matGear, log);
                ConfigureProp(AxeFbx, matGear, log);
                ConfigureProp(ShieldFbx, matGear, log);
                var controller = BuildController(log);
                BuildPrefab(controller, log);
                PlaceInTestScene(log);
                ImportPine(log);
                ImportMeadow(log);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                ok = false;
                Debug.LogException(e);
            }

            Debug.Log("[ChopperImport]\n" + log + "\n[ChopperImport] RESULT: " + (ok ? "DONE" : "FAILED"));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        // ------------------------------------------------------------------ copy

        static void CopyExports(StringBuilder log)
        {
            Directory.CreateDirectory(ModelsDir);
            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(AnimationDir);
            Directory.CreateDirectory(Root + "/Textures");
            foreach (var dest in new[] { CharFbx, AxeFbx, ShieldFbx })
            {
                string src = Path.Combine(ExportDir, Path.GetFileName(dest));
                if (!File.Exists(src)) throw new FileNotFoundException("Missing export " + src);
                bool same = File.Exists(dest) && File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dest));
                if (!same) File.Copy(src, dest, true);
                log.AppendLine($"copy {src} -> {dest} ({(same ? "unchanged" : "copied")}, {new FileInfo(src).Length} bytes)");
            }
            string atlasSrc = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Art", "Textures", "chopper_atlas.png"));
            if (!File.Exists(atlasSrc)) throw new FileNotFoundException("Missing atlas " + atlasSrc);
            bool atlasSame = File.Exists(AtlasPath) && File.ReadAllBytes(atlasSrc).SequenceEqual(File.ReadAllBytes(AtlasPath));
            if (!atlasSame) File.Copy(atlasSrc, AtlasPath, true);
            log.AppendLine($"copy {atlasSrc} -> {AtlasPath} ({(atlasSame ? "unchanged" : "copied")})");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var texImp = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            if (texImp != null)
            {
                texImp.sRGBTexture = true;
                texImp.mipmapEnabled = true;
                texImp.maxTextureSize = 2048;
                texImp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ materials

        static Material EnsureMaterial(string path, string name)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new Exception("Shader not found: " + ShaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_AmbientStrength", 1f);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas != null)
            {
                mat.SetTexture("_BaseMap", atlas);
                mat.SetFloat("_UseAtlas", 1f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static IEnumerable<string> SourceMaterialNames(ModelImporter imp, string path)
        {
            var names = new HashSet<string>();
            foreach (var kv in imp.GetExternalObjectMap())
                if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                names.Add(m.name);
            return names;
        }

        static void RemapMaterials(ModelImporter imp, string path, Func<string, Material> pick, StringBuilder log)
        {
            foreach (var n in SourceMaterialNames(imp, path).ToList())
            {
                var target = pick(n);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), target);
                log.AppendLine($"  material remap {Path.GetFileName(path)}: '{n}' -> {AssetDatabase.GetAssetPath(target)}");
            }
        }

        // ------------------------------------------------------------------ character

        static void ApplyCommonModelSettings(ModelImporter imp)
        {
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.bakeAxisConversion = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importVisibility = false;
            imp.importBlendShapes = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.importNormals = ModelImporterNormals.Import;
            imp.isReadable = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        static void ConfigureCharacter(Material matChopper, Material matGear, StringBuilder log)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(CharFbx);
            ApplyCommonModelSettings(imp);
            imp.importAnimation = true;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.optimizeGameObjects = false;
            imp.animationCompression = ModelImporterAnimationCompression.Off;
            imp.SaveAndReimport(); // pass 1: hierarchy, takes and source materials become known

            imp = (ModelImporter)AssetImporter.GetAtPath(CharFbx);
            RemapMaterials(imp, CharFbx, n => n.Contains("Gear") ? matGear : matChopper, log);
            imp.humanDescription = BuildHumanDescription(log);
            imp.clipAnimations = BuildClips(imp, log);
            imp.SaveAndReimport(); // pass 2: avatar, clips, materials

            var avatar = LoadAvatar();
            log.AppendLine($"avatar: {(avatar ? avatar.name : "<none>")} valid={avatar && avatar.isValid} human={avatar && avatar.isHuman}");
        }

        static HumanDescription BuildHumanDescription(StringBuilder log)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            var temp = UnityEngine.Object.Instantiate(modelAsset);
            temp.name = modelAsset.name;
            try
            {
                var all = temp.GetComponentsInChildren<Transform>(true);
                var byName = new Dictionary<string, Transform>();
                foreach (var t in all) if (!byName.ContainsKey(t.name)) byName[t.name] = t;

                // Enforce a T-pose on the avatar's reference skeleton: the FBX is bound in an A-pose
                // (arms about 45 degrees down). Rotate the upper and lower arms so the arm chain is
                // horizontal and straight, as Unity's Configure > Enforce T-Pose does.
                log.AppendLine("T-pose enforcement on the avatar reference skeleton:");
                foreach (var side in new[] { "Left", "Right" })
                {
                    AlignChain(byName, side + "UpperArm", side + "LowerArm", log);
                    AlignChain(byName, side + "LowerArm", side + "Hand", log);
                }

                var human = new List<HumanBone>();
                log.AppendLine("humanoid bone mapping (Unity name -> our bone):");
                for (int i = 0; i < HumanTrait.BoneCount; i++)
                {
                    string unityName = HumanTrait.BoneName[i];
                    string ours = unityName.Replace(" ", "");
                    if (!byName.ContainsKey(ours)) continue;
                    var hb = new HumanBone { humanName = unityName, boneName = ours };
                    hb.limit.useDefaultValues = true;
                    human.Add(hb);
                    log.AppendLine($"  {unityName,-14} -> {ours}{(HumanTrait.RequiredBone(i) ? "  (required)" : "")}");
                }
                for (int i = 0; i < HumanTrait.BoneCount; i++)
                    if (HumanTrait.RequiredBone(i) && human.All(h => h.humanName != HumanTrait.BoneName[i]))
                        log.AppendLine("  MISSING required bone: " + HumanTrait.BoneName[i]);

                var skeleton = all.Select(t => new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                }).ToArray();

                return new HumanDescription
                {
                    human = human.ToArray(),
                    skeleton = skeleton,
                    upperArmTwist = 0.5f,
                    lowerArmTwist = 0.5f,
                    upperLegTwist = 0.5f,
                    lowerLegTwist = 0.5f,
                    armStretch = 0.05f,
                    legStretch = 0.05f,
                    feetSpacing = 0f,
                    hasTranslationDoF = false
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        static void AlignChain(Dictionary<string, Transform> byName, string boneName, string childName, StringBuilder log)
        {
            var bone = byName[boneName];
            var child = byName[childName];
            Vector3 dir = (child.position - bone.position).normalized;
            Vector3 target = new Vector3(Mathf.Sign(dir.x), 0f, 0f);
            float angle = Vector3.Angle(dir, target);
            bone.rotation = Quaternion.FromToRotation(dir, target) * bone.rotation;
            log.AppendLine($"  {boneName}: rotated {angle:F1} deg to horizontal");
        }

        static readonly (string name, bool loop)[] Clips =
        {
            ("Idle", true), ("Walk", true), ("Run", true),
            ("Attack_1", false), ("Attack_2", false), ("HitReact", false), ("Death", false)
        };

        static ModelImporterClipAnimation[] BuildClips(ModelImporter imp, StringBuilder log)
        {
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var spec in Clips)
            {
                var c = imp.defaultClipAnimations.FirstOrDefault(t =>
                    t.takeName == spec.name || t.takeName.EndsWith("|" + spec.name) || t.name == spec.name);
                if (c == null)
                {
                    log.AppendLine("  MISSING take " + spec.name);
                    continue;
                }
                c.name = spec.name;
                c.loopTime = spec.loop;
                c.loopPose = spec.loop;
                c.cycleOffset = 0f;
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.heightFromFeet = false;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                c.mirror = false;
                clips.Add(c);
                log.AppendLine($"  clip {spec.name}: take '{c.takeName}' frames {c.firstFrame}-{c.lastFrame}, loop={spec.loop}");
            }
            return clips.ToArray();
        }

        public static Avatar LoadAvatar() =>
            AssetDatabase.LoadAllAssetsAtPath(CharFbx).OfType<Avatar>().FirstOrDefault();

        public static AnimationClip LoadClip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(CharFbx).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__"));

        // ------------------------------------------------------------------ props

        static void ConfigureProp(string path, Material matGear, StringBuilder log)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            ApplyCommonModelSettings(imp);
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.SaveAndReimport();
            imp = (ModelImporter)AssetImporter.GetAtPath(path);
            RemapMaterials(imp, path, _ => matGear, log);
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ controller

        static AnimatorController BuildController(StringBuilder log)
        {
            var idle = LoadClip("Idle");
            var walk = LoadClip("Walk");
            var run = LoadClip("Run");
            if (idle == null || walk == null || run == null) throw new Exception("Idle, Walk or Run clip missing in " + CharFbx);

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                       ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var p in ctrl.parameters.ToArray()) ctrl.RemoveParameter(p);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
            foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);

            AnimatorState Add(string name, Vector3 pos)
            {
                var clip = LoadClip(name);
                var st = sm.AddState(name, pos);
                st.motion = clip;
                st.iKOnFeet = name != "Death";
                return st;
            }

            var sIdle = Add("Idle", new Vector3(300, 0, 0));
            var sWalk = Add("Walk", new Vector3(300, 80, 0));
            var sRun = Add("Run", new Vector3(300, 160, 0));
            var sA1 = Add("Attack_1", new Vector3(560, 0, 0));
            var sA2 = Add("Attack_2", new Vector3(560, 80, 0));
            var sHit = Add("HitReact", new Vector3(560, 160, 0));
            var sDie = Add("Death", new Vector3(560, 240, 0));
            sm.defaultState = sIdle;

            void SpeedTo(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
            {
                var tr = from.AddTransition(to);
                tr.hasExitTime = false;
                tr.duration = 0.15f;
                tr.AddCondition(mode, threshold, "Speed");
            }
            SpeedTo(sIdle, sWalk, AnimatorConditionMode.Greater, SpeedThreshold);
            SpeedTo(sWalk, sIdle, AnimatorConditionMode.Less, SpeedThreshold);
            SpeedTo(sWalk, sRun, AnimatorConditionMode.Greater, RunThreshold);
            SpeedTo(sRun, sWalk, AnimatorConditionMode.Less, RunThreshold);
            SpeedTo(sIdle, sRun, AnimatorConditionMode.Greater, RunThreshold);
            SpeedTo(sRun, sIdle, AnimatorConditionMode.Less, SpeedThreshold);

            void Any(AnimatorState to, string trigger)
            {
                var tr = sm.AddAnyStateTransition(to);
                tr.hasExitTime = false;
                tr.duration = 0.08f;
                tr.canTransitionToSelf = false;
                tr.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            }
            Any(sA1, "Attack1");
            Any(sA2, "Attack2");
            Any(sHit, "Hit");
            Any(sDie, "Die");
            foreach (var combat in new[] { sA1, sA2, sHit })
            {
                var back = combat.AddTransition(sIdle);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.12f;
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            log.AppendLine($"controller: Idle/Walk/Run on Speed, triggers Attack1 Attack2 Hit Die, Death has no exit");
            return ctrl;
        }

        // ------------------------------------------------------------------ prefab

        static void BuildPrefab(AnimatorController controller, StringBuilder log)
        {
            var avatar = LoadAvatar();
            var root = new GameObject("Chopper");
            try
            {
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var childAnimator = model.GetComponent<Animator>();
                if (childAnimator) UnityEngine.Object.DestroyImmediate(childAnimator);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                log.AppendLine($"model child '{model.name}' (unpacked; rebuilt by this script on every run)");

                var anim = root.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.avatar = avatar;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                float radius = FitRadius(model, log);
                var cap = root.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.height = CharacterHeight;
                cap.center = new Vector3(0f, CharacterHeight * 0.5f, 0f);
                cap.radius = radius;
                log.AppendLine($"capsule: height {cap.height}, center {cap.center}, radius {cap.radius:F3}");

                AttachProp(root, AxeFbx, WeaponSocket, log);
                AttachProp(root, ShieldFbx, ShieldSocket, log);
                SetRenderer(root, "chopper_hair", false);
                SetRenderer(root, "chopper_helmet", true);
                SetRenderer(root, "chopper_vest", true);
                SetRenderer(root, "chopper_plate", false);
                SetRenderer(root, "chopper_pauldrons", false);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success) throw new Exception("Saving prefab failed: " + PrefabPath);
                log.AppendLine("saved " + PrefabPath + " (Warrior: helmet on, hair off, leather vest, axe and shield)");

                SetRenderer(root, "chopper_vest", false);
                SetRenderer(root, "chopper_plate", true);
                SetRenderer(root, "chopper_pauldrons", true);
                PrefabUtility.SaveAsPrefabAsset(root, ElitePrefabPath, out bool eliteOk);
                if (!eliteOk) throw new Exception("Saving elite prefab failed: " + ElitePrefabPath);
                log.AppendLine("saved " + ElitePrefabPath + " (Elite: plate and pauldrons, helmet, axe and shield)");

                SetRenderer(root, "chopper_hair", true);
                SetRenderer(root, "chopper_helmet", false);
                SetRenderer(root, "chopper_vest", true);
                SetRenderer(root, "chopper_plate", false);
                SetRenderer(root, "chopper_pauldrons", false);
                foreach (var propName in new[] { "chopper_axe", "chopper_shield" })
                {
                    var prop = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == propName);
                    if (prop != null) UnityEngine.Object.DestroyImmediate(prop.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, WorkerPrefabPath, out bool workerOk);
                if (!workerOk) throw new Exception("Saving worker prefab failed: " + WorkerPrefabPath);
                log.AppendLine("saved " + WorkerPrefabPath + " (Worker: hair on, helmet off, no weapons)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static float FitRadius(GameObject model, StringBuilder log)
        {
            float maxR = 0f;
            int used = 0, skipped = 0;
            var mesh = new Mesh();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.gameObject.name == "chopper_plate" || smr.gameObject.name == "chopper_pauldrons")
                {
                    skipped += smr.sharedMesh.vertexCount;
                    continue;
                }
                var armIdx = new HashSet<int>();
                var headIdx = new HashSet<int>();
                for (int b = 0; b < smr.bones.Length; b++)
                {
                    if (smr.bones[b] == null) continue;
                    if (ArmBones.Contains(smr.bones[b].name)) armIdx.Add(b);
                    if (smr.bones[b].name == "Head") headIdx.Add(b);
                }
                var weights = smr.sharedMesh.boneWeights;
                smr.BakeMesh(mesh, true);
                var verts = mesh.vertices;
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int i = 0; i < verts.Length; i++)
                {
                    // Skip arm and head (beard, hair) vertices: the capsule fits torso and legs.
                    int dom = weights.Length > i ? weights[i].boneIndex0 : -1;
                    if (armIdx.Contains(dom) || headIdx.Contains(dom)) { skipped++; continue; }
                    Vector3 w = m.MultiplyPoint3x4(verts[i]);
                    maxR = Mathf.Max(maxR, new Vector2(w.x, w.z).magnitude);
                    used++;
                }
            }
            UnityEngine.Object.DestroyImmediate(mesh);
            float fitted = Mathf.Ceil(maxR * 100f) / 100f;
            const float r = 0.37f;
            log.AppendLine($"capsule fit: max radial distance of torso/leg vertices (rest pose, arms, head, plate and pauldrons excluded) {maxR:F3} m over {used} verts ({skipped} skipped), fitted {fitted:F2} -> radius kept at {r:F2}");
            return r;
        }

        static void SetRenderer(GameObject root, string objectName, bool enabled)
        {
            var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == objectName);
            if (t == null) throw new Exception("Module not found: " + objectName);
            var r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = enabled;
        }

        static void AttachProp(GameObject root, string fbxPath, string socketName, StringBuilder log)
        {
            var socket = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == socketName);
            if (socket == null) throw new Exception("Socket not found: " + socketName);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(asset, socket);
            var t = prop.transform;
            log.AppendLine($"prop {Path.GetFileName(fbxPath)} under {socketName}: imported root local pos {t.localPosition}, rot {t.localRotation.eulerAngles}, scale {t.localScale}, children {t.childCount}");
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            // If the mesh sits on a child node with its own transform, zero that too.
            foreach (Transform c in t)
            {
                if (c.GetComponent<MeshFilter>() == null) continue;
                log.AppendLine($"  child mesh node '{c.name}' local pos {c.localPosition}, rot {c.localRotation.eulerAngles}, scale {c.localScale} -> identity");
                c.localPosition = Vector3.zero;
                c.localRotation = Quaternion.identity;
                c.localScale = Vector3.one;
            }
            log.AppendLine($"  -> set to local position 0, rotation identity, scale 1");
        }

        // ------------------------------------------------------------------ scene

        static void PlaceInTestScene(StringBuilder log)
        {
            var scene = EditorSceneManager.OpenScene(ChopperProjectSetup.TestScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects().Where(g => g.name == "Chopper"))
                UnityEngine.Object.DestroyImmediate(go);
            var spawn = scene.GetRootGameObjects().First(g => g.name == "CharacterSpawn").transform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = "Chopper";
            inst.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var driver = inst.AddComponent<ChopperTestDriver>();
            driver.mode = ChopperTestDriver.Mode.Walk;
            driver.walkSpeed = 0.75f;
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"TestScene: Chopper.prefab placed at CharacterSpawn {spawn.position}, ChopperTestDriver added (mode Walk, 0.75 m/s)");
        }

        static void ImportPine(StringBuilder log)
        {
            string src = Path.Combine(ExportDir, "prop_pine_voxel.fbx");
            if (!File.Exists(src)) throw new FileNotFoundException("Missing export " + src);
            Directory.CreateDirectory("Assets/Game/Props");
            bool same = File.Exists(PineFbx) && File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(PineFbx));
            if (!same) File.Copy(src, PineFbx, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine($"copy {src} -> {PineFbx} ({(same ? "unchanged" : "copied")})");

            var mat = EnsureMaterial(PineMatPath, "M_Pine");
            mat.SetFloat("_UseAtlas", 0f);
            mat.SetTexture("_BaseMap", null);
            EditorUtility.SetDirty(mat);

            ConfigureProp(PineFbx, mat, log);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PineFbx);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            try
            {
                root.name = "prop_pine_voxel";
                PrefabUtility.SaveAsPrefabAsset(root, PinePrefabPath);
                log.AppendLine("saved " + PinePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var scene = EditorSceneManager.OpenScene(ChopperProjectSetup.TestScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects().Where(g => g.name == "prop_pine_voxel"))
                UnityEngine.Object.DestroyImmediate(go);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PinePrefabPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = "prop_pine_voxel";
            inst.transform.position = new Vector3(10f, 0f, 10f);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("TestScene: prop_pine_voxel at (10, 0, 10), behind the character review cameras");
        }

        static void ImportMeadow(StringBuilder log)
        {
            var mat = EnsureMaterial("Assets/Game/Props/M_World.mat", "M_World");
            mat.SetFloat("_UseAtlas", 0f);
            mat.SetTexture("_BaseMap", null);
            EditorUtility.SetDirty(mat);
            var spots = new (string file, Vector3 at)[]
            {
                ("prop_rock_voxel", new Vector3(6f, 0f, 8f)),
                ("prop_stump_voxel", new Vector3(8f, 0f, 8f)),
                ("prop_log_voxel", new Vector3(10.5f, 0f, 8f)),
            };
            var scene = EditorSceneManager.OpenScene(ChopperProjectSetup.TestScenePath, OpenSceneMode.Single);
            foreach (var (file, at) in spots)
            {
                string src = Path.Combine(ExportDir, file + ".fbx");
                if (!File.Exists(src)) throw new FileNotFoundException("Missing export " + src);
                string fbx = "Assets/Game/Props/" + file + ".fbx";
                string prefabPath = "Assets/Game/Props/" + file + ".prefab";
                Directory.CreateDirectory("Assets/Game/Props");
                bool same = File.Exists(fbx) && File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(fbx));
                if (!same) File.Copy(src, fbx, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                log.AppendLine($"copy {src} -> {fbx} ({(same ? "unchanged" : "copied")})");
                ConfigureProp(fbx, mat, log);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                try
                {
                    root.name = file;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    log.AppendLine("saved " + prefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                foreach (var go in scene.GetRootGameObjects().Where(g => g.name == file))
                    UnityEngine.Object.DestroyImmediate(go);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.name = file;
                inst.transform.position = at;
                log.AppendLine($"TestScene: {file} at {at}");
            }
            EditorSceneManager.SaveScene(scene);
        }
    }
}
