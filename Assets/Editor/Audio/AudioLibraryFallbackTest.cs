using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prüft die eingebauten Audio-Clips (Default-/Custom-Set) und ihre
/// Verdrahtung im AudioLibrary der App-Szene.
///
/// - Beide Sets sind vollständig verdrahtet (keine "Missing"-Referenzen).
/// - WillBePublished == false → das Custom-Set liefert die Fallback-Clips.
/// - WillBePublished == true  → das Default-Set liefert die Fallback-Clips.
/// - Die eingebauten Clips erscheinen NICHT in den Clip-Counts
///   (sie sind kein User-Recording) — der Customization-Screen zeigt 0.
/// - Die Option "Default-Clips als Fallback" schaltet den eingebauten
///   Rückfall ab (nur eigene Aufnahmen, sonst stumm).
///
/// Menü: Darts/Tests/Audio Fallback.
/// Batchmode: -executeMethod AudioLibraryFallbackTest.Run
/// </summary>
public static class AudioLibraryFallbackTest
{
    private const string ScenePath = "Assets/Scenes/App.unity";

    [MenuItem("Darts/Tests/Audio Fallback")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            if (cond)
            {
                Debug.Log($"  OK   {name}");
            }
            else
            {
                failures++;
                Debug.LogError($"  FAIL {name} {detail}");
            }
        }

        var source = FindLibraryInScene();
        if (source == null)
        {
            Debug.LogError($"[AUDIO-TEST] Kein AudioLibrary in '{ScenePath}' gefunden.");
            EditorApplication.Exit(1);
            return;
        }

        var tempGo = new GameObject("TempAudioLibrary");
        var lib = tempGo.AddComponent<AudioLibrary>();
        CopyInspectorContent(source, lib);

