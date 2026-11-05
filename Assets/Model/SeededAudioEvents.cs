using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Geseedete Audio-Events (Defaults ab erstem Start). Built-in-Seeds sind
/// nicht löschbar (builtIn=true) und haben einen Fallback-Sound. Einige
/// Seeds sind dagegen bewusst user-ähnlich (builtIn=false, löschbar) und
/// werden nur im Custom-Paket einmalig angelegt; ihre Clips kommen
/// ausschließlich aus der normalen Event-Aufnahme.
/// </summary>
public static class SeededAudioEvents
{
    public const string UndoId = "menu_undo";
    public const string GameOverId = "menu_matchwon";
    public const string GameStartId = "menu_startgame";
    public const string LegEndId = "menu_leg_end";
    public const string SetEndId = "menu_set_end";
    public const string BustId = "x01_bust";
    public const string SuddenDeathId = "x01_suddendeath";
    public const string Score420Id = "x01_420";
    public const string BahId = "x01_bah";
    public const string ThreeTwentiesId = "x01_3x20";
    public const string ThreeNineteensId = "x01_3x19";
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
        public readonly List<AudioEventTrigger> Triggers;
        public readonly SpecialAudioType? FallbackSpecial;
        public readonly int? FallbackScore;
        public readonly bool BuiltIn;
        public readonly bool OnlyCustomAudioSet;

        public Seed(string id, string name, AudioEventCategory category,
            List<AudioEventTrigger> triggers, SpecialAudioType? fallbackSpecial = null,
            int? fallbackScore = null, bool builtIn = true,
            bool onlyCustomAudioSet = false)
        {
            Id = id;
            Name = name;
            Category = category;
            Triggers = triggers ?? new List<AudioEventTrigger>();
            FallbackSpecial = fallbackSpecial;
            FallbackScore = fallbackScore;
            BuiltIn = builtIn;
            OnlyCustomAudioSet = onlyCustomAudioSet;
        }

        public AudioEventDef ToDef()
        {
            var def = new AudioEventDef
            {
                id = Id,
                name = Name,
                builtIn = BuiltIn,
                enabled = true,
                category = Category,
                triggers = Triggers != null && Triggers.Count > 0
                    ? new List<AudioEventTrigger>(Triggers)
                    : new List<AudioEventTrigger>()
            };
            def.info = CustomizationInfo.BuildAudioEvent(def);
            return def;
        }
    }

    public static readonly List<Seed> All = new List<Seed>
    {
        new Seed(UndoId, "Undo", AudioEventCategory.Global,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } },
            fallbackSpecial: SpecialAudioType.Undo),
        new Seed(GameOverId, "Game Over", AudioEventCategory.Global,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } },
            fallbackSpecial: SpecialAudioType.MatchWon),
        new Seed(GameStartId, "Game Start", AudioEventCategory.Global,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } },
            fallbackSpecial: SpecialAudioType.StartGame),
        new Seed(LegEndId, "Leg End", AudioEventCategory.Global,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } }),
        new Seed(SetEndId, "Set End", AudioEventCategory.Global,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } }),
        new Seed(BustId, "Bust", AudioEventCategory.X01,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } },
            fallbackSpecial: SpecialAudioType.Bust),
        new Seed(SuddenDeathId, "Sudden Death", AudioEventCategory.X01,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } },
            fallbackSpecial: SpecialAudioType.SuddenDeath),
        new Seed(Score420Id, "420", AudioEventCategory.Global,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.ScoreReached, score = 420 } },
            fallbackSpecial: SpecialAudioType.FourTwenty,
            fallbackScore: 420),
        // Marks-Ansage (Cricket): bewusst Manual — die Engine spielt sie nach
        // dem Turn-Score (Reihenfolge: Penalties → Specials → Score → Marks).
        new Seed(MarksId, "Marks", AudioEventCategory.Cricket,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } },
            fallbackSpecial: SpecialAudioType.Marks),
        new Seed(WhiteHorseId, "White Horse", AudioEventCategory.Cricket,
            new List<AudioEventTrigger>
            {
                new AudioEventTrigger
                {
                    kind = AudioEventTriggerKind.TurnPattern,
                    matchOrder = false,
                    pattern = new List<TurnPatternEntry>
                    {
                        TripleSlot(), TripleSlot(), TripleSlot()
                    }
                }
            }),
        new Seed(NineMarksId, "9 Marks", AudioEventCategory.Cricket,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.MarksThreshold, threshold = 9 } }),
        new Seed(HitsId, "Hits", AudioEventCategory.ATC,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.HitsInTurnThreshold, threshold = 1 } },
            fallbackSpecial: SpecialAudioType.Hits),
        new Seed(StreakId, "ATC Streak", AudioEventCategory.ATC,
            new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.StreakReached, threshold = 3 } },
            fallbackSpecial: SpecialAudioType.ATCStreak),

        // User-ähnliche X01-Events: löschbar, ohne eingebauten Clip, nur im
        // Custom-Paket einmalig angelegt. Clips kommen aus der Event-UI.
        new Seed(BahId, "Bah", AudioEventCategory.X01,
            new List<AudioEventTrigger>
            {
                new AudioEventTrigger
                {
                    kind = AudioEventTriggerKind.TurnPattern,
                    matchOrder = false,
                    pattern = new List<TurnPatternEntry>
                    {
                        SingleBoardSlot(1), SingleBoardSlot(5), SingleBoardSlot(20)
                    }
                }
            },
            builtIn: false, onlyCustomAudioSet: true),
        new Seed(ThreeTwentiesId, "3x20", AudioEventCategory.X01,
            new List<AudioEventTrigger>
            {
                new AudioEventTrigger
                {
                    kind = AudioEventTriggerKind.TurnPattern,
                    matchOrder = false,
                    pattern = new List<TurnPatternEntry>
                    {
                        SingleBoardSlot(20), SingleBoardSlot(20), SingleBoardSlot(20)
                    }
                }
            },
            builtIn: false, onlyCustomAudioSet: true),
        new Seed(ThreeNineteensId, "3x19", AudioEventCategory.X01,
            new List<AudioEventTrigger>
            {
                new AudioEventTrigger
                {
                    kind = AudioEventTriggerKind.TurnPattern,
                    matchOrder = false,
                    pattern = new List<TurnPatternEntry>
                    {
                        SingleBoardSlot(19), SingleBoardSlot(19), SingleBoardSlot(19)
                    }
                }
            },
            builtIn: false, onlyCustomAudioSet: true)
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

    /// <summary>Exakter Single-Treffer auf dem Board (z. B. S20).</summary>
    private static TurnPatternEntry SingleBoardSlot(int value)
    {
        return new TurnPatternEntry
        {
            anyValue = false,
            value = value,
            anyMultiplier = false,
            multiplier = DartMultiplier.Single,
            anyHitType = false,
            hitType = HitType.Board
        };
    }

    /// <summary>Einmal-Marker für user-ähnliche Seeds (verhindert Wiederanlage).</summary>
    public static string SeedMarkerKey(string id)
    {
        return "audio_seed_" + id + "_v1";
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
