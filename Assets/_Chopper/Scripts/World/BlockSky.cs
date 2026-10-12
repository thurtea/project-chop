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

        static Mesh CloudMesh(int index)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            int blocks = 4 + (index % 3);
            float cell = 7f + (index % 3);
            for (int b = 0; b < blocks; b++)
            {
                int h = (index * 5 + b * 3) % 7;
                var center = new Vector3((b - blocks * 0.4f) * cell * 0.85f, (b % 2) * cell * 0.35f, ((b * 2) % 3 - 1) * cell * 0.4f);
                AddBox(verts, cols, tris, center, new Vector3(cell, cell * (0.6f + (h % 3) * 0.2f), cell),
                    b % 2 == 0 ? new Color(0.96f, 0.97f, 0.98f) : new Color(0.82f, 0.86f, 0.90f));
            }
            return MeshFrom(verts, cols, tris);
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
