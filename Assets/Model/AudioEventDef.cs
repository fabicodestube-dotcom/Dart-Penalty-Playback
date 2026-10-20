using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Kategorie eines Audio-Events (Seite 2 des Customization-Screens).
/// </summary>
public enum AudioEventCategory
{
    Global,
    X01,
    Cricket,
    ATC
}

/// <summary>
/// Auslöser-Art eines Audio-Events.
/// Manual = direkter Aufruf im Code (Undo, MatchWon, ...), keine Auto-Auswertung.
/// </summary>
public enum AudioEventTriggerKind
{
    Manual,
    ScoreReached,
    TurnPattern,
    MarksThreshold,
    PointsReached,
    HitsInTurnThreshold,
    StreakReached
}

/// <summary>
/// Auslöser-Parameter. Je nach Kind werden score / pattern+matchExact /
/// threshold ausgewertet. Schwellen sind >= (exakt: ScoreReached).
/// </summary>
[System.Serializable]
public class AudioEventTrigger
{
    [JsonProperty] public AudioEventTriggerKind kind = AudioEventTriggerKind.ScoreReached;
    [JsonProperty] public int score;
    [JsonProperty] public List<TurnPatternEntry> pattern = new List<TurnPatternEntry>();
    [JsonProperty] public bool matchExact = true;
    [JsonProperty] public int threshold;
}

/// <summary>
/// Audio-Event: auslösbarer Sound-Anlass, Clip direkt über die Row.
/// Seeds (Undo, Bust, Marks, ...) sind nicht löschbar (builtIn), aber
/// enabled-/trigger-editierbar und werden von der Engine ausgewertet.
/// </summary>
[System.Serializable]
public class AudioEventDef
{
    [JsonProperty] public string id;
    [JsonProperty] public string name = "New event";

    /// <summary>
    /// Generated English description of the trigger (fixed function text for
    /// manual built-ins, settings summary otherwise). Set on seed/save and
    /// persisted for the row info text.
    /// </summary>
    [JsonProperty] public string info;

    [JsonProperty] public bool builtIn;
    [JsonProperty] public bool enabled = true;
    [JsonProperty] public AudioEventCategory category = AudioEventCategory.Global;
    [JsonProperty] public AudioEventTrigger trigger = new AudioEventTrigger();

    public static AudioEventDef CreateNew(AudioEventCategory category)
    {
        return new AudioEventDef
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = "New event",
            builtIn = false,
            enabled = true,
            category = category,
            trigger = new AudioEventTrigger { kind = AudioEventTriggerKind.ScoreReached }
        };
    }
}

/// <summary>
/// Fest eingebaute Events (Anzeige-Fallback, falls der Store leer ist).
/// Maßgeblich sind die Seeds in SeededAudioEvents (IDs identisch).
/// Hinweis: ScoreReached meint immer den Turn-Score (Summe der 3 Darts einer
/// Aufnahme), nicht die Gesamtpunktzahl. Der geseedete Score-Trigger ist die
/// 420 (X01, liegt außerhalb des 0-180-Score-Grids); generische Scores 0-180
/// gibt es hier bewusst nicht — Score-Ansagen laufen über die Score-Sammel-Row
/// und AudioLibrary.GetScoreClip.
/// </summary>
public static class AudioEventCatalog
{
    private static List<AudioEventDef> cachedBuiltIns;

    /// <summary>
    /// Gibt die gecachten Built-ins zurück (nur Specials, keine Scores).
    /// Wird pro Refresh aufgerufen — ohne Cache unnötige Allokationen je Save.
    /// </summary>
    public static List<AudioEventDef> BuildBuiltIns()
    {
        if (cachedBuiltIns != null)
            return cachedBuiltIns;

        cachedBuiltIns = BuildBuiltInsUncached();
        return cachedBuiltIns;
    }

    private static List<AudioEventDef> BuildBuiltInsUncached()
    {
        var list = new List<AudioEventDef>();

        list.Add(BuiltIn("menu_undo", "Undo", AudioEventCategory.Global, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("menu_matchwon", "Game Over", AudioEventCategory.Global, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("menu_startgame", "Game Start", AudioEventCategory.Global, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("x01_bust", "Bust", AudioEventCategory.X01, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("x01_suddendeath", "Sudden Death", AudioEventCategory.X01, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("x01_420", "420", AudioEventCategory.X01, AudioEventTriggerKind.ScoreReached, score: 420));
        list.Add(BuiltIn("cricket_marks", "Marks", AudioEventCategory.Cricket, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("cricket_whitehorse", "White Horse", AudioEventCategory.Cricket, AudioEventTriggerKind.TurnPattern));
        list.Add(BuiltIn("cricket_ninemarks", "9 Marks", AudioEventCategory.Cricket, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("atc_hits", "Hits", AudioEventCategory.ATC, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("atc_streak", "ATC Streak", AudioEventCategory.ATC, AudioEventTriggerKind.Manual));

        return list;
    }

    private static AudioEventDef BuiltIn(string id, string name, AudioEventCategory category, AudioEventTriggerKind kind, int score = 0)
    {
        var def = new AudioEventDef
        {
            id = id,
            name = name,
            builtIn = true,
            category = category,
            trigger = new AudioEventTrigger { kind = kind, score = score }
        };
        def.info = CustomizationInfo.BuildAudioEvent(def);
        return def;
    }
}
