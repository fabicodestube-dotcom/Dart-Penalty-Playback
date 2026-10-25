using System.Collections.Generic;

/// <summary>
/// Geseedete Audio-Events (Defaults ab erstem Start): echte Defs mit
/// builtIn=true (nicht löschbar, aber enabled-/trigger-editierbar und von
/// der Engine ausgewertet), mit hinterlegtem Fallback-Sound, damit jeder
/// Seed ab Tag 1 exakt so klingt wie der bisher hart verdrahtete Pfad.
/// Clips werden direkt über die Event-Row aufgenommen (stabiler String-Key).
/// </summary>
public static class SeededAudioEvents
{
    public const string UndoId = "menu_undo";
    public const string GameOverId = "menu_matchwon";
    public const string GameStartId = "menu_startgame";
    public const string BustId = "x01_bust";
    public const string SuddenDeathId = "x01_suddendeath";
    public const string Score420Id = "x01_420";
    public const string MarksId = "cricket_marks";
    public const string WhiteHorseId = "cricket_whitehorse";
    public const string NineMarksId = "cricket_ninemarks";
    public const string HitsId = "atc_hits";
    public const string StreakId = "atc_streak";

    public sealed class Seed
    {
        public readonly string Id;
        public readonly string Name;
        public readonly AudioEventCategory Category;
        public readonly AudioEventTrigger Trigger;
        public readonly SpecialAudioType? FallbackSpecial;
        public readonly int? FallbackScore;

        public Seed(string id, string name, AudioEventCategory category,
            AudioEventTrigger trigger, SpecialAudioType? fallbackSpecial = null,
            int? fallbackScore = null)
        {
            Id = id;
            Name = name;
            Category = category;
            Trigger = trigger;
            FallbackSpecial = fallbackSpecial;
            FallbackScore = fallbackScore;
        }

        public AudioEventDef ToDef()
        {
            var def = new AudioEventDef
            {
                id = Id,
                name = Name,
                builtIn = true,
                enabled = true,
                category = Category,
                trigger = Trigger
            };
            def.info = CustomizationInfo.BuildAudioEvent(def);
            return def;
        }
    }

    public static readonly List<Seed> All = new List<Seed>
    {
        new Seed(UndoId, "Undo", AudioEventCategory.Global,
            new AudioEventTrigger { kind = AudioEventTriggerKind.Manual },
            fallbackSpecial: SpecialAudioType.Undo),
        new Seed(GameOverId, "Game Over", AudioEventCategory.Global,
            new AudioEventTrigger { kind = AudioEventTriggerKind.Manual },
            fallbackSpecial: SpecialAudioType.MatchWon),
        new Seed(GameStartId, "Game Start", AudioEventCategory.Global,
            new AudioEventTrigger { kind = AudioEventTriggerKind.Manual },
            fallbackSpecial: SpecialAudioType.StartGame),
        new Seed(BustId, "Bust", AudioEventCategory.X01,
            new AudioEventTrigger { kind = AudioEventTriggerKind.Manual },
            fallbackSpecial: SpecialAudioType.Bust),
        new Seed(SuddenDeathId, "Sudden Death", AudioEventCategory.X01,
            new AudioEventTrigger { kind = AudioEventTriggerKind.Manual },
            fallbackSpecial: SpecialAudioType.SuddenDeath),
        new Seed(Score420Id, "420", AudioEventCategory.Global,
            new AudioEventTrigger { kind = AudioEventTriggerKind.ScoreReached, score = 420 },
            fallbackSpecial: SpecialAudioType.FourTwenty,
            fallbackScore: 420),
        // Marks-Ansage (Cricket): bewusst Manual — die Engine spielt sie nach
        // dem Turn-Score (Reihenfolge: Penalties → Specials → Score → Marks).
        new Seed(MarksId, "Marks", AudioEventCategory.Cricket,
            new AudioEventTrigger { kind = AudioEventTriggerKind.Manual },
            fallbackSpecial: SpecialAudioType.Marks),
        new Seed(WhiteHorseId, "White Horse", AudioEventCategory.Cricket,
            new AudioEventTrigger
            {
                kind = AudioEventTriggerKind.TurnPattern,
                matchOrder = false,
                pattern = new List<TurnPatternEntry>
                {
                    TripleSlot(), TripleSlot(), TripleSlot()
                }
            }),
        new Seed(NineMarksId, "9 Marks", AudioEventCategory.Cricket,
            new AudioEventTrigger { kind = AudioEventTriggerKind.MarksThreshold, threshold = 9 }),
        new Seed(HitsId, "Hits", AudioEventCategory.ATC,
            new AudioEventTrigger { kind = AudioEventTriggerKind.HitsInTurnThreshold, threshold = 1 },
            fallbackSpecial: SpecialAudioType.Hits),
        new Seed(StreakId, "ATC Streak", AudioEventCategory.ATC,
            new AudioEventTrigger { kind = AudioEventTriggerKind.StreakReached, threshold = 3 },
            fallbackSpecial: SpecialAudioType.ATCStreak)
    };

    /// <summary>Default White-Horse-Slot: beliebiges Triple (3 verschiedene egal).</summary>
    private static TurnPatternEntry TripleSlot()
    {
        return new TurnPatternEntry
        {
            anyValue = true,
            anyMultiplier = false,
            multiplier = DartMultiplier.Triple,
            anyHitType = true
        };
    }

    public static Seed Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (var seed in All)
        {
            if (string.Equals(seed.Id, id, System.StringComparison.OrdinalIgnoreCase))
                return seed;
        }

        return null;
    }
}
