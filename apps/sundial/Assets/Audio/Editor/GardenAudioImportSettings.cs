// Import policy for everything under Assets/Audio (AUDIO-BIBLE section 8): Vorbis quality 0.6; SFX decompress on load;
// beds (Music/) and narration (Voice/) stream. Applied once, when Unity first imports a clip (importSettingsMissing),
// so a later hand-tuned import is never overwritten. The .meta files carrying these settings are committed by the audio agent.
using UnityEditor;
using UnityEngine;

namespace GardenVR.AudioImport
{
    internal sealed class GardenAudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            if (!importer.importSettingsMissing) return;

            bool streams = assetPath.StartsWith("Assets/Audio/Music/") || assetPath.StartsWith("Assets/Audio/Voice/");
            var s = importer.defaultSampleSettings;
            s.loadType = streams ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = !streams;
            importer.defaultSampleSettings = s;
            importer.loadInBackground = streams;
        }
    }
}
