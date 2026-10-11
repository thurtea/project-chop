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
        public const string ShaderName = "Chopper/VertexColorLit";
        public const string WeaponSocket = "Weapon_R";
        public const string ShieldSocket = "Shield_L";
        public const float CharacterHeight = 1.8f;
        public const float SpeedThreshold = 0.1f;

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
            foreach (var dest in new[] { CharFbx, AxeFbx, ShieldFbx })
            {
                string src = Path.Combine(ExportDir, Path.GetFileName(dest));
                if (!File.Exists(src)) throw new FileNotFoundException("Missing export " + src);
                bool same = File.Exists(dest) && File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dest));
                if (!same) File.Copy(src, dest, true);
                log.AppendLine($"copy {src} -> {dest} ({(same ? "unchanged" : "copied")}, {new FileInfo(src).Length} bytes)");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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
            imp.isReadable = false;
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

        static ModelImporterClipAnimation[] BuildClips(ModelImporter imp, StringBuilder log)
        {
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var c in imp.defaultClipAnimations)
            {
                string name = c.takeName.Contains("Idle") ? "Idle" : c.takeName.Contains("Walk") ? "Walk" : null;
                if (name == null)
                {
                    log.AppendLine("  skipping unexpected take " + c.takeName);
                    continue;
                }
                c.name = name;
                c.loopTime = true;
                c.loopPose = true;          // first and last frames are identical in the export
                c.cycleOffset = 0f;
                c.lockRootRotation = true;  // Root Transform Rotation: bake into pose, original
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;   // Root Transform Position (Y): bake into pose, original
                c.keepOriginalPositionY = true;
                c.heightFromFeet = false;
                c.lockRootPositionXZ = true; // Root Transform Position (XZ): bake into pose, original (walk is in place)
                c.keepOriginalPositionXZ = true;
                c.mirror = false;
                clips.Add(c);
                log.AppendLine($"  clip {name}: take '{c.takeName}' frames {c.firstFrame}-{c.lastFrame}, loopTime, loopPose, root motion baked in place");
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
            if (idle == null || walk == null) throw new Exception("Idle or Walk clip missing in " + CharFbx);

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                       ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var p in ctrl.parameters.ToArray()) ctrl.RemoveParameter(p);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var sm = ctrl.layers[0].stateMachine;
            foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
            foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);

            var sIdle = sm.AddState("Idle", new Vector3(300, 0, 0));
            sIdle.motion = idle;
            sIdle.iKOnFeet = true;
            var sWalk = sm.AddState("Walk", new Vector3(300, 120, 0));
            sWalk.motion = walk;
            sWalk.iKOnFeet = true;
            sm.defaultState = sIdle;

            var toWalk = sIdle.AddTransition(sWalk);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, SpeedThreshold, "Speed");
            var toIdle = sWalk.AddTransition(sIdle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, SpeedThreshold, "Speed");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            log.AppendLine($"controller {ControllerPath}: states Idle (default) / Walk, Speed > {SpeedThreshold} -> Walk, < {SpeedThreshold} -> Idle, 0.15 s transitions, Foot IK on");
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

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success) throw new Exception("Saving prefab failed: " + PrefabPath);
                log.AppendLine("saved " + PrefabPath);
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
            float r = Mathf.Clamp(Mathf.Ceil(maxR * 100f) / 100f, 0.15f, 0.4f);
            log.AppendLine($"capsule fit: max radial distance of torso/leg vertices (rest pose, arms and head excluded) {maxR:F3} m over {used} verts ({skipped} skipped) -> radius {r:F2}");
            return r;
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
    }
}
