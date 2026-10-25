using UnityEditor;
using UnityEngine;

/// <summary>
/// Setzt die Import-Settings der eingebauten Audio-Clips unter Assets/AudioFiles.
///
/// Default-Set: kurze Ansagen (24 kHz, mono) → Decompress On Load, kein Preload.
///              RAM-Bedarft ≈ Roh-WAV-Größe (~14 MB) — unkritisch.
///
/// Custom-Set:  lange Aufnahmen (bis ~10 s, 44,1 kHz stereo, ~101 MB Roh-WAV) →
///              Force To Mono + Compressed In Memory. Dadurch bleibt der
///              RAM-Verbrauch auf dem Handy im Rahmen (komprimiert statt
///              volldekodiert im Speicher).
///
/// Preload ist bei beiden Sets aus: die Clips werden erst beim Abspielen
/// geladen (die eingebauten Clips liegen als Assets vor, sie müssen nicht
/// vorab in die Runtime-Dicts geladen werden).
///
/// Menü: Darts/Audio/Import-Settings anwenden
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

            // Standard-Settings (alle Plattformen ohne expliziten Override).
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

            // Android-Override mit denselben Werten.
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

    /// <summary>Status-Log der beiden Sets (Clip-Anzahl + Import-Settings).</summary>
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
