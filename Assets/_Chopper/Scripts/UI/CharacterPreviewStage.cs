using Chopper.Player;
using UnityEngine;

namespace Chopper.UI
{
    // A small photo studio for the start screen: the animated preview character far below the world, a camera that
    // renders it into a texture, and a light. The character turns slowly so every side of the outfit can be seen.
    public class CharacterPreviewStage
    {
        private const float StageHeight = -500f;
        private const int TextureWidth = 512;
        private const int TextureHeight = 768;
        private const float TurnSpeed = 28f;

        private readonly GameObject _root;
        private readonly CharacterOutfitter _outfitter;
        private readonly RenderTexture _texture;

        public RenderTexture Texture => _texture;

        public CharacterPreviewStage(CharacterCatalog catalog)
        {
            _root = new GameObject("CharacterPreviewStage");
            _root.transform.position = new Vector3(0f, StageHeight, 0f);

            _texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

            var cameraGo = new GameObject("PreviewCamera");
            cameraGo.transform.SetParent(_root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0.9f, 4.0f);
            cameraGo.transform.localRotation = Quaternion.Euler(2f, 180f, 0f);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.09f, 1f);
            camera.targetTexture = _texture;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;

            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_root.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(28f, 150f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.shadows = LightShadows.None;

            if (catalog != null && catalog.PreviewPrefab != null)
            {
                GameObject model = Object.Instantiate(catalog.PreviewPrefab, _root.transform);
                model.transform.localPosition = Vector3.zero;
                _outfitter = model.GetComponentInChildren<CharacterOutfitter>();
                PreviewTurner turner = _root.AddComponent<PreviewTurner>();
                turner.Target = model.transform;
                turner.Speed = TurnSpeed;
            }
            _root.SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (_root != null) _root.SetActive(active);
        }

        public void Apply(AppearanceData data)
        {
            if (_outfitter != null) _outfitter.Apply(data);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            if (_texture != null) _texture.Release();
        }
    }
}
