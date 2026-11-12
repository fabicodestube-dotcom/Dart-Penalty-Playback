using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Build help for the built-in audio clips.
///
/// The AudioLibrary holds two clip sets in the Inspector: Default (released)
/// and custom (development). which set at runtime as a built-in
/// Fallback serves, decides WillBePublished.
/// (419 clips, ~115 MB raw WAV) migrate to the build, the
/// Scene Processor: The inactive set from the build copy of the scene.
/// The source-scene asset remains unaffected.
///
/// Menu: Darts/Audio/...
/// </summary>
public class AudioLibraryBuildTools : IProcessSceneWithReport
{
    /// <summary>Order: run according to the usual processors.</summary>
    public int callbackOrder => 100;

    /// <summary>Number of clips removed during last build.</summary>
    public static int StrippedClipCount { get; private set; }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (!scene.IsValid())
            return;

        var stripped = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var lib in root.GetComponentsInChildren<AudioLibrary>(true))
                stripped += StripInactiveSet(lib);
        }

        if (stripped > 0)
            Debug.Log($"[AUDIO-BUILD] {stripped} Clips des inaktiven Sets aus Szene '{scene.name}' entfernt.");
    }

    /// <summary>
    /// Removes the set that is not in use according to WillBePublished.
    /// Return: Number of removed clips.
    /// </summary>
    private static int StripInactiveSet(AudioLibrary lib)
    {
        if (lib == null)
            return 0;

        int removed = 0;

        if (lib.WillBePublished)
        {
            // Default set is active → Remove custom set.
            removed += CountClips(lib.customNumberClips);
            lib.customNumberClips = null;

            removed += ClearList(ref lib.customSchnapszahlClip);
            removed += ClearList(ref lib.customThreeOnesClip);
            removed += ClearList(ref lib.customWallClip);
            removed += ClearList(ref lib.customCeilingClip);
            removed += ClearList(ref lib.customAllMissClip);
            removed += ClearList(ref lib.customStartGameClip);
            removed += ClearList(ref lib.customBustClip);
            removed += ClearList(ref lib.customFourTwentyClip);
            removed += ClearList(ref lib.customUndoClip);
            removed += ClearList(ref lib.customMatchWonClip);
            removed += ClearList(ref lib.customSuddenDeathClip);
            removed += ClearList(ref lib.customCricketMarksClip);
            removed += ClearList(ref lib.customAtcHitsClip);
            removed += ClearList(ref lib.customAtcStreakClip);
            removed += ClearList(ref lib.customBahClip);
            removed += ClearList(ref lib.customThreeTwentiesClip);
            removed += ClearList(ref lib.customThreeNineteensClip);
        }
        else
        {
            // Custom-Set is active → Remove default-Set.
            removed += CountClips(lib.numberClips);
            lib.numberClips = null;

            removed += ClearSingle(ref lib.schnapszahlClip);
            removed += ClearSingle(ref lib.threeOnesClip);
            removed += ClearSingle(ref lib.wallClip);
            removed += ClearSingle(ref lib.ceilingClip);
            removed += ClearSingle(ref lib.allMissClip);
            removed += ClearSingle(ref lib.startGameClip);
            removed += ClearSingle(ref lib.bustClip);
            removed += ClearSingle(ref lib.fourTwentyClip);
            removed += ClearSingle(ref lib.undoClip);
            removed += ClearSingle(ref lib.matchWonClip);
            removed += ClearSingle(ref lib.suddenDeathClip);
            removed += ClearSingle(ref lib.cricketMarksClip);
            removed += ClearSingle(ref lib.atcHitsClip);
            removed += ClearSingle(ref lib.atcStreakClip);
        }

        StrippedClipCount += removed;
        return removed;
    }

    private static int CountClips(AudioClip[] clips)
        => clips == null ? 0 : clips.Length;

    private static int ClearList(ref List<AudioClip> list)
    {
        int count = list?.Count ?? 0;
        list = null;
        return count;
    }

    private static int ClearSingle(ref AudioClip clip)
    {
        int count = clip != null ? 1 : 0;
        clip = null;
        return count;
    }

    // =========================================================
    // MENU
    // =========================================================

    /// <summary>
    /// Switches WillBePublished in the open scene asset (Default ↔ Custom)
    /// And saves the scene. Determines which clip set lands in the build.
    /// </summary>
    [MenuItem("Darts/Audio/Verwendetes Set umschalten (Default/Custom)")]
    public static void ToggleActiveSet()
    {
        var libs = FindLibrariesInOpenScenes();

        if (libs.Count == 0)
        {
            Debug.LogWarning("[AUDIO] Kein AudioLibrary in den geöffneten Szenen gefunden.");
            return;
        }

        bool target = !libs[0].WillBePublished;

        foreach (var lib in libs)
        {
            lib.WillBePublished = target;
            UnityEditor.EditorUtility.SetDirty(lib);
        }

        // Secure scene(s) so that the build uses the new set.
        foreach (var scene in GetOpenScenes())
        {
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            if (EditorSceneManager.MarkSceneDirty(scene))
                EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[AUDIO] WillBePublished = {target} → aktives Set: {(target ? "Default" : "Custom")}.");
    }

    /// <summary>Info-Log: Which clips are wired, which set is active?</summary>
    [MenuItem("Darts/Audio/AudioLibrary-Status anzeigen")]
    public static void ShowStatus()
    {
        var libs = FindLibrariesInOpenScenes();

        if (libs.Count == 0)
        {
            Debug.LogWarning("[AUDIO] Kein AudioLibrary in den geöffneten Szenen gefunden.");
            return;
        }

        foreach (var lib in libs)
        {
            int defaultClips = CountClips(lib.numberClips) + CountSingles(lib) ;
            int customClips = CountClips(lib.customNumberClips) + CountCustomLists(lib);

            Debug.Log($"[AUDIO] AudioLibrary '{lib.name}': " +
                $"WillBePublished={lib.WillBePublished} (aktiv: {(lib.WillBePublished ? "Default" : "Custom")}), " +
                $"Default-Set={defaultClips} Clips, Custom-Set={customClips} Clips.");
        }
    }

    private static int CountSingles(AudioLibrary lib)
    {
        int n = 0;
        if (lib.schnapszahlClip != null) n++;
        if (lib.threeOnesClip != null) n++;
        if (lib.wallClip != null) n++;
        if (lib.ceilingClip != null) n++;
        if (lib.allMissClip != null) n++;
        if (lib.startGameClip != null) n++;
        if (lib.bustClip != null) n++;
        if (lib.fourTwentyClip != null) n++;
        if (lib.undoClip != null) n++;
        if (lib.matchWonClip != null) n++;
        if (lib.suddenDeathClip != null) n++;
        if (lib.cricketMarksClip != null) n++;
        if (lib.atcHitsClip != null) n++;
        if (lib.atcStreakClip != null) n++;
        return n;
    }

    private static int CountCustomLists(AudioLibrary lib)
    {
        int n = 0;
        n += lib.customSchnapszahlClip?.Count ?? 0;
        n += lib.customThreeOnesClip?.Count ?? 0;
        n += lib.customWallClip?.Count ?? 0;
        n += lib.customCeilingClip?.Count ?? 0;
        n += lib.customAllMissClip?.Count ?? 0;
        n += lib.customStartGameClip?.Count ?? 0;
        n += lib.customBustClip?.Count ?? 0;
        n += lib.customFourTwentyClip?.Count ?? 0;
        n += lib.customUndoClip?.Count ?? 0;
        n += lib.customMatchWonClip?.Count ?? 0;
        n += lib.customSuddenDeathClip?.Count ?? 0;
        n += lib.customCricketMarksClip?.Count ?? 0;
        n += lib.customAtcHitsClip?.Count ?? 0;
        n += lib.customAtcStreakClip?.Count ?? 0;
        n += lib.customBahClip?.Count ?? 0;
        n += lib.customThreeTwentiesClip?.Count ?? 0;
        n += lib.customThreeNineteensClip?.Count ?? 0;
        return n;
    }

    private static List<AudioLibrary> FindLibrariesInOpenScenes()
    {
        var result = new List<AudioLibrary>();

        foreach (var scene in GetOpenScenes())
        {
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<AudioLibrary>(true));
        }

        return result;
    }

    private static Scene[] GetOpenScenes()
    {
        int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
        var scenes = new Scene[count];

        for (int i = 0; i < count; i++)
            scenes[i] = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);

        return scenes;
    }
}
