using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Import settings for everything under Assets/Resources/Audio, by folder.
    ///
    /// Every clip here is produced by a script in Tools/audio and re-baked often, so settings have
    /// to follow the file rather than be clicked per clip. The defaults would decompress ~20 MB
    /// of music into memory at load and stall the frame; these keep music streamed, voices
    /// compressed until played, and short SFX decoded for instant, click-free starts.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Audio/";

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root)) return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;

            if (path.StartsWith(Root + "Music/"))
            {
                // Long stems: stream from disk. Stems start via PlayScheduled, so preloading keeps
                // their first buffer ready and the layers stay phase-locked.
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.loadInBackground = true;
            }
            else if (path.StartsWith(Root + "Voices/"))
            {
                // Hundreds of short lines, only a few in use at once.
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.55f;
                settings.preloadAudioData = false;
                importer.loadInBackground = true;
            }
            else if (path.Contains("ambient") || path.Contains("/Ambient/"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.loadInBackground = true;
            }
            else
            {
                // One-shots: decoded up front so a hit plays on the frame it happens.
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
            }

            // The bake scripts already choose mono or stereo per clip (victory, launch and fail
            // are stereo on purpose). Force-to-mono would also peak-normalise every clip and flatten
            // the loudness hierarchy render_sfx.py sets, so leave channels and levels alone.
            importer.forceToMono = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
