using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Import settings for recordings dropped into Resources/Audio (see the audio list in requirements.md). Short
    // effects are decompressed on load so SoundBank can measure and level them; ambience and music loops stream.
    public class AudioImportRules : AssetPostprocessor
    {
        private const string Folder = "Assets/_Chopper/Resources/Audio/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (AudioImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool loop = name.StartsWith("ambience-") || name.StartsWith("music-");

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = loop ? 0.6f : 0.8f;
            settings.preloadAudioData = !loop;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !loop;
            importer.loadInBackground = loop;
        }
    }
}
