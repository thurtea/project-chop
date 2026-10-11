using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Chopper.EditorTools
{
    /// <summary>
    /// Batch verification of the Chopper import (W1-05). Run WITHOUT -nographics so the screenshots render:
    ///   Unity.exe -batchmode -quit -projectPath Chopper -executeMethod Chopper.EditorTools.ChopperCheck.RunChecks
    /// Writes Chopper/Art/Renders/unity_check_report.txt and Chopper/Art/Renders/unity_*.png.
    /// </summary>
    public static class ChopperCheck
    {
        const float Fps = 30f;
        const float WalkSpeed = 0.75f;
        const float FootMin = -0.01f, FootMax = 0.05f;
        const float SlipMaxPerFrame = 0.005f;
        const float ContactHeight = 0.003f;
        const float InteriorScale = 0.75f;      // "interior" of the body capsule = 75 % of its radius
        const float MaxPropInteriorFraction = 0.02f;

        static readonly StringBuilder Report = new StringBuilder();
        static int pass, fail;

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RendersDir => Path.Combine(ProjectRoot, "Art", "Renders");

        static void Line(string s) => Report.AppendLine(s);

        static bool Check(bool ok, string what)
        {
            if (ok) pass++; else fail++;
            Report.AppendLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
            return ok;
        }

        public static void RunChecks()
        {
            Report.Clear();
            pass = fail = 0;
            Line("Chopper Unity check report (W1-05)");
            Line("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " by ChopperCheck.RunChecks, Unity " + Application.unityVersion);
            Line(new string('=', 78));
            try
            {
                CheckAvatar();
                CheckClipsAndController();
                CheckPrefab();
                CheckMeshClip();
                CheckSceneAndAnimation();
                CheckPlayMode();
            }
            catch (Exception e)
            {
                Check(false, "check run threw " + e.GetType().Name + ": " + e.Message);
                Debug.LogException(e);
            }
            Line("");
            Line(new string('=', 78));
            Line($"SUMMARY: {pass} PASS, {fail} FAIL");

            Directory.CreateDirectory(RendersDir);
            string path = Path.Combine(RendersDir, "unity_check_report.txt");
            File.WriteAllText(path, Report.ToString());
            Debug.Log("[ChopperCheck] report written to " + path + "\n" + Report);
            Debug.Log($"[ChopperCheck] SUMMARY: {pass} PASS, {fail} FAIL");
            if (Application.isBatchMode) EditorApplication.Exit(fail == 0 ? 0 : 1);
        }

        static void CheckMeshClip()
        {
            Line("");
            Line("MESH CLIP (shield vertices against the body mesh, not the capsule)");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChopperImport.PrefabPath);
            if (prefab == null) { Check(false, "prefab for mesh clip"); return; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                int inside = CountShieldInsideBody(inst, Vector3.zero);
                Check(inside == 0, $"shield vertices inside the body mesh: {inside} (expected 0)");
                int sunk = CountShieldInsideBody(inst, new Vector3(0f, 0f, -0.08f));
                Check(sunk > 20, $"detector self-test: shield pushed 8 cm into the arm -> {sunk} inside verts (expected > 20)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        static int CountShieldInsideBody(GameObject root, Vector3 shieldNudge)
        {
            var shield = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "chopper_shield");
            var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == "chopper_body");
            if (shield == null || body == null) return -1;
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            var tris = baked.triangles;
            var verts = baked.vertices;
            var world = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) world[i] = body.transform.TransformPoint(verts[i]);
            var filter = shield.GetComponentInChildren<MeshFilter>();
            if (filter == null) { UnityEngine.Object.DestroyImmediate(baked); return -1; }
            var sv = filter.sharedMesh.vertices;
            int inside = 0;
            foreach (var v in sv)
            {
                Vector3 p = filter.transform.TransformPoint(v) + shieldNudge;
                if (PointInside(p, world, tris)) inside++;
            }
            UnityEngine.Object.DestroyImmediate(baked);
            return inside;
        }

        static bool PointInside(Vector3 p, Vector3[] world, int[] tris)
        {
            Vector3 dir = new Vector3(0.31f, 0.17f, 0.93f).normalized;
            int hits = 0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (RayHitsTri(p, dir, world[tris[i]], world[tris[i + 1]], world[tris[i + 2]])) hits++;
            }
            return (hits % 2) == 1;
        }

        static bool RayHitsTri(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a;
            Vector3 pvec = Vector3.Cross(dir, ac);
            float det = Vector3.Dot(ab, pvec);
            if (Mathf.Abs(det) < 1e-8f) return false;
            float inv = 1f / det;
            Vector3 tvec = origin - a;
            float u = Vector3.Dot(tvec, pvec) * inv;
            if (u < 0f || u > 1f) return false;
            Vector3 qvec = Vector3.Cross(tvec, ab);
            float v = Vector3.Dot(dir, qvec) * inv;
            if (v < 0f || u + v > 1f) return false;
            float t = Vector3.Dot(ac, qvec) * inv;
            return t > 1e-4f;
        }

        static void CheckPlayMode()
        {
            Line("");
            Line("PLAY-MODE STEP (ChopperTestDriver.Step + Animator.Update, the same calls Play mode makes)");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/TestScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Chopper");
            if (!Check(go != null, "Chopper in TestScene")) return;
            var anim = go.GetComponent<Animator>();
            var driver = go.GetComponent<ChopperTestDriver>();
            if (!Check(anim != null && driver != null, "Animator and ChopperTestDriver on the scene Chopper")) return;
            anim.Update(0f);
            bool sawWalk = false;
            driver.mode = ChopperTestDriver.Mode.Walk;
            for (int i = 0; i < 20; i++)
            {
                driver.Step(1f / 30f);
                anim.Update(1f / 30f);
                if (anim.GetCurrentAnimatorStateInfo(0).IsName("Walk") && !anim.IsInTransition(0)) sawWalk = true;
            }
            Check(sawWalk, "after 20 driver steps at walk speed the Animator is in Walk");
            driver.mode = ChopperTestDriver.Mode.Idle;
            bool sawIdle = false;
            for (int i = 0; i < 20; i++)
            {
                driver.Step(1f / 30f);
                anim.Update(1f / 30f);
                if (anim.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !anim.IsInTransition(0)) sawIdle = true;
            }
            Check(sawIdle, "after switching the driver to Idle the Animator returns to Idle");
        }

        // ------------------------------------------------------------------ avatar

        static void CheckAvatar()
        {
            Line("");
            Line("AVATAR (" + ChopperImport.CharFbx + ")");
            var imp = (ModelImporter)AssetImporter.GetAtPath(ChopperImport.CharFbx);
            Check(imp.animationType == ModelImporterAnimationType.Human, $"animation type {imp.animationType} (expected Human)");
            Check(imp.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel, $"avatar setup {imp.avatarSetup} (expected CreateFromThisModel)");
            var avatar = ChopperImport.LoadAvatar();
            if (!Check(avatar != null, "avatar sub-asset present" + (avatar ? $" ({avatar.name})" : ""))) return;
            Check(avatar.isValid, "avatar.isValid = " + avatar.isValid);
            Check(avatar.isHuman, "avatar.isHuman = " + avatar.isHuman);
            var hd = avatar.humanDescription;
            Line($"  mapped bones ({hd.human.Length}), Unity humanoid name -> our bone:");
            foreach (var hb in hd.human)
                Line($"    {hb.humanName,-14} -> {hb.boneName}");
            var missing = new List<string>();
            for (int i = 0; i < HumanTrait.BoneCount; i++)
                if (HumanTrait.RequiredBone(i) && hd.human.All(h => h.humanName != HumanTrait.BoneName[i]))
                    missing.Add(HumanTrait.BoneName[i]);
            Check(missing.Count == 0, $"all {HumanTrait.RequiredBoneCount} required humanoid bones mapped (missing: {(missing.Count == 0 ? "none" : string.Join(", ", missing))})");
            var wrong = hd.human.Where(h => h.humanName.Replace(" ", "") != h.boneName).Select(h => h.humanName + "->" + h.boneName).ToList();
            Check(wrong.Count == 0, $"every mapping is name-identical (mismatches: {(wrong.Count == 0 ? "none" : string.Join(", ", wrong))})");
            Check(hd.human.Length == 22, $"{hd.human.Length} bones mapped (expected the 22 rig bones)");
        }

        // ------------------------------------------------------------------ clips and controller

        static void CheckClipsAndController()
        {
            Line("");
            Line("CLIPS AND CONTROLLER");
            foreach (var (name, expected, loop) in new (string, float, bool)[]
            {
                ("Idle", 2f, true), ("Walk", 1f, true), ("Run", 20f / 30f, true),
                ("Attack_1", 0.8f, false), ("Attack_2", 0.8f, false), ("HitReact", 0.5f, false), ("Death", 1.2f, false)
            })
            {
                var clip = ChopperImport.LoadClip(name);
                if (!Check(clip != null, $"clip {name} present")) continue;
                Check(Mathf.Abs(clip.length - expected) < 0.05f, $"clip {name} length {clip.length:F3} s (expected {expected:F2} s), frameRate {clip.frameRate}");
                Check(clip.isLooping == loop, $"clip {name} loop expected {loop} (isLooping={clip.isLooping})");
                Check(clip.isHumanMotion, $"clip {name} is humanoid motion");
                Check(clip.averageSpeed.magnitude < 0.05f, $"clip {name} root motion in place (averageSpeed {clip.averageSpeed.magnitude:F4} m/s)");
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ChopperImport.ControllerPath);
            if (!Check(ctrl != null, "controller " + ChopperImport.ControllerPath)) return;
            Check(ctrl.parameters.Any(p => p.name == "Speed" && p.type == AnimatorControllerParameterType.Float), "Speed float parameter");
            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
            var walk = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Walk");
            Check(idle != null && walk != null && idle.motion && idle.motion.name == "Idle" && walk.motion && walk.motion.name == "Walk",
                "states Idle and Walk with matching clips");
            Check(sm.defaultState == idle, "default state Idle");
            var tw = idle?.transitions.FirstOrDefault(t => t.destinationState == walk);
            var ti = walk?.transitions.FirstOrDefault(t => t.destinationState == idle);
            Check(tw != null && tw.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Greater && Mathf.Approximately(c.threshold, 0.1f)) && !tw.hasExitTime && tw.duration <= 0.25f,
                $"Idle -> Walk when Speed > 0.1, no exit time, duration {tw?.duration:F2} s");
            Check(ti != null && ti.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Less && Mathf.Approximately(c.threshold, 0.1f)) && !ti.hasExitTime && ti.duration <= 0.25f,
                $"Walk -> Idle when Speed < 0.1, no exit time, duration {ti?.duration:F2} s");
            foreach (var stateName in new[] { "Run", "Attack_1", "Attack_2", "HitReact", "Death" })
            {
                var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
                Check(st != null && st.motion != null && st.motion.name == stateName, $"state {stateName} with matching clip");
            }
            Check(ctrl.parameters.Any(p => p.name == "Attack1" && p.type == AnimatorControllerParameterType.Trigger), "Attack1 trigger");
            Check(ctrl.parameters.Any(p => p.name == "Die" && p.type == AnimatorControllerParameterType.Trigger), "Die trigger");
        }

        // ------------------------------------------------------------------ prefab

        static void CheckPrefab()
        {
            Line("");
            Line("PREFAB (" + ChopperImport.PrefabPath + ")");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChopperImport.PrefabPath);
            if (!Check(prefab != null, "prefab exists")) return;
            var anim = prefab.GetComponent<Animator>();
            Check(anim != null, "root has Animator");
            if (anim)
            {
                Check(anim.runtimeAnimatorController != null && AssetDatabase.GetAssetPath(anim.runtimeAnimatorController) == ChopperImport.ControllerPath, "Animator controller = Chopper.controller");
                Check(anim.avatar != null && anim.avatar == ChopperImport.LoadAvatar(), "Animator avatar = chopper.fbx avatar");
                Check(!anim.applyRootMotion, "Apply Root Motion off");
            }
            Check(prefab.GetComponentsInChildren<Animator>(true).Length == 1, "exactly one Animator in the prefab");
            var cap = prefab.GetComponent<CapsuleCollider>();
            if (Check(cap != null, "root has CapsuleCollider"))
                Check(cap.direction == 1 && Mathf.Abs(cap.height - 1.8f) < 0.05f && Mathf.Abs(cap.center.y - 0.9f) < 0.05f && cap.radius > 0.15f && cap.radius < 0.45f,
                    $"capsule height {cap.height:F2}, center {cap.center}, radius {cap.radius:F2} (Y axis)");
            CheckPropInPrefab(prefab, "chopper_axe", ChopperImport.WeaponSocket);
            CheckPropInPrefab(prefab, "chopper_shield", ChopperImport.ShieldSocket);
            int smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            Check(smrs == 12, $"{smrs} skinned meshes (expected 12, including helmet, plate and pauldrons)");
            var mats = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToList();
            Check(mats.Count <= 2 && mats.All(m => m && m.shader && m.shader.name == ChopperImport.ShaderName),
                $"materials used: {string.Join(", ", mats.Select(m => m ? m.name + " (" + m.shader.name + ")" : "<null>"))}");
        }

        static void CheckPropInPrefab(GameObject prefab, string propName, string socket)
        {
            var t = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == propName);
            if (!Check(t != null, $"{propName} present")) return;
            Check(t.parent && t.parent.name == socket, $"{propName} parent = {(t.parent ? t.parent.name : "<none>")} (expected {socket})");
            Check(t.localPosition.sqrMagnitude < 1e-8f && Quaternion.Angle(t.localRotation, Quaternion.identity) < 0.01f && (t.localScale - Vector3.one).sqrMagnitude < 1e-8f,
                $"{propName} local transform identity (pos {t.localPosition}, rot {t.localRotation.eulerAngles}, scale {t.localScale}); correction: FBX root node came in with rotation (-90,0,0), reset to identity by ChopperImport.AttachProp");
            Check(t.GetComponent<MeshRenderer>() != null && t.GetComponent<MeshFilter>()?.sharedMesh != null, $"{propName} has its mesh on the root node");
        }

        // ------------------------------------------------------------------ scene, sampling, screenshots

        class Rig
        {
            public GameObject root;
            public Animator anim;
            public CapsuleCollider capsule;
            public SkinnedMeshRenderer[] smrs;
            public SkinnedMeshRenderer boots, body;
            public Transform leftFoot, rightFoot, weaponR, shieldL, axe, shield;
            public MeshFilter axeMf, shieldMf;
            public Mesh scratch = new Mesh();
        }

        static Transform Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static void CheckSceneAndAnimation()
        {
            Line("");
            Line("TEST SCENE (" + ChopperProjectSetup.TestScenePath + ")");
            var scene = EditorSceneManager.OpenScene(ChopperProjectSetup.TestScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var go = roots.FirstOrDefault(g => g.name == "Chopper");
            var spawn = roots.FirstOrDefault(g => g.name == "CharacterSpawn");
            if (!Check(go != null, "Chopper instance in TestScene")) return;
            Check(PrefabUtility.GetCorrespondingObjectFromSource(go) == AssetDatabase.LoadAssetAtPath<GameObject>(ChopperImport.PrefabPath), "instance of Chopper.prefab");
            Check(spawn != null && (go.transform.position - spawn.transform.position).sqrMagnitude < 1e-6f, $"placed at CharacterSpawn {go.transform.position}");
            var driver = go.GetComponent<ChopperTestDriver>();
            Check(driver != null && Mathf.Approximately(driver.walkSpeed, 0.75f), $"ChopperTestDriver present (mode {driver?.mode}, walkSpeed {driver?.walkSpeed})");

            var rig = new Rig
            {
                root = go,
                anim = go.GetComponent<Animator>(),
                capsule = go.GetComponent<CapsuleCollider>(),
                smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                leftFoot = Find(go, "LeftFoot"),
                rightFoot = Find(go, "RightFoot"),
                weaponR = Find(go, "Weapon_R"),
                shieldL = Find(go, "Shield_L"),
                axe = Find(go, "chopper_axe"),
                shield = Find(go, "chopper_shield"),
            };
            rig.boots = rig.smrs.First(s => s.name == "chopper_boots");
            rig.body = rig.smrs.First(s => s.name == "chopper_body");
            rig.axeMf = rig.axe.GetComponent<MeshFilter>();
            rig.shieldMf = rig.shield.GetComponent<MeshFilter>();

            CheckStateMachine(rig);
            CheckIdle(rig);
            CheckWalk(rig);
            Screenshots(rig, scene);
        }

        static void CheckStateMachine(Rig rig)
        {
            Line("");
            Line("ANIMATOR STATE MACHINE (edit-mode Animator.Update)");
            var a = rig.anim;
            a.Rebind();
            a.SetFloat("Speed", 0f);
            for (int i = 0; i < 5; i++) a.Update(0.1f);
            bool idle0 = a.GetCurrentAnimatorStateInfo(0).IsName("Idle");
            a.SetFloat("Speed", WalkSpeed);
            for (int i = 0; i < 5; i++) a.Update(0.1f);
            bool walk1 = a.GetCurrentAnimatorStateInfo(0).IsName("Walk") && !a.IsInTransition(0);
            a.SetFloat("Speed", 0.05f);
            for (int i = 0; i < 5; i++) a.Update(0.1f);
            bool idle2 = a.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !a.IsInTransition(0);
            Check(a.isInitialized && a.isHuman, $"Animator initialized={a.isInitialized}, human={a.isHuman}");
            Check(idle0 && walk1 && idle2, $"Speed 0 -> Idle ({idle0}); Speed 0.75 -> Walk ({walk1}); Speed 0.05 -> Idle ({idle2})");
        }

        static void Pose(Rig rig, string state, float time, float rootZ)
        {
            rig.root.transform.SetPositionAndRotation(new Vector3(0f, 0f, rootZ), Quaternion.identity);
            var clip = ChopperImport.LoadClip(state);
            rig.anim.SetFloat("Speed", state == "Walk" ? WalkSpeed : 0f);
            rig.anim.Play(state, 0, (time / clip.length) % 1f);
            rig.anim.Update(0f);
        }

        struct Sample
        {
            public float leftMin, rightMin, allMin;
            public Vector3 leftFoot, rightFoot;
            public bool weaponInHand;
            public Bounds handBox;
            public float axeInterior, shieldInterior;
            public float shieldMinAxisDist;
            public Bounds axeBounds, shieldBounds;
            public Vector3[] bootVerts;
            public int[] bootSide;   // -1 left, +1 right, 0 other
        }

        static IEnumerable<Vector3> BakedWorld(Rig rig, SkinnedMeshRenderer smr)
        {
            smr.BakeMesh(rig.scratch, true);
            var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            return rig.scratch.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
        }

        static Sample Measure(Rig rig)
        {
            var s = new Sample { leftMin = float.MaxValue, rightMin = float.MaxValue, allMin = float.MaxValue };
            // Boots: split soles by the dominant bone side.
            var bw = rig.boots.sharedMesh.boneWeights;
            var bootVerts = BakedWorld(rig, rig.boots).ToArray();
            s.bootVerts = bootVerts;
            s.bootSide = new int[bootVerts.Length];
            for (int i = 0; i < bootVerts.Length; i++)
            {
                string bone = rig.boots.bones[bw[i].boneIndex0].name;
                if (bone.StartsWith("Left")) { s.leftMin = Mathf.Min(s.leftMin, bootVerts[i].y); s.bootSide[i] = -1; }
                else if (bone.StartsWith("Right")) { s.rightMin = Mathf.Min(s.rightMin, bootVerts[i].y); s.bootSide[i] = 1; }
            }
            foreach (var smr in rig.smrs)
                foreach (var v in BakedWorld(rig, smr)) s.allMin = Mathf.Min(s.allMin, v.y);
            s.leftFoot = rig.leftFoot.position;
            s.rightFoot = rig.rightFoot.position;

            // Right-hand area: body vertices dominated by RightHand.
            var w = rig.body.sharedMesh.boneWeights;
            var bodyVerts = BakedWorld(rig, rig.body).ToArray();
            int handIdx = Array.FindIndex(rig.body.bones, b => b && b.name == "RightHand");
            bool first = true;
            for (int i = 0; i < bodyVerts.Length; i++)
            {
                if (w[i].boneIndex0 != handIdx || w[i].weight0 < 0.5f) continue;
                if (first) { s.handBox = new Bounds(bodyVerts[i], Vector3.zero); first = false; }
                else s.handBox.Encapsulate(bodyVerts[i]);
            }
            s.handBox.Expand(0.02f);
            s.weaponInHand = !first && s.handBox.Contains(rig.weaponR.position);

            s.axeInterior = InteriorFraction(rig, rig.axeMf, out s.axeBounds, out _);
            s.shieldInterior = InteriorFraction(rig, rig.shieldMf, out s.shieldBounds, out s.shieldMinAxisDist);
            return s;
        }

        static float InteriorFraction(Rig rig, MeshFilter mf, out Bounds bounds, out float minAxisDist)
        {
            var cap = rig.capsule;
            var t = rig.root.transform;
            float rIn = cap.radius * InteriorScale;
            Vector3 a = t.TransformPoint(cap.center + Vector3.down * (cap.height * 0.5f - cap.radius));
            Vector3 b = t.TransformPoint(cap.center + Vector3.up * (cap.height * 0.5f - cap.radius));
            var verts = mf.sharedMesh.vertices;
            var m = mf.transform.localToWorldMatrix;
            int inside = 0;
            minAxisDist = float.MaxValue;
            bounds = new Bounds(m.MultiplyPoint3x4(verts[0]), Vector3.zero);
            foreach (var v in verts)
            {
                var p = m.MultiplyPoint3x4(v);
                bounds.Encapsulate(p);
                var ab = b - a;
                float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = Vector3.Distance(p, a + ab * k);
                minAxisDist = Mathf.Min(minAxisDist, d);
                if (d < rIn) inside++;
            }
            return verts.Length == 0 ? 0f : inside / (float)verts.Length;
        }

        static string Fmt(Bounds b) => $"min ({b.min.x:F3}, {b.min.y:F3}, {b.min.z:F3}) max ({b.max.x:F3}, {b.max.y:F3}, {b.max.z:F3})";

        static void CheckIdle(Rig rig)
        {
            Line("");
            Line($"IDLE SAMPLES (root fixed at origin; capsule radius {rig.capsule.radius:F2}, interior = {InteriorScale:P0} of radius)");
            Line("   time   L sole   R sole   lowest  Weapon_R in hand  axe interior  shield interior  shield min dist to body axis");
            bool feetOk = true, handOk = true, axeOk = true, shieldOk = true;
            foreach (float time in new[] { 0f, 0.5f, 1f, 1.5f, 1.9f })
            {
                Pose(rig, "Idle", time, 0f);
                var s = Measure(rig);
                float low = Mathf.Min(s.leftMin, s.rightMin);
                feetOk &= low >= FootMin && low <= FootMax && s.allMin >= FootMin;
                handOk &= s.weaponInHand;
                axeOk &= s.axeInterior <= MaxPropInteriorFraction;
                shieldOk &= s.shieldInterior <= MaxPropInteriorFraction;
                Line($"  {time,5:F2}  {s.leftMin,7:F4}  {s.rightMin,7:F4}  {s.allMin,7:F4}  {s.weaponInHand,-16}  {s.axeInterior,11:P1}  {s.shieldInterior,14:P1}  {s.shieldMinAxisDist,8:F3} m");
                if (time == 0f)
                {
                    Line($"         Weapon_R {rig.weaponR.position.ToString("F3")} hand box {Fmt(s.handBox)}");
                    Line($"         axe bounds {Fmt(s.axeBounds)}");
                    Line($"         shield bounds {Fmt(s.shieldBounds)}; Shield_L {rig.shieldL.position.ToString("F3")}");
                }
            }
            Check(feetOk, $"Idle: lowest sole within {FootMin}..{FootMax} m on every sample, no vertex below {FootMin} m");
            Check(handOk, "Idle: Weapon_R inside the right-hand vertex box (2 cm margin) on every sample");
            Check(axeOk, $"Idle: axe vertices inside body capsule interior <= {MaxPropInteriorFraction:P0} on every sample");
            Check(shieldOk, $"Idle: shield vertices inside body capsule interior <= {MaxPropInteriorFraction:P0} on every sample");
        }

        static void CheckWalk(Rig rig)
        {
            Line("");
            Line($"WALK SAMPLES (every frame at {Fps} fps, root moved forward +Z at {WalkSpeed} m/s)");
            Line($"  slip = horizontal world movement of the same sole vertex between consecutive frames, for vertices within {ContactHeight * 1000f:F0} mm of the ground in both frames (max per foot); ankle = foot bone horizontal movement of the lower foot (information only, includes heel/toe roll)");
            Line("  frame  root z   L sole   R sole   lowest  L contact slip  R contact slip  ankle(lower)  in hand  axe int  shield int");
            bool feetOk = true, handOk = true, axeOk = true, shieldOk = true;
            float maxSlip = 0f, sumSlip = 0f, maxAnkle = 0f;
            int slipN = 0;
            Sample prev = default;
            for (int f = 0; f <= 30; f++)
            {
                float rootZ = WalkSpeed * f / Fps;
                Pose(rig, "Walk", f / Fps, rootZ);
                var s = Measure(rig);
                float low = Mathf.Min(s.leftMin, s.rightMin);
                feetOk &= low >= FootMin && low <= FootMax && s.allMin >= FootMin;
                handOk &= s.weaponInHand;
                axeOk &= s.axeInterior <= MaxPropInteriorFraction;
                shieldOk &= s.shieldInterior <= MaxPropInteriorFraction;
                string[] slipStr = { "-", "-" };
                string ankleStr = "-";
                if (f > 0)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        int sideId = side == 0 ? -1 : 1;
                        float m = -1f;
                        for (int i = 0; i < s.bootVerts.Length; i++)
                        {
                            if (s.bootSide[i] != sideId) continue;
                            if (s.bootVerts[i].y > ContactHeight || prev.bootVerts[i].y > ContactHeight) continue;
                            var d = s.bootVerts[i] - prev.bootVerts[i];
                            m = Mathf.Max(m, new Vector2(d.x, d.z).magnitude);
                        }
                        if (m >= 0f)
                        {
                            slipStr[side] = m.ToString("F4");
                            maxSlip = Mathf.Max(maxSlip, m);
                            sumSlip += m;
                            slipN++;
                        }
                    }
                    bool leftLower = s.leftMin <= s.rightMin;
                    var a = leftLower ? s.leftFoot - prev.leftFoot : s.rightFoot - prev.rightFoot;
                    float ankle = new Vector2(a.x, a.z).magnitude;
                    maxAnkle = Mathf.Max(maxAnkle, ankle);
                    ankleStr = ankle.ToString("F4");
                }
                prev = s;
                Line($"  {f,5}  {rootZ,6:F3}  {s.leftMin,7:F4}  {s.rightMin,7:F4}  {s.allMin,7:F4}  {slipStr[0],14}  {slipStr[1],14}  {ankleStr,12}  {s.weaponInHand,-7}  {s.axeInterior,7:P1}  {s.shieldInterior,9:P1}");
            }
            Check(feetOk, $"Walk: lowest sole within {FootMin}..{FootMax} m on every frame, no vertex below {FootMin} m");
            Check(slipN > 0 && maxSlip <= SlipMaxPerFrame, $"Walk: planted sole slip max {maxSlip * 1000f:F2} mm/frame, mean {(slipN > 0 ? sumSlip / slipN * 1000f : 0f):F2} mm/frame over {slipN} foot-frames in contact (limit {SlipMaxPerFrame * 1000f:F0} mm/frame; the ground moves 25 mm/frame, so sliding feet would show ~25 mm)");
            Line($"  (info) max lower-foot ankle movement {maxAnkle * 1000f:F1} mm/frame (heel/toe roll and double-support switches, not slip)");
            Check(handOk, "Walk: Weapon_R inside the right-hand vertex box on every frame");
            Check(axeOk, $"Walk: axe vertices inside body capsule interior <= {MaxPropInteriorFraction:P0} on every frame");
            Check(shieldOk, $"Walk: shield vertices inside body capsule interior <= {MaxPropInteriorFraction:P0} on every frame");
        }

        // ------------------------------------------------------------------ screenshots

        static void Screenshots(Rig rig, UnityEngine.SceneManagement.Scene scene)
        {
            Line("");
            Line("SCREENSHOTS (TestScene camera rendered to a RenderTexture)");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Check(false, "screenshots need a graphics device (run without -nographics)");
                return;
            }
            var cam = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            if (!Check(cam != null, "TestScene camera found")) return;
            DynamicGI.UpdateEnvironment();

            var shots = new (string file, string state, float time, Vector3 camPos, Vector3 look)[]
            {
                ("unity_idle_front.png", "Idle", 0.5f, new Vector3(0f, 1.15f, 3.4f), new Vector3(0f, 0.95f, 0f)),
                ("unity_idle_three_quarter.png", "Idle", 0.5f, new Vector3(2.4f, 1.35f, 2.4f), new Vector3(0f, 0.9f, 0f)),
                ("unity_idle_three_quarter_left.png", "Idle", 0.5f, new Vector3(-2.4f, 1.35f, 2.4f), new Vector3(0f, 0.9f, 0f)),
                ("unity_walk_contact_side.png", "Walk", 0f, new Vector3(3.4f, 1.0f, 0f), new Vector3(0f, 0.9f, 0f)),
                ("unity_walk_passing_side.png", "Walk", 7.5f / Fps, new Vector3(3.4f, 1.0f, 0f), new Vector3(0f, 0.9f, 0f)),
            };
            var camT = cam.transform;
            var oldPos = camT.position;
            var oldRot = camT.rotation;
            foreach (var shot in shots)
            {
                Pose(rig, shot.state, shot.time, 0f);
                var proxies = BakeProxies(rig);
                camT.position = shot.camPos;
                camT.LookAt(shot.look);
                string path = Path.Combine(RendersDir, shot.file);
                bool ok = RenderToPng(cam, path, 1280, 960, out string how);
                foreach (var p in proxies) UnityEngine.Object.DestroyImmediate(p);
                foreach (var smr in rig.smrs) smr.enabled = true;
                Check(ok, $"{shot.file}: {shot.state} t={shot.time:F2}s, camera {shot.camPos} -> {shot.look} ({how})");
            }
            camT.SetPositionAndRotation(oldPos, oldRot);
        }

        // Replace skinned meshes by baked static copies so the rendered pose is exactly the sampled pose.
        static List<GameObject> BakeProxies(Rig rig)
        {
            var list = new List<GameObject>();
            foreach (var smr in rig.smrs)
            {
                var mesh = new Mesh { name = smr.name + "_baked" };
                smr.BakeMesh(mesh, true);
                var go = new GameObject(smr.name + "_proxy");
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                smr.enabled = false;
                list.Add(go);
            }
            return list;
        }

        static bool RenderToPng(Camera cam, string path, int w, int h, out string how)
        {
            var desc = new RenderTextureDescriptor(w, h, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, 24) { msaaSamples = 1 };
            var rt = new RenderTexture(desc);
            rt.Create();
            float oldAspect = cam.aspect;
            cam.aspect = w / (float)h;
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req))
            {
                RenderPipeline.SubmitRenderRequest(cam, req);
                how = "SubmitRenderRequest";
            }
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                how = "Camera.Render";
            }
            cam.aspect = oldAspect;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            int distinct = px.Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            how += $", {distinct} distinct colours";
            return distinct > 50;
        }
    }
}