        try
        {
            // ---------- Verdrahtung vollständig? ----------
            Check(lib.numberClips != null && lib.numberClips.Length == 181,
                "default-score-clips-181", $"len={(lib.numberClips?.Length ?? -1)}");
            Check(lib.customNumberClips != null && lib.customNumberClips.Length == 181,
                "custom-score-clips-181", $"len={(lib.customNumberClips?.Length ?? -1)}");

            Check(lib.numberClips != null && lib.numberClips[0] != null && lib.numberClips[180] != null,
                "default-score-clips-0-180");
            Check(lib.customNumberClips != null && lib.customNumberClips[0] != null && lib.customNumberClips[180] != null,
                "custom-score-clips-0-180");

            Check(lib.wallClip != null && lib.ceilingClip != null && lib.allMissClip != null
                  && lib.schnapszahlClip != null && lib.threeOnesClip != null,
                "default-penalty-clips");
            Check(lib.startGameClip != null && lib.bustClip != null && lib.fourTwentyClip != null
                  && lib.undoClip != null && lib.matchWonClip != null && lib.suddenDeathClip != null
                  && lib.cricketMarksClip != null && lib.atcHitsClip != null && lib.atcStreakClip != null,
                "default-special-clips");

            Check(Count(lib.customWallClip) == 8 && Count(lib.customCeilingClip) == 5
                  && Count(lib.customThreeOnesClip) == 3 && Count(lib.customAllMissClip) == 5
                  && Count(lib.customSchnapszahlClip) == 4 && Count(lib.customUndoClip) == 9
                  && Count(lib.customAtcStreakClip) == 2,
                "custom-lists",
                $"wall={Count(lib.customWallClip)} ceiling={Count(lib.customCeilingClip)} " +
                $"threeOnes={Count(lib.customThreeOnesClip)} allMiss={Count(lib.customAllMissClip)} " +
                $"schnapszahl={Count(lib.customSchnapszahlClip)} undo={Count(lib.customUndoClip)} " +
                $"atcStreak={Count(lib.customAtcStreakClip)}");

            // ---------- WillBePublished == false → Custom-Set ----------
            lib.WillBePublished = false;

            Check(lib.GetBuiltInScoreClip(42) == lib.customNumberClips[42], "custom-set-score-42");
            Check(lib.GetBuiltInScoreClip(0) == lib.customNumberClips[0], "custom-set-score-0");
            Check(lib.GetBuiltInScoreClip(180) == lib.customNumberClips[180], "custom-set-score-180");
            Check(lib.GetBuiltInScoreClip(999) == null, "custom-set-score-out-of-range");

            Check(InList(lib.GetBuiltInClipForKey("custom_wall"), lib.customWallClip), "custom-set-wall");
            Check(InList(lib.GetBuiltInClipForKey("custom_ceiling"), lib.customCeilingClip), "custom-set-ceiling");
            Check(InList(lib.GetBuiltInClipForKey("builtin:allmiss"), lib.customAllMissClip), "custom-set-allmiss");
            Check(InList(lib.GetBuiltInClipForKey("builtin:threeones"), lib.customThreeOnesClip), "custom-set-threeones");
            Check(InList(lib.GetBuiltInClipForKey("builtin:score111"), lib.customSchnapszahlClip), "custom-set-schnapszahl");
            Check(InList(lib.GetBuiltInClipForKey("menu_undo"), lib.customUndoClip), "custom-set-undo");
            Check(InList(lib.GetBuiltInClipForKey("menu_startgame"), lib.customStartGameClip), "custom-set-startgame");
            Check(InList(lib.GetBuiltInClipForKey("menu_matchwon"), lib.customMatchWonClip), "custom-set-matchwon");
            Check(InList(lib.GetBuiltInClipForKey("x01_bust"), lib.customBustClip), "custom-set-bust");
            Check(InList(lib.GetBuiltInClipForKey("x01_suddendeath"), lib.customSuddenDeathClip), "custom-set-suddendeath");
            Check(InList(lib.GetBuiltInClipForKey("x01_420"), lib.customFourTwentyClip), "custom-set-420");
            Check(InList(lib.GetBuiltInClipForKey("cricket_marks"), lib.customCricketMarksClip), "custom-set-marks");
            Check(InList(lib.GetBuiltInClipForKey("atc_hits"), lib.customAtcHitsClip), "custom-set-hits");
            Check(InList(lib.GetBuiltInClipForKey("atc_streak"), lib.customAtcStreakClip), "custom-set-atcstreak");

            // Ohne eingebauten Clip: eigene Events/Sektoren bleiben stumm.
            Check(lib.GetBuiltInClipForKey("eigene-event-id") == null, "custom-set-unknown-id-null");
            Check(lib.GetBuiltInClipForKey("builtin:wall") == null, "custom-set-sector-id-null");

            // ---------- WillBePublished == true → Default-Set ----------
            lib.WillBePublished = true;

            Check(lib.GetBuiltInScoreClip(42) == lib.numberClips[42], "default-set-score-42");
            Check(lib.GetBuiltInScoreClip(180) == lib.numberClips[180], "default-set-score-180");
            Check(lib.GetBuiltInScoreClip(999) == null, "default-set-score-out-of-range");

            Check(lib.GetBuiltInClipForKey("custom_wall") == lib.wallClip, "default-set-wall");
            Check(lib.GetBuiltInClipForKey("custom_ceiling") == lib.ceilingClip, "default-set-ceiling");
            Check(lib.GetBuiltInClipForKey("builtin:allmiss") == lib.allMissClip, "default-set-allmiss");
            Check(lib.GetBuiltInClipForKey("builtin:threeones") == lib.threeOnesClip, "default-set-threeones");
            Check(lib.GetBuiltInClipForKey("builtin:score111") == lib.schnapszahlClip, "default-set-schnapszahl");
            Check(lib.GetBuiltInClipForKey("menu_undo") == lib.undoClip, "default-set-undo");
            Check(lib.GetBuiltInClipForKey("menu_startgame") == lib.startGameClip, "default-set-startgame");
            Check(lib.GetBuiltInClipForKey("menu_matchwon") == lib.matchWonClip, "default-set-matchwon");
            Check(lib.GetBuiltInClipForKey("x01_bust") == lib.bustClip, "default-set-bust");
            Check(lib.GetBuiltInClipForKey("x01_suddendeath") == lib.suddenDeathClip, "default-set-suddendeath");
            Check(lib.GetBuiltInClipForKey("x01_420") == lib.fourTwentyClip, "default-set-420");
            Check(lib.GetBuiltInClipForKey("cricket_marks") == lib.cricketMarksClip, "default-set-marks");
            Check(lib.GetBuiltInClipForKey("atc_hits") == lib.atcHitsClip, "default-set-hits");
            Check(lib.GetBuiltInClipForKey("atc_streak") == lib.atcStreakClip, "default-set-atcstreak");

            // ---------- Eingebaute Clips erscheinen NICHT in den Counts ----------
            Check(lib.GetCustomClipCount("custom_wall") == 0, "count-wall-0");
            Check(lib.GetCustomClipCount("menu_undo") == 0, "count-undo-0");
            Check(lib.GetCustomClipCount("x01_420") == 0, "count-420-0");
            Check(lib.GetRuntimeScoreList(42) == null, "runtime-score-42-empty");
            Check(lib.GetRuntimeCustomList("custom_wall") == null, "runtime-custom-wall-empty");

            // ---------- Option: Default-Clips als Fallback ----------
            // Kette: eigene Aufnahme → eingebauter Fallback → null.
            var defaultClip = lib.numberClips[42];
            var customClip = lib.customNumberClips[42];

            Check(AudioLibrary.ResolveClip(null, () => defaultClip, true) == defaultClip,
                "fallback-on-builtin-used");
            Check(AudioLibrary.ResolveClip(null, () => defaultClip, false) == null,
                "fallback-off-no-user-clip-silent");
            Check(AudioLibrary.ResolveClip(customClip, () => defaultClip, false) == customClip,
                "fallback-off-user-clip-wins");
            Check(AudioLibrary.ResolveClip(customClip, () => defaultClip, true) == customClip,
                "fallback-on-user-clip-first");
            Check(AudioLibrary.ResolveClip(null, null, true) == null,
                "fallback-on-no-builtin-null");
            Check(AudioLibrary.ResolveClip(null, null, false) == null,
                "fallback-off-no-builtin-null");
        }
        finally
        {
            Object.DestroyImmediate(tempGo);
        }

