using System.Collections.Generic;
using Chopper.World.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.World
{
    // The sky of chopper-3D-concept-2.0.jpg: white block clouds and a pale block sun. The clouds stay with the
    // player so the sky still has them away from the hut. The sun sits on the daylight, in the direction the
    // sun light comes from, and hides at night. Neither casts a shadow.
    public class BlockSky : MonoBehaviour
    {
        const float SunDistance = 220f;
        const float SunCell = 6f;

        Transform _clouds;
        Transform _sun;
        MeshRenderer _sunRenderer;
        MeshRenderer[] _cloudRenderers;

        public static void Build(Transform world)
        {
            var root = new GameObject("BlockSky");
            root.transform.SetParent(world, false);
            root.AddComponent<BlockSky>().Create();
        }

        void Create()
        {
            Shader lit = Shader.Find("Chopper/VertexColorLit");
            var cloudMat = new Material(lit);
            cloudMat.SetFloat("_UseAtlas", 0f);
            cloudMat.SetColor("_BaseColor", Color.white);

            _clouds = new GameObject("Clouds").transform;
            _clouds.SetParent(transform, false);
            var renderers = new List<MeshRenderer>();
            Vector3[] centers =
            {
                new Vector3(48f, 42f, 36f), new Vector3(-36f, 50f, 62f), new Vector3(18f, 58f, -48f),
                new Vector3(-72f, 40f, -18f), new Vector3(84f, 46f, -28f), new Vector3(-16f, 62f, 22f),
                new Vector3(8f, 44f, 88f), new Vector3(-58f, 48f, 44f)
            };
            for (int i = 0; i < centers.Length; i++)
            {
                var puff = new GameObject("Cloud");
                puff.transform.SetParent(_clouds, false);
                puff.transform.localPosition = centers[i];
                var filter = puff.AddComponent<MeshFilter>();
                filter.sharedMesh = CloudMesh(i);
                var renderer = puff.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = cloudMat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderers.Add(renderer);
            }
            _cloudRenderers = renderers.ToArray();

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var sunMat = new Material(unlit != null ? unlit : lit);
            sunMat.SetColor("_BaseColor", new Color(1f, 0.93f, 0.62f));
            if (sunMat.HasProperty("_UseAtlas")) sunMat.SetFloat("_UseAtlas", 0f);
            _sun = new GameObject("BlockSun").transform;
            _sun.SetParent(transform, false);
            var sunFilter = _sun.gameObject.AddComponent<MeshFilter>();
            sunFilter.sharedMesh = SunMesh();
            _sunRenderer = _sun.gameObject.AddComponent<MeshRenderer>();
            _sunRenderer.sharedMaterial = sunMat;
            _sunRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Debug.Log($"[BlockSky] {centers.Length} clouds, sun {sunFilter.sharedMesh.vertexCount} verts");
        }

        void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 cam = camera.transform.position;
            _clouds.position = new Vector3(cam.x, 0f, cam.z);

            Light sun = RenderSettings.sun;
            bool day = sun != null && -sun.transform.forward.y > 0.08f;
            bool under = CryptWorld.IsUnderground(cam);
            _sunRenderer.enabled = day && !under;
            for (int i = 0; i < _cloudRenderers.Length; i++) _cloudRenderers[i].enabled = !under;
            if (!day) return;
            Vector3 toward = -sun.transform.forward;
            _sun.position = cam + toward * SunDistance;
            _sun.rotation = Quaternion.LookRotation(-toward, Vector3.up);
        }

        public const float CloudCell = 2.5f;
        static readonly Color CloudTop = new Color(0.99f, 0.99f, 1f);
        static readonly Color CloudBottom = new Color(0.70f, 0.76f, 0.85f);
        static readonly Color CloudSky = new Color(0.80f, 0.87f, 0.96f);

        public static Mesh CloudMesh(int index)
        {
            var rng = new System.Random(9173 + index * 131);
            int lobes = 3 + index % 3;
            var lc = new Vector3[lobes];
            var lr = new Vector3[lobes];
            for (int i = 0; i < lobes; i++)
            {
                float t = i / (float)(lobes - 1) - 0.5f;
                float r = (6f + (float)rng.NextDouble() * 3.5f) * (1f + 0.35f * (1f - Mathf.Abs(t) * 2f));
                lc[i] = new Vector3(t * lobes * 6.5f, (float)rng.NextDouble() * 2f, ((float)rng.NextDouble() - 0.5f) * 6f);
                lr[i] = new Vector3(r * 1.15f, r * 0.72f, r);
            }
            int nx = Mathf.CeilToInt((lobes * 3.25f + 16f) / CloudCell), ny = Mathf.CeilToInt(12f / CloudCell), nz = Mathf.CeilToInt(16f / CloudCell);
            int sx = nx * 2 + 1, sy = ny * 2 + 1, sz = nz * 2 + 1;
            var fill = new float[sx, sy, sz];
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
            for (int z = 0; z < sz; z++)
            {
                Vector3 c = new Vector3(x - nx, y - ny, z - nz) * CloudCell;
                float best = 9f;
                if (c.y >= -1.5f)
                    for (int i = 0; i < lobes; i++)
                    {
                        Vector3 d = c - lc[i];
                        best = Mathf.Min(best, Mathf.Sqrt((d.x / lr[i].x) * (d.x / lr[i].x) + (d.y / lr[i].y) * (d.y / lr[i].y) + (d.z / lr[i].z) * (d.z / lr[i].z)));
                    }
                fill[x, y, z] = best;
                if (best <= 1f) { minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y); }
            }
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            Vector3Int[] dirs = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
            for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
            for (int z = 0; z < sz; z++)
            {
                if (fill[x, y, z] > 1f) continue;
                Vector3 c = new Vector3(x - nx, y - ny, z - nz) * CloudCell;
                float h = Mathf.InverseLerp(minY, maxY, c.y);
                h = h * h * (3f - 2f * h);
                Color body = Color.Lerp(CloudBottom, CloudTop, h);
                float rim = Mathf.InverseLerp(0.72f, 1f, fill[x, y, z]);
                body = Color.Lerp(body, CloudSky, rim * 0.35f);
                foreach (Vector3Int d in dirs)
                {
                    int ax = x + d.x, ay = y + d.y, az = z + d.z;
                    bool open = ax < 0 || ay < 0 || az < 0 || ax >= sx || ay >= sy || az >= sz || fill[ax, ay, az] > 1f;
                    if (!open) continue;
                    Color face = d.y > 0 ? Color.Lerp(body, CloudTop, 0.5f) : d.y < 0 ? body * 0.92f : body;
                    face.a = 1f;
                    AddFace(verts, cols, tris, c, d, CloudCell * 0.5f, face);
                }
            }
            return MeshFrom(verts, cols, tris);
        }

        static void AddFace(List<Vector3> verts, List<Color> cols, List<int> tris, Vector3 c, Vector3Int d, float h, Color color)
        {
            Vector3 n = d;
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(n, u);
            Vector3 f = c + n * h;
            int o = verts.Count;
            verts.Add(f - u * h - v * h);
            verts.Add(f + u * h - v * h);
            verts.Add(f + u * h + v * h);
            verts.Add(f - u * h + v * h);
            for (int i = 0; i < 4; i++) cols.Add(color);
            Vector3 normal = Vector3.Cross(verts[o + 1] - verts[o], verts[o + 2] - verts[o]);
            if (Vector3.Dot(normal, n) < 0f) { tris.Add(o); tris.Add(o + 2); tris.Add(o + 1); tris.Add(o); tris.Add(o + 3); tris.Add(o + 2); }
            else { tris.Add(o); tris.Add(o + 1); tris.Add(o + 2); tris.Add(o); tris.Add(o + 2); tris.Add(o + 3); }
        }

        static Mesh SunMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            const int r = 4;
            var core = new Color(1f, 0.98f, 0.82f);
            var edge = new Color(1f, 0.86f, 0.42f);
            for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y > r * r + 1) continue;
                AddBox(verts, cols, tris, new Vector3(x * SunCell, y * SunCell, 0f), Vector3.one * (SunCell * 0.92f),
                    x * x + y * y <= 4 ? core : edge);
            }
            return MeshFrom(verts, cols, tris);
        }

        static void AddBox(List<Vector3> verts, List<Color> cols, List<int> tris, Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * 0.5f;
            int o = verts.Count;
            Vector3[] c =
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z), center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(-h.x, -h.y, h.z), center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z)
            };
            int[] faces =
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7
            };
            for (int i = 0; i < c.Length; i++)
            {
                verts.Add(c[i]);
                cols.Add(color);
            }
            for (int i = 0; i < faces.Length; i++) tris.Add(o + faces[i]);
        }

        static Mesh MeshFrom(List<Vector3> verts, List<Color> cols, List<int> tris)
        {
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
