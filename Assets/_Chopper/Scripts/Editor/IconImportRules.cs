using UnityEditor;

namespace Chopper.Editor
{
    // The game-icons.net silhouettes (white on transparent, 256 px) become UI sprites: no compression (they are
    // small), mipmaps so they stay smooth when drawn at hotbar size, and the alpha kept clean for tinting.
    public class IconImportRules : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/GameIcons_net/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