        if (failures == 0)
        {
            Debug.Log("[AUDIO-TEST] Alle Prüfungen bestanden.");
        }
        else
        {
            Debug.LogError($"[AUDIO-TEST] {failures} Prüfung(en) fehlgeschlagen.");
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static AudioLibrary FindLibraryInScene()
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ScenePath))
        {
            if (!(obj is GameObject go))
                continue;

            var found = go.GetComponentInChildren<AudioLibrary>(true);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void CopyInspectorContent(AudioLibrary src, AudioLibrary dst)
    {
        dst.WillBePublished = src.WillBePublished;
        dst.numberClips = src.numberClips;
        dst.customNumberClips = src.customNumberClips;

        dst.schnapszahlClip = src.schnapszahlClip;
        dst.threeOnesClip = src.threeOnesClip;
        dst.wallClip = src.wallClip;
        dst.ceilingClip = src.ceilingClip;
        dst.allMissClip = src.allMissClip;

        dst.startGameClip = src.startGameClip;
        dst.bustClip = src.bustClip;
        dst.fourTwentyClip = src.fourTwentyClip;
        dst.undoClip = src.undoClip;
        dst.matchWonClip = src.matchWonClip;
        dst.suddenDeathClip = src.suddenDeathClip;
        dst.cricketMarksClip = src.cricketMarksClip;
        dst.atcHitsClip = src.atcHitsClip;
        dst.atcStreakClip = src.atcStreakClip;

        dst.customSchnapszahlClip = src.customSchnapszahlClip;
        dst.customThreeOnesClip = src.customThreeOnesClip;
        dst.customWallClip = src.customWallClip;
        dst.customCeilingClip = src.customCeilingClip;
        dst.customAllMissClip = src.customAllMissClip;

        dst.customStartGameClip = src.customStartGameClip;
        dst.customBustClip = src.customBustClip;
        dst.customFourTwentyClip = src.customFourTwentyClip;
        dst.customUndoClip = src.customUndoClip;
        dst.customMatchWonClip = src.customMatchWonClip;
        dst.customSuddenDeathClip = src.customSuddenDeathClip;
        dst.customCricketMarksClip = src.customCricketMarksClip;
        dst.customAtcHitsClip = src.customAtcHitsClip;
        dst.customAtcStreakClip = src.customAtcStreakClip;
    }

    private static int Count(List<AudioClip> list) => list?.Count ?? 0;

    private static bool InList(AudioClip clip, List<AudioClip> list)
    {
        if (clip == null || list == null)
            return false;

        return list.Contains(clip);
    }
}
