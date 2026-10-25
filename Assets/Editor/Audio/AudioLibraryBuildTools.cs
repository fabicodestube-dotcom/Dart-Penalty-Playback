using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Build-Hilfen für die eingebauten Audio-Clips.
///
/// Der AudioLibrary hält im Inspector zwei Clip-Sets: Default (veröffentlicht)
/// und Custom (Entwicklung). Welches Set zur Laufzeit als eingebauter
/// Fallback dient, entscheidet WillBePublished. Damit nicht beide Sätze
/// (419 Clips, ~115 MB Roh-WAV) in den Build wandern, entfernt der
/// Szenen-Processor das inaktive Set aus der Build-Kopie der Szene.
/// Das Quell-Szenen-Asset bleibt unberührt.
///
/// Menü: Darts/Audio/...
/// </summary>
public class AudioLibraryBuildTools : IProcessSceneWithReport
{
    /// <summary>Reihenfolge: nach den üblichen Processoren laufen.</summary>
    public int callbackOrder => 100;

    /// <summary>Anzahl der Clips, die beim letzten Build entfernt wurden.</summary>
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
    /// Entfernt das Set, das laut WillBePublished nicht verwendet wird.
    /// Rückgabe: Anzahl entfernter Clips.
    /// </summary>
    private static int StripInactiveSet(AudioLibrary lib)
    {
        if (lib == null)
            return 0;

        int removed = 0;

        if (lib.WillBePublished)
        {
            // Default-Set ist aktiv → Custom-Set entfernen.
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
        }
        else
        {
            // Custom-Set ist aktiv → Default-Set entfernen.
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
    // MENÜ
    // =========================================================

    /// <summary>
    /// Schaltet WillBePublished im geöffneten Szenen-Asset um (Default ↔ Custom)
    /// und speichert die Szene. Bestimmt, welches Clip-Set im Build landet.
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

        // Szene(n) sichern, damit der Build das neue Set verwendet.
        foreach (var scene in GetOpenScenes())
        {
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            if (EditorSceneManager.MarkSceneDirty(scene))
                EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[AUDIO] WillBePublished = {target} → aktives Set: {(target ? "Default" : "Custom")}.");
    }

    /// <summary>Info-Log: Welche Clips sind verdrahtet, welches Set ist aktiv?</summary>
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
