using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Category of an audio event (page 2 of the customization screen).
/// </summary>
public enum AudioEventCategory
{
    Global,
    X01,
    Cricket,
    ATC
}

/// <summary>
/// Trigger type of an audio event.
/// Manual = direct call in the code (Undo, MatchWon,...), no auto-evaluation.
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
/// Trigger parameters. Depending on the child, score / pattern+ matchOrder /
/// threshold evaluated. Thresholds are >= (exact: ScoreReached).
/// </summary>
[System.Serializable]
public class AudioEventTrigger
{
    [JsonProperty] public AudioEventTriggerKind kind = AudioEventTriggerKind.ScoreReached;
    [JsonProperty] public int score;
    [JsonProperty] public List<TurnPatternEntry> pattern = new List<TurnPatternEntry>();

    /// <summary>
    /// Turn patterns: true = order must be correct (slot i corresponds to
    /// Throw i, false = order no matter.
    /// The number of slots is equal to the number of slots – identical to the semantics of the
    /// Custom-Penalties (PenaltyCondition.matchOrder).
    /// </summary>
    [JsonProperty] public bool matchOrder = false;

    [JsonProperty] public int threshold;
}

/// <summary>
/// Audio event: triggerable sound event, clip directly over the row.
/// Built-in seeds (Undo, Bust, Marks,...) are not erasable (builtIn ),
/// but enabled-/trigger-editable and are evaluated by the engine.
/// User-like seeds are erasable (builtIn =false) and have no
/// built-in clip fallback.
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
    [JsonProperty] public List<AudioEventTrigger> triggers = new List<AudioEventTrigger>();

    public static AudioEventDef CreateNew(AudioEventCategory category)
    {
        return new AudioEventDef
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = "New event",
            builtIn = false,
            enabled = true,
            category = category,
            triggers = new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.ScoreReached } }
        };
    }
}

/// <summary>
/// Fixed events (display fallback if the store is empty).
/// The seeds in SeededAudioEvents (IDs are identical) are decisive.
/// Note: ScoreReached means the score reached after the turn — in X01 the
/// The total total in cricket, the seeded 420 trigger is global
/// And you'll fire in both modes.
/// and AudioLibrary.GetScoreClip.
/// </summary>
public static class AudioEventCatalog
{
    private static List<AudioEventDef> cachedBuiltIns;

    /// <summary>
    /// Returns the cached build-ins (specials only, no scores).
    /// Called per refresh — no cache unnecessary allocations per save.
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
        list.Add(BuiltIn("menu_leg_end", "Leg End", AudioEventCategory.Global, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("menu_set_end", "Set End", AudioEventCategory.Global, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("x01_bust", "Bust", AudioEventCategory.X01, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("x01_suddendeath", "Sudden Death", AudioEventCategory.X01, AudioEventTriggerKind.Manual));
        list.Add(BuiltIn("x01_420", "420", AudioEventCategory.Global, AudioEventTriggerKind.ScoreReached, score: 420));
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
            triggers = new List<AudioEventTrigger> { new AudioEventTrigger { kind = kind, score = score } }
        };
        def.info = CustomizationInfo.BuildAudioEvent(def);
        return def;
    }
}
