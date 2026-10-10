using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chopper.Player;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Builds the fixed Chopper look from the Quaternius Universal Base Characters and Modular Character Outfits packs
    // (both CC0): the whole body with one skin (only the skin the clothes truly cover is taken out, so nothing is see
    // through at the collar, cuffs or hems), the peasant shirt and trousers baked as a plaid shirt, belt and jeans,
    // the ranger's long sleeves in the same plaid, the ranger's boots, short dark hair parted in the middle and a full
    // beard. Called from CharacterSetup when it makes the player model.
    public static class OutfitSetup
    {
        private const string BaseFolder = "Assets/ThirdParty/Quaternius_UniversalBaseCharacters";
        private const string OutfitFolder = "Assets/ThirdParty/Quaternius_ModularOutfits";
        private const string MaterialFolder = "Assets/_Chopper/Materials/Character";
        private const string CatalogPath = "Assets/_Chopper/Resources/CharacterCatalog.asset";
        private const string HeadMeshPath = "Assets/_Chopper/Prefabs/Player/Mesh_Head.asset";
        private const string BodyMeshFolder = "Assets/_Chopper/Prefabs/Player/Bodies";
        private const string PreviewPath = "Assets/_Chopper/Prefabs/Player/CharacterPreview.prefab";
        private const string SleevesMeshPath = "Assets/_Chopper/Prefabs/Player/Mesh_Sleeves.asset";
        // Body skin is taken out under clothes found along its normal from this far inside to this far outside it.
        // The pack's clothes were made for its slimmer Regular body: on the broader Superhero body the shirt sits up
        // to several centimeters inside the back and shoulders, where the skin would show through it.
        private const float CoverInside = 0.08f;
        private const float CoverOutside = 0.07f;
        // Meters to the side for the extra cover test lines (see UncoveredBodyMesh).
        private const float SeamJitter = 0.006f;
        // The grey hair texture is about this bright, so hair tints are divided by it to land on the wanted color.
        private const float HairTextureBrightness = 0.56f;

        private class Materials
        {
            public Material Peasant, Peasant2, Ranger, Ranger3, Hands, Head, Hair1, Hair2, Eyes, Gear;
        }

        public static CharacterCatalog BuildCatalog()
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>($"{BaseFolder}/Bodies/Superhero_Male_FullBody.fbx");
            var peasantBody = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutfitFolder}/Parts/Male_Peasant_Body.fbx");
            if (body == null || peasantBody == null)
            {
                Debug.LogWarning("[OutfitSetup] Outfit packs are missing; the player keeps the plain mannequin.");
                return null;
            }

            EnsureFolder(MaterialFolder);
            Materials m = MakeMaterials();

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            bool isNew = catalog == null;
            if (isNew) catalog = ScriptableObject.CreateInstance<CharacterCatalog>();

            EnsureFolder(BodyMeshFolder);
            SkinnedMeshRenderer bodySkin = body.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.name != "Eyes" && r.name != "Eyebrows");

            // Chopper (the first outfit): the peasant shirt and trousers baked as a red and black plaid shirt, belt
            // and jeans, the ranger's long sleeves in the same plaid (without their bare forearm skin), the ranger's
            // boots. The other outfits are the packs' own, tinted by the chosen shirt color.
            SkinnedMeshRenderer shirt = FindPart("Male_Peasant_Body", "Male_Peasant_Body");
            SkinnedMeshRenderer legs = FindPart("Male_Peasant_Legs", "Male_Peasant_Legs");
            SkinnedMeshRenderer sleeves = FindPart("Male_Ranger_Arms", "Male_Ranger_Arms");
            SkinnedMeshRenderer boots = FindPart("Male_Ranger_Feet_Boots", "Male_Ranger_Feet_Boots");
            Material chopperCloth = MakeChopperCloth();
            Material chopperLegs = MakeChopperLegs();
            Material chopperSleeves = MakeChopperSleeves(sleeves);
            Mesh sleevesMesh = ClothOnlyMesh(sleeves, SleevesMeshPath);
            var chopperParts = new List<(SkinnedMeshRenderer renderer, Mesh mesh, OutfitPart part)>();
            if (shirt != null) chopperParts.Add((shirt, shirt.sharedMesh, Fixed(MakePart(shirt, shirt.sharedMesh, m, chopperCloth, false))));
            if (legs != null) chopperParts.Add((legs, legs.sharedMesh, Fixed(MakePart(legs, legs.sharedMesh, m, chopperLegs, false))));
            if (sleeves != null) chopperParts.Add((sleeves, sleevesMesh, Fixed(MakePart(sleeves, sleevesMesh, m, chopperSleeves, false))));
            if (boots != null) chopperParts.Add((boots, boots.sharedMesh, Fixed(MakePart(boots, boots.sharedMesh, m, m.Ranger, false))));

            catalog.Head = BuildFace(body, m);
            catalog.Outfits = new[]
            {
                WithBody("Chopper", bodySkin, m, chopperParts, false),
                Outfit("Lumberjack", bodySkin, m, m.Peasant, false, "Male_Peasant_Body", "Male_Peasant_Arms", "Male_Peasant_Legs", "Male_Peasant_Feet"),
                Outfit("Woodsman", bodySkin, m, m.Peasant2, false, "Male_Peasant_Body", "Male_Peasant_Arms", "Male_Peasant_Legs", "Male_Peasant_Feet"),
                Outfit("Scout", bodySkin, m, m.Ranger, false, "Male_Ranger_Body", "Male_Ranger_Arms", "Male_Ranger_Legs", "Male_Ranger_Feet_Boots"),
                Outfit("Ranger", bodySkin, m, m.Ranger, true, "Male_Ranger_Body", "Male_Ranger_Arms", "Male_Ranger_Legs", "Male_Ranger_Feet_Boots", "Male_Ranger_Head_Hood", "Male_Ranger_Acc_Pauldron"),
                Outfit("Trapper", bodySkin, m, m.Ranger3, false, "Male_Ranger_Body", "Male_Ranger_Arms", "Male_Ranger_Legs", "Male_Ranger_Feet_Boots")
            };
            catalog.ChopperCloth = chopperCloth;
            catalog.ChopperLegs = chopperLegs;
            catalog.PlainCloth = m.Peasant;
            catalog.ChopperSleeves = chopperSleeves;
            catalog.PlainSleeves = m.Ranger;
            catalog.Hairs = new[]
            {
                new OutfitSet { Name = "Bald", Parts = new OutfitPart[0] },
                HairSet("Buzzed", m, "Hair_Buzzed"),
                HairSet("Parted", m, "Hair_SimpleParted"),
                HairSet("Long", m, "Hair_Long")
            };
            catalog.GearMaterial = m.Gear;
            catalog.Beards = new[]
            {
                new OutfitSet { Name = "Clean shaven", Parts = new OutfitPart[0] },
                HairSet("Full beard", m, "Hair_Beard")
            };
            catalog.HairColors = new[]
            {
                Named("Black", HairTint(0.05f, 0.04f, 0.04f)),
                Named("Dark brown", HairTint(0.11f, 0.065f, 0.04f)),
                Named("Brown", HairTint(0.25f, 0.15f, 0.08f)),
                Named("Auburn", HairTint(0.38f, 0.15f, 0.07f)),
                Named("Ginger", HairTint(0.55f, 0.24f, 0.09f)),
                Named("Blonde", HairTint(0.70f, 0.55f, 0.28f)),
                Named("Gray", HairTint(0.45f, 0.45f, 0.45f)),
                Named("White", HairTint(0.80f, 0.80f, 0.80f))
            };
            catalog.ClothTints = new[]
            {
                Named("Natural", new Color(1f, 1f, 1f)),
                Named("Red", new Color(1.6f, 0.7f, 0.62f)),
                Named("Forest", new Color(0.8f, 1.15f, 0.8f)),
                Named("Navy", new Color(0.65f, 0.8f, 1.3f)),
                Named("Mustard", new Color(1.35f, 1.12f, 0.55f)),
                Named("Charcoal", new Color(0.62f, 0.62f, 0.64f))
            };
            // Multipliers on the skin texture; Warm is the tone Chopper has had since 2026-10-05.
            catalog.SkinTones = new[]
            {
                Named("Fair", new Color(2.3f, 1.8f, 1.4f)),
                Named("Warm", new Color(2.05f, 1.5f, 1.08f)),
                Named("Tan", new Color(1.6f, 1.12f, 0.8f)),
                Named("Brown", new Color(1.1f, 0.78f, 0.58f)),
                Named("Dark", new Color(0.7f, 0.5f, 0.4f))
            };
            catalog.PantsColor = new Color(0.16f, 0.24f, 0.42f);

            if (isNew) AssetDatabase.CreateAsset(catalog, CatalogPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        // ---------- Parts ----------

        private static SkinnedMeshRenderer FindPart(string fbx, string meshName)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutfitFolder}/Parts/{fbx}.fbx");
            if (go == null)
            {
                Debug.LogWarning($"[OutfitSetup] Missing {fbx}.fbx.");
                return null;
            }
            return go.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.name == meshName);
        }

        // Baked clothes keep their own colors; nothing on them is tinted at runtime.
        private static OutfitPart Fixed(OutfitPart part)
        {
            for (int i = 0; i < part.Roles.Length; i++) part.Roles[i] = PartMaterialRole.Fixed;
            return part;
        }

        // The base body's eyes and eyebrows; the body itself comes with each outfit.
        private static OutfitPart[] BuildFace(GameObject body, Materials m)
        {
            var parts = new List<OutfitPart>();
            foreach (SkinnedMeshRenderer smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.name == "Eyes" || smr.name == "Eyebrows") parts.Add(MakePart(smr, smr.sharedMesh, m, null, false));
            }
            AssetDatabase.DeleteAsset(HeadMeshPath);
            AssetDatabase.DeleteAsset("Assets/_Chopper/Prefabs/Player/Mesh_Body.asset");
            return parts.ToArray();
        }

        // An outfit from the pack's own parts: every skinned mesh in the listed files. The shirt, arms and hood take the
        // chosen shirt color; belts, bracers, trousers and boots keep their own.
        private static OutfitSet Outfit(string name, SkinnedMeshRenderer bodySkin, Materials m, Material cloth, bool hidesHair, params string[] fbxNames)
        {
            var parts = new List<(SkinnedMeshRenderer renderer, Mesh mesh, OutfitPart part)>();
            foreach (string fbx in fbxNames)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutfitFolder}/Parts/{fbx}.fbx");
                if (go == null)
                {
                    Debug.LogWarning($"[OutfitSetup] Missing {fbx}.fbx.");
                    continue;
                }
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    bool tintable = (fbx.EndsWith("_Body") || fbx.EndsWith("_Arms") || fbx.EndsWith("_Hood")) && !smr.name.Contains("Belt") && !smr.name.Contains("Bracer");
                    parts.Add((smr, smr.sharedMesh, MakePart(smr, smr.sharedMesh, m, cloth, tintable)));
                }
            }
            return WithBody(name, bodySkin, m, parts, hidesHair);
        }

        // The outfit with its own cut of the body: the whole skin, less only what these clothes cover.
        private static OutfitSet WithBody(string name, SkinnedMeshRenderer bodySkin, Materials m, List<(SkinnedMeshRenderer renderer, Mesh mesh, OutfitPart part)> parts, bool hidesHair)
        {
            var covers = parts.Select(p => (p.renderer, p.mesh)).ToList();
            OutfitPart[] body = new OutfitPart[0];
            if (bodySkin != null)
            {
                Mesh cut = UncoveredBodyMesh(bodySkin, covers, $"{BodyMeshFolder}/Mesh_Body_{name}.asset", name);
                body = new[] { MakePart(bodySkin, cut, m, null, false) };
            }
            return new OutfitSet { Name = name, Parts = parts.Select(p => p.part).ToArray(), Body = body, HidesHair = hidesHair };
        }

        // A standalone animated character for the start screen: the same skeleton and animator, dressed by the same
        // outfitter, turned to face +Z like the player.
        public static void BuildPreview(CharacterCatalog catalog, GameObject modelFbx, Avatar avatar, RuntimeAnimatorController controller, float facingYaw)
        {
            if (catalog == null || modelFbx == null) return;
            var root = new GameObject("CharacterPreview");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelFbx, root.transform);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
            model.transform.localScale = Vector3.one * CharacterSetup.ChopperScale;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.runtimeAnimatorController = controller;

            CharacterOutfitter outfitter = model.AddComponent<CharacterOutfitter>();
            outfitter.Catalog = catalog;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PreviewPath);
            Object.DestroyImmediate(root);
            catalog.PreviewPrefab = saved;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        // The body mesh without the triangles the clothes hide. A vertex is hidden when a clothing triangle crosses
        // the short line along its normal (a little inside the skin to a few centimeters out); a triangle goes only
        // when all three of its corners are hidden, so the skin always reaches under the edge of the cloth.
        private static Mesh UncoveredBodyMesh(SkinnedMeshRenderer smr, List<(SkinnedMeshRenderer renderer, Mesh mesh)> covers, string path, string label)
        {
            Mesh source = smr.sharedMesh;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;

            // Every clothing vertex is brought into the body mesh's own space the way the skeleton would draw it in the
            // bind pose: through each of its bones' bind poses and back out through the body's bind pose for the
            // same bone. Each file's own transform is not trusted (it put the clothes beside the skin).
            var boneToBody = new Dictionary<string, Matrix4x4>();
            for (int i = 0; i < smr.bones.Length && i < source.bindposes.Length; i++)
            {
                if (smr.bones[i] != null) boneToBody[smr.bones[i].name] = source.bindposes[i].inverse;
            }
            var tris = new List<Vector3>();
            foreach (var cover in covers)
            {
                Vector3[] cv = InBodySpace(cover.renderer, cover.mesh, boneToBody);
                if (cv == null) continue;
                int[] ct = cover.mesh.triangles;
                for (int t = 0; t + 2 < ct.Length; t += 3)
                {
                    tris.Add(cv[ct[t]]);
                    tris.Add(cv[ct[t + 1]]);
                    tris.Add(cv[ct[t + 2]]);
                }
            }
            int count = tris.Count / 3;
            var mins = new Vector3[count];
            var maxs = new Vector3[count];
            for (int t = 0; t < count; t++)
            {
                mins[t] = Vector3.Min(tris[t * 3], Vector3.Min(tris[t * 3 + 1], tris[t * 3 + 2]));
                maxs[t] = Vector3.Max(tris[t * 3], Vector3.Max(tris[t * 3 + 1], tris[t * 3 + 2]));
            }

            var hidden = new bool[vertices.Length];
            int hiddenCount = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals[i].normalized;
                // The straight line out of the skin, and four more a few millimeters to the sides: a line that runs
                // exactly along a seam in the cloth (the shirt's back seam on the spine) slips between its triangles,
                // and a strip of skin showed through there.
                Vector3 side = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 side2 = Vector3.Cross(n, side);
                Vector3[] offsets = { Vector3.zero, side * SeamJitter, -side * SeamJitter, side2 * SeamJitter, -side2 * SeamJitter };
                foreach (Vector3 offset in offsets)
                {
                    if (hidden[i]) break;
                    Vector3 p = vertices[i] + offset;
                    Vector3 from = p - n * CoverInside;
                    Vector3 to = p + n * CoverOutside;
                    Vector3 segMin = Vector3.Min(from, to), segMax = Vector3.Max(from, to);
                    for (int t = 0; t < count; t++)
                    {
                        if (maxs[t].x < segMin.x || mins[t].x > segMax.x || maxs[t].y < segMin.y || mins[t].y > segMax.y || maxs[t].z < segMin.z || mins[t].z > segMax.z) continue;
                        if (!SegmentHitsTriangle(from, to, tris[t * 3], tris[t * 3 + 1], tris[t * 3 + 2])) continue;
                        hidden[i] = true;
                        hiddenCount++;
                        break;
                    }
                }
            }

            int[] triangles = source.triangles;
            var kept = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (hidden[triangles[t]] && hidden[triangles[t + 1]] && hidden[triangles[t + 2]]) continue;
                kept.Add(triangles[t]);
                kept.Add(triangles[t + 1]);
                kept.Add(triangles[t + 2]);
            }
            Debug.Log($"[OutfitSetup] Body for {label}: {hiddenCount} of {vertices.Length} vertices under the clothes, {kept.Count / 3} of {triangles.Length / 3} triangles kept, against {count} clothing triangles.");
            return SaveMesh(source, kept, path, Path.GetFileNameWithoutExtension(path), 0);
        }

        // A skinned mesh's vertices where the body mesh would have them, in the bind pose (see UncoveredBodyMesh).
        private static Vector3[] InBodySpace(SkinnedMeshRenderer renderer, Mesh mesh, Dictionary<string, Matrix4x4> boneToBody)
        {
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] binds = mesh.bindposes;
            Transform[] bones = renderer.bones;
            var toBody = new Matrix4x4[binds.Length];
            for (int i = 0; i < binds.Length; i++)
            {
                if (i >= bones.Length || bones[i] == null || !boneToBody.TryGetValue(bones[i].name, out Matrix4x4 back))
                {
                    Debug.LogWarning($"[OutfitSetup] {renderer.name}: bone {i} has no match on the body; left out of the cover test.");
                    return null;
                }
                toBody[i] = back * binds[i];
            }
            var result = new Vector3[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                BoneWeight w = weights[v];
                Vector3 x = vertices[v];
                result[v] = toBody[w.boneIndex0].MultiplyPoint3x4(x) * w.weight0 + toBody[w.boneIndex1].MultiplyPoint3x4(x) * w.weight1
                          + toBody[w.boneIndex2].MultiplyPoint3x4(x) * w.weight2 + toBody[w.boneIndex3].MultiplyPoint3x4(x) * w.weight3;
            }
            return result;
        }

        // Möller and Trumbore, limited to the segment.
        private static bool SegmentHitsTriangle(Vector3 from, Vector3 to, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 dir = to - from;
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 h = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, h);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            Vector3 s = from - a;
            float u = inv * Vector3.Dot(s, h);
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = inv * Vector3.Dot(dir, q);
            if (v < 0f || u + v > 1f) return false;
            float t = inv * Vector3.Dot(e2, q);
            return t >= 0f && t <= 1f;
        }

        // A clothing mesh with only its cloth submeshes (the bare skin the pack ships with some sleeves is dropped:
        // the body underneath has its own, in one tone).
        private static Mesh ClothOnlyMesh(SkinnedMeshRenderer smr, string path)
        {
            if (smr == null) return null;
            Mesh source = smr.sharedMesh;
            Material[] materials = smr.sharedMaterials;
            var kept = new List<int>();
            int keptSub = -1;
            for (int sub = 0; sub < source.subMeshCount && sub < materials.Length; sub++)
            {
                if (materials[sub] != null && SkinAndHairMaterials.Any(prefix => materials[sub].name.StartsWith(prefix))) continue;
                kept.AddRange(source.GetTriangles(sub));
                keptSub = sub;
            }
            return SaveMesh(source, kept, path, Path.GetFileNameWithoutExtension(path), keptSub);
        }

        // A copy of source with one submesh holding the given triangles, saved (or refreshed) at path.
        private static Mesh SaveMesh(Mesh source, List<int> triangles, string path, string name, int materialSub)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh mesh = existing != null ? existing : new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = source.indexFormat;
            mesh.vertices = source.vertices;
            mesh.normals = source.normals;
            mesh.tangents = source.tangents;
            mesh.uv = source.uv;
            mesh.boneWeights = source.boneWeights;
            mesh.bindposes = source.bindposes;
            mesh.subMeshCount = 1;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static OutfitSet HairSet(string name, Materials m, string fbx)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{BaseFolder}/Hair/{fbx}.fbx");
            var parts = new List<OutfitPart>();
            if (go != null)
            {
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    parts.Add(MakePart(smr, smr.sharedMesh, m, null, false));
                }
            }
            return new OutfitSet { Name = name, Parts = parts.ToArray() };
        }

        private static OutfitPart MakePart(SkinnedMeshRenderer source, Mesh mesh, Materials m, Material cloth, bool clothTintable)
        {
            Material[] originals = source.sharedMaterials;
            // A mesh cut down to its cloth (ClothOnlyMesh) has one submesh: keep only the cloth material for it.
            if (mesh != null && mesh.subMeshCount < originals.Length)
                originals = originals.Where(o => o == null || !SkinAndHairMaterials.Any(prefix => o.name.StartsWith(prefix))).Take(mesh.subMeshCount).ToArray();
            var materials = new Material[originals.Length];
            var roles = new PartMaterialRole[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                string original = originals[i] != null ? originals[i].name : string.Empty;
                switch (original)
                {
                    case "MI_Regular_Male": materials[i] = m.Hands; roles[i] = PartMaterialRole.Skin; break;
                    case "MI_Superhero_Male": materials[i] = m.Head; roles[i] = PartMaterialRole.Skin; break;
                    case "MI_Hair_1": materials[i] = m.Hair1; roles[i] = PartMaterialRole.Hair; break;
                    case "MI_Hair_2": materials[i] = m.Hair2; roles[i] = PartMaterialRole.Hair; break;
                    case "MI_Eyes": materials[i] = m.Eyes; roles[i] = PartMaterialRole.Fixed; break;
                    default:
                        materials[i] = cloth != null ? cloth : originals[i];
                        roles[i] = clothTintable ? PartMaterialRole.Cloth : PartMaterialRole.Fixed;
                        break;
                }
            }

            return new OutfitPart
            {
                Name = source.name,
                Mesh = mesh,
                Materials = materials,
                Roles = roles,
                BoneNames = source.bones.Select(b => b != null ? b.name : "root").ToArray()
            };
        }

        // ---------- Materials ----------

        private const string ChopperClothTexturePath = MaterialFolder + "/T_Chopper_Clothes.png";

        private static readonly string[] SkinAndHairMaterials = { "MI_Regular", "MI_Superhero", "MI_Hair", "MI_Eyes" };

        private const string ChopperLegsTexturePath = MaterialFolder + "/T_Chopper_Legs.png";

        // The shirt and the trousers each on their own copy of the peasant atlas: their texture areas overlap, so baked
        // together the belt buckle's gold was painted onto the shirt's back. (The peasant arms are not used: Chopper
        // wears the ranger's long sleeves.)
        private static Material MakeChopperCloth() => BakePeasant("Male_Peasant_Body", ChopperClothTexturePath, "M_Cloth_Chopper");

        private static Material MakeChopperLegs() => BakePeasant("Male_Peasant_Legs", ChopperLegsTexturePath, "M_Cloth_ChopperLegs");

        private static Material BakePeasant(string fbx, string texturePath, string materialName)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutfitFolder}/Parts/{fbx}.fbx");
            SkinnedMeshRenderer[] parts = go != null ? go.GetComponentsInChildren<SkinnedMeshRenderer>() : new SkinnedMeshRenderer[0];
            ClothesBaker.Bake(OutfitFolder + "/Textures/T_Peasant_BaseColor.png", texturePath,
                material => material != null && !SkinAndHairMaterials.Any(prefix => material.name.StartsWith(prefix)), parts);
            return MakeMaterial(materialName, texturePath, OutfitFolder + "/Textures/T_Peasant_Normal.png", 0.12f);
        }

        private const string ChopperSleevesTexturePath = MaterialFolder + "/T_Chopper_Sleeves.png";

        // The ranger sleeves in the same plaid as the shirt, down to the wrist, on their own copy of the ranger atlas.
        private static Material MakeChopperSleeves(SkinnedMeshRenderer sleeves)
        {
            if (sleeves == null) return null;
            ClothesBaker.Bake(OutfitFolder + "/Textures/T_Ranger_BaseColor.png", ChopperSleevesTexturePath,
                material => material != null && !SkinAndHairMaterials.Any(prefix => material.name.StartsWith(prefix)), true, sleeves);
            return MakeMaterial("M_Cloth_ChopperSleeves", ChopperSleevesTexturePath, OutfitFolder + "/Textures/T_Ranger_Normal.png", 0.12f);
        }

        private static Materials MakeMaterials()
        {
            string outfits = OutfitFolder + "/Textures/";
            string bases = BaseFolder + "/Textures/";
            return new Materials
            {
                Peasant = MakeMaterial("M_Cloth_Peasant", outfits + "T_Peasant_BaseColor.png", outfits + "T_Peasant_Normal.png", 0.12f),
                Peasant2 = MakeMaterial("M_Cloth_Peasant2", outfits + "T_Peasant_2_BaseColor.png", outfits + "T_Peasant_Normal.png", 0.12f),
                Ranger = MakeMaterial("M_Cloth_Ranger", outfits + "T_Ranger_BaseColor.png", outfits + "T_Ranger_Normal.png", 0.15f),
                Ranger3 = MakeMaterial("M_Cloth_Ranger3", outfits + "T_Ranger_3_BaseColor.png", outfits + "T_Ranger_Normal.png", 0.15f),
                Hands = MakeMaterial("M_Skin_Hands", outfits + "T_Regular_Male_Dark_BaseColor.png", outfits + "T_Regular_Male_Normal.png", 0.25f),
                Head = MakeMaterial("M_Skin_Head", bases + "T_Superhero_Male_Dark.png", bases + "T_Superhero_Male_Normal.png", 0.25f),
                Hair1 = MakeMaterial("M_Hair_1", bases + "T_Hair_1_BaseColor.png", bases + "T_Hair_1_Normal.png", 0.3f),
                Hair2 = MakeMaterial("M_Hair_2", bases + "T_Hair_2_BaseColor.png", bases + "T_Hair_2_Normal.png", 0.3f),
                Eyes = MakeMaterial("M_Eyes", bases + "T_Eye_Brown.png", bases + "T_Eye_Normal.png", 0.6f),
                Gear = MakeGearMaterial()
            };
        }

        // Plain brown for gear made in code: the fishing rod (tinted per piece) and the suspenders. Chopper wears no
        // hat any more, so the old hat material goes.
        private static Material MakeGearMaterial()
        {
            AssetDatabase.DeleteAsset($"{MaterialFolder}/M_Hat.mat");
            string path = $"{MaterialFolder}/M_Gear.mat";
            AssetDatabase.DeleteAsset(path);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Gear" };
            material.SetColor("_BaseColor", new Color(0.30f, 0.19f, 0.11f));
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Valheim style: the character's textures are small and crisp like the world's pixel pack, not smooth.
        private const int CharacterTextureSize = 256;

        private static void Pixelate(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.filterMode == FilterMode.Point && importer.maxTextureSize == CharacterTextureSize && importer.mipmapEnabled == false) return;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = CharacterTextureSize;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Material MakeMaterial(string name, string basePath, string normalPath, float smoothness)
        {
            EnsureNormalMap(normalPath);
            Pixelate(basePath);
            string path = $"{MaterialFolder}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            material.SetColor("_BaseColor", Color.white);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            // Emission on, but black. The draugr turn emission on at runtime to glow, and a build only keeps the shader
            // variants its saved materials use: without this the normal mapped emissive variant was stripped and every
            // draugr body drew nothing (only their swords showed).
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureNormalMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        private static NamedColor Named(string name, Color color) => new NamedColor { Name = name, Color = color };

        private static Color HairTint(float r, float g, float b)
        {
            return new Color(r / HairTextureBrightness, g / HairTextureBrightness, b / HairTextureBrightness);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
