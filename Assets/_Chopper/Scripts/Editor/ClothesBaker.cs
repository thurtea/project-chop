using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Paints Chopper's own clothes over the peasant outfit's texture atlas, after the concept art: a red and black
    // buffalo plaid shirt with the sleeves rolled to the elbow, a brown belt with a gold buckle, and blue work
    // trousers. Every triangle of the shirt, sleeves and trousers is filled in UV space with a color worked out from
    // where that point sits on the body (from the skeleton's bind pose), so the checks run straight around the body
    // and meet across the UV seams. Pixels no painted part uses keep the original atlas (the boots).
    public static class ClothesBaker
    {
        private const int Size = 2048;
        // Buffalo plaid checks, in meters, and the radii used to unroll the torso and the sleeves into flat cloth.
        private const float Check = 0.055f;
        private const float TorsoRadius = 0.16f;
        private const float ArmRadius = 0.06f;
        // The belt, in meters above the pelvis bone, and the buckle's half width at the front.
        private const float BeltLow = 0.05f;
        private const float BeltHigh = 0.11f;
        private const float BuckleHalfWidth = 0.035f;
        // Triangles are filled a little past their edges so no background shows along the UV seams.
        private const float EdgeSlack = 0.04f;

        private static readonly Color Red = new Color(0.62f, 0.06f, 0.05f);
        private static readonly Color DarkRed = new Color(0.30f, 0.03f, 0.03f);
        private static readonly Color Black = new Color(0.05f, 0.04f, 0.04f);
        public static readonly Color Skin = new Color(0.86f, 0.55f, 0.38f);
        private static readonly Color Leather = new Color(0.25f, 0.14f, 0.07f);
        private static readonly Color Gold = new Color(0.86f, 0.66f, 0.22f);
        private static readonly Color Denim = new Color(0.16f, 0.24f, 0.44f);

        // The body's frame in the meshes' bind pose: where the pelvis is and which ways are up, sideways and forward,
        // plus how far out the shoulders and elbows are and how high the shoulders sit.
        private struct Frame
        {
            public Vector3 Pelvis, Up, Side, Forward;
            public float ShoulderSide, ElbowSide, ShoulderHeight;
        }

        // Bakes the texture to outPath (a PNG in the project) and returns the imported asset. Only the material slots
        // the cloth test accepts are painted.
        public static Texture2D Bake(string sourcePath, string outPath, System.Func<Material, bool> isCloth, params SkinnedMeshRenderer[] parts)
        {
            return Bake(sourcePath, outPath, isCloth, false, parts);
        }

        // longSleeves: plaid down to the wrist instead of sleeves rolled to the elbow.
        public static Texture2D Bake(string sourcePath, string outPath, System.Func<Material, bool> isCloth, bool longSleeves, params SkinnedMeshRenderer[] parts)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(sourcePath));
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++) pixels[y * Size + x] = source.GetPixelBilinear((x + 0.5f) / Size, (y + 0.5f) / Size);
            }
            Object.DestroyImmediate(source);

            foreach (SkinnedMeshRenderer part in parts)
            {
                if (part == null || part.sharedMesh == null) continue;
                if (!TryFrame(part, out Frame frame))
                {
                    Debug.LogWarning($"[ClothesBaker] {part.name} is missing the bones the clothes are laid out by; left as it is.");
                    continue;
                }
                Mesh mesh = part.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                Material[] materials = part.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                {
                    if (!isCloth(materials[sub])) continue;
                    int[] triangles = mesh.GetTriangles(sub);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Fill(pixels, frame, vertices, uvs, triangles[t], triangles[t + 1], triangles[t + 2], longSleeves);
                    }
                }
            }

            var baked = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            baked.SetPixels(pixels);
            baked.Apply();
            File.WriteAllBytes(outPath, baked.EncodeToPNG());
            Object.DestroyImmediate(baked);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            // The import size and filtering are set by OutfitSetup (small and crisp, like the world).
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static bool TryFrame(SkinnedMeshRenderer part, out Frame frame)
        {
            frame = default;
            Matrix4x4[] binds = part.sharedMesh.bindposes;
            var bones = new Dictionary<string, Vector3>();
            for (int i = 0; i < part.bones.Length && i < binds.Length; i++)
            {
                if (part.bones[i] != null) bones[part.bones[i].name.ToLowerInvariant()] = binds[i].inverse.MultiplyPoint3x4(Vector3.zero);
            }
            if (!bones.TryGetValue("pelvis", out Vector3 pelvis) || !bones.TryGetValue("head", out Vector3 head)) return false;
            if (!bones.TryGetValue("upperarm_l", out Vector3 shoulderL) || !bones.TryGetValue("upperarm_r", out Vector3 shoulderR)) return false;
            if (!bones.TryGetValue("lowerarm_l", out Vector3 elbowL)) return false;

            Vector3 up = (head - pelvis).normalized;
            Vector3 side = Vector3.ProjectOnPlane(shoulderL - shoulderR, up).normalized;
            Vector3 forward = Vector3.Cross(side, up).normalized;
            // The toes say which way is front; the cross product only gets it right half the time.
            if (bones.TryGetValue("foot_l", out Vector3 foot) && bones.TryGetValue("ball_l", out Vector3 ball))
            {
                Vector3 toes = Vector3.ProjectOnPlane(ball - foot, up);
                if (toes.sqrMagnitude > 1e-6f && Vector3.Dot(toes, forward) < 0f) forward = -forward;
            }
            frame = new Frame
            {
                Pelvis = pelvis,
                Up = up,
                Side = side,
                Forward = forward,
                ShoulderSide = Mathf.Abs(Vector3.Dot(shoulderL - pelvis, side)),
                ElbowSide = Mathf.Abs(Vector3.Dot(elbowL - pelvis, side)),
                ShoulderHeight = Vector3.Dot(shoulderL - pelvis, up)
            };
            return frame.ElbowSide > frame.ShoulderSide;
        }

        private static void Fill(Color[] pixels, Frame frame, Vector3[] vertices, Vector2[] uvs, int a, int b, int c, bool longSleeves)
        {
            // Some islands sit outside 0 to 1 and wrap (the texture repeats): move each triangle into the 0 to 1 tile,
            // and wrap the pixels it covers, or those parts keep the original atlas (a tan patch on the shirt's back).
            Vector2 shift = new Vector2(Mathf.Floor(Mathf.Min(uvs[a].x, Mathf.Min(uvs[b].x, uvs[c].x))), Mathf.Floor(Mathf.Min(uvs[a].y, Mathf.Min(uvs[b].y, uvs[c].y))));
            Vector2 pa = (uvs[a] - shift) * Size, pb = (uvs[b] - shift) * Size, pc = (uvs[c] - shift) * Size;
            float area = (pb.x - pa.x) * (pc.y - pa.y) - (pc.x - pa.x) * (pb.y - pa.y);
            if (Mathf.Abs(area) < 1e-6f) return;
            int x0 = Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))) - 1;
            int x1 = Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))) + 1;
            int y0 = Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))) - 1;
            int y1 = Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))) + 1;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float wa = ((pb.x - p.x) * (pc.y - p.y) - (pc.x - p.x) * (pb.y - p.y)) / area;
                    float wb = ((pc.x - p.x) * (pa.y - p.y) - (pa.x - p.x) * (pc.y - p.y)) / area;
                    float wc = 1f - wa - wb;
                    if (wa < -EdgeSlack || wb < -EdgeSlack || wc < -EdgeSlack) continue;
                    Vector3 point = vertices[a] * wa + vertices[b] * wb + vertices[c] * wc;
                    int px = ((x % Size) + Size) % Size, py = ((y % Size) + Size) % Size;
                    pixels[py * Size + px] = Paint(frame, point, longSleeves);
                }
            }
        }

        private static Color Paint(Frame frame, Vector3 point, bool longSleeves)
        {
            Vector3 offset = point - frame.Pelvis;
            float side = Vector3.Dot(offset, frame.Side);
            float up = Vector3.Dot(offset, frame.Up);
            float forward = Vector3.Dot(offset, frame.Forward);
            float outward = Mathf.Abs(side);

            if (outward > frame.ShoulderSide)
            {
                // Rolled sleeves: bare forearms past the elbow, plaid between the shoulder and the elbow.
                if (outward > frame.ElbowSide && !longSleeves) return Skin;
                return Plaid(outward, Mathf.Atan2(up - frame.ShoulderHeight, forward) * ArmRadius);
            }
            if (up > BeltHigh) return Plaid(Mathf.Atan2(side, forward) * TorsoRadius, up);
            if (up >= BeltLow) return forward > 0f && outward < BuckleHalfWidth ? Gold : Leather;
            return Denim;
        }

        // Buffalo plaid: black where two black bands cross, dark red where one runs, red between them.
        private static Color Plaid(float across, float along)
        {
            int bands = (Mathf.FloorToInt(across / Check) & 1) + (Mathf.FloorToInt(along / Check) & 1);
            return bands == 2 ? Black : bands == 1 ? DarkRed : Red;
        }
    }
}
