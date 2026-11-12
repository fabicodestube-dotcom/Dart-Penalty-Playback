using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets the import settings of the built-in audio clips under Assets/ AudioFiles.
///
/// Default set: short announcements (24 kHz, mono) → Decompress On Load, no preload.
/// RAM demand ≈ raw WAV size (~14 MB) — non-critical.
///
/// Custom set: long recordings (up to ~10 s, 44.1 kHz stereo, ~101 MB raw WAV) →
/// Force To Mono + Compressed In Memory. This keeps the
/// RAM consumption on the mobile phone in the frame (compressed instead)
/// fully decoded in memory.
///
/// Preload is off for both sets: the clips are only played when
/// loaded (the built-in clips are available as assets, they do not have to be)
/// be loaded into the runtime dicts in advance.
///
/// Menu: Apply darts/audio/import settings
/// </summary>
public static class AudioClipImportSetup
{
    private const string DefaultSetRoot = "Assets/AudioFiles/Default";
    private const string CustomSetRoot = "Assets/AudioFiles/Custom";
    private const string AndroidPlatform = "Android";

    [MenuItem("Darts/Audio/Import-Settings anwenden")]
    public static void Apply()
    {
        int defaultCount = ApplySet(DefaultSetRoot, forceMono: false, loadType: AudioClipLoadType.DecompressOnLoad);
        int customCount = ApplySet(CustomSetRoot, forceMono: true, loadType: AudioClipLoadType.CompressedInMemory);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AUDIO] Import-Settings gesetzt — Default: {defaultCount} Clips, Custom: {customCount} Clips.");
    }

    private static int ApplySet(string root, bool forceMono, AudioClipLoadType loadType)
    {
        if (!AssetDatabase.IsValidFolder(root))
        {
            Debug.LogWarning($"[AUDIO] Import-Settings: Ordner '{root}' existiert nicht.");
            return 0;
        }

        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { root });
        int changed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;

            if (importer == null)
                continue;

            bool dirty = false;

            if (importer.forceToMono != forceMono)
            {
                importer.forceToMono = forceMono;
                dirty = true;
            }

            // Standard settings (all platforms without explicit override).
            var settings = importer.defaultSampleSettings;
            bool settingsChanged = false;

            if (settings.loadType != loadType)
            {
                settings.loadType = loadType;
                settingsChanged = true;
            }

            if (settings.preloadAudioData)
            {
                settings.preloadAudioData = false;
                settingsChanged = true;
            }

            if (settingsChanged)
            {
                importer.defaultSampleSettings = settings;
                dirty = true;
            }

            // Android overrides with the same values.
            var android = importer.GetOverrideSampleSettings(AndroidPlatform);
            bool androidChanged = false;

            if (android.loadType != loadType)
            {
                android.loadType = loadType;
                androidChanged = true;
            }

            if (android.preloadAudioData)
            {
                android.preloadAudioData = false;
                androidChanged = true;
            }

            if (androidChanged)
            {
                importer.SetOverrideSampleSettings(AndroidPlatform, android);
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                changed++;
            }
        }

        return changed;
    }

    /// <summary>Status log of the two sets (clip number + import settings).</summary>
    [MenuItem("Darts/Audio/Import-Status anzeigen")]
    public static void ShowStatus()
    {
        Report(DefaultSetRoot);
        Report(CustomSetRoot);
    }

    private static void Report(string root)
    {
        if (!AssetDatabase.IsValidFolder(root))
            return;

        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { root });

        int mono = 0;
        int compressed = 0;
        int preload = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;

            if (importer == null)
                continue;

            if (importer.forceToMono) mono++;

            if (importer.defaultSampleSettings.loadType == AudioClipLoadType.CompressedInMemory)
                compressed++;

            if (importer.defaultSampleSettings.preloadAudioData) preload++;
        }

        Debug.Log($"[AUDIO] {root}: {guids.Length} Clips — ForceToMono: {mono}, " +
            $"CompressedInMemory: {compressed}, Preload: {preload}");
    }
}
