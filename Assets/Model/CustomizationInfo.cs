using System.Collections.Generic;

/// <summary>
/// Builds short English "info" descriptions for customization rows from the
/// current definition settings. Used both for seeding built-ins and for every
/// user save, so the persisted info always reflects the actual configuration.
/// Fixed functional texts are used for manual built-in audio events; all
/// parametrized penalties/events and custom sectors are described from their
/// settings.
/// </summary>
public static class CustomizationInfo
{
    public static string BuildPenalty(CustomPenaltyDef def)
    {
        if (def?.condition == null)
            return "Penalty";

        var c = def.condition;
        switch (c.kind)
        {
            case PenaltyConditionKind.HitTypeMiss:
                return c.hitType switch
                {
                    HitType.Wall => "Penalty on wall hit",
                    HitType.Ceiling => "Penalty on ceiling hit",
                    HitType.Custom => "Penalty on custom miss",
                    _ => $"Penalty on {c.hitType}"
                };

            case PenaltyConditionKind.ExactScore:
                return $"Turn score: {c.score}";

            case PenaltyConditionKind.RemainingExactScore:
                return $"Reach exact score: {c.score}";

            case PenaltyConditionKind.RepdigitScore:
                return $"Repdigit turn score \u2265 {c.minScore}";

            case PenaltyConditionKind.TurnPattern:
                return $"Turn: {DescribePattern(c.pattern)}"
                    + (c.matchOrder ? " (in order)" : " (any order)");

            case PenaltyConditionKind.LegLost:
                return "When a leg is lost";

            case PenaltyConditionKind.SetLost:
                return "When a set is lost";

            case PenaltyConditionKind.GameLost:
                return "When the game is lost";

            default:
                return "Penalty";
        }
    }

    public static string BuildSector(CustomHitDef def)
    {
        string id = def?.id;

        if (string.Equals(id, "builtin:wall", System.StringComparison.OrdinalIgnoreCase))
            return "Hit outside the board (wall)";

        if (string.Equals(id, "builtin:ceiling", System.StringComparison.OrdinalIgnoreCase))
            return "Hit the ceiling";

        if (def != null && def.value > 0)
        {
            string mult = def.multiplier switch
            {
                DartMultiplier.Double => "D",
                DartMultiplier.Triple => "T",
                _ => ""
            };
            return $"Custom area ({mult}{def.value})";
        }

        return "Custom out-of-board area (0 points)";
    }

    public static string BuildAudioEvent(AudioEventDef def)
    {
        if (def?.trigger == null)
            return "Manual event";

        var t = def.trigger;
        switch (t.kind)
        {
            case AudioEventTriggerKind.Manual:
                return ManualInfo(def.id);

            case AudioEventTriggerKind.ScoreReached:
                return $"Reach exact score: {t.score}";

            case AudioEventTriggerKind.TurnPattern:
                return $"Turn: {DescribePattern(t.pattern)}"
                    + (t.matchOrder ? " (in order)" : " (any order)");

            case AudioEventTriggerKind.MarksThreshold:
                return $"Marks \u2265 {t.threshold}";

            case AudioEventTriggerKind.PointsReached:
                return $"Points \u2265 {t.threshold}";

            case AudioEventTriggerKind.HitsInTurnThreshold:
                return $"Hits per turn \u2265 {t.threshold}";

            case AudioEventTriggerKind.StreakReached:
                return $"Streak \u2265 {t.threshold}";

            default:
                return "Manual event";
        }
    }

    /// <summary>Fixed functional descriptions for built-in manual events.</summary>
    private static string ManualInfo(string id)
    {
        switch (id)
        {
            case "menu_undo": return "Sound on undo";
            case "menu_matchwon": return "Sound when the match is won";
            case "menu_startgame": return "Sound at match start";
            case "x01_bust": return "Sound when overthrow (bust)";
            case "x01_suddendeath": return "Sound on sudden death";
            case "cricket_marks": return "Sound after Cricket marks";
            case "cricket_whitehorse": return "Sound on white horse";
            case "cricket_ninemarks": return "Sound on 9 marks";
            case "atc_hits": return "Sound on hits";
            case "atc_streak": return "Sound on ATC streak";
            default: return "Manual event";
        }
    }

    /// <summary>Compact turn-pattern description (e.g. "T20,1,Miss").</summary>
    public static string DescribePattern(List<TurnPatternEntry> pattern)
    {
        if (pattern == null || pattern.Count == 0)
            return "-";

        var parts = new List<string>(pattern.Count);
        foreach (var slot in pattern)
        {
            if (slot == null)
            {
                parts.Add("?");
                continue;
            }

            if (slot.anyMissHitType)
            {
                parts.Add("Miss");
                continue;
            }

            string value = slot.anyValue ? "*" : slot.value.ToString();
            string mult = slot.anyMultiplier ? "" : slot.multiplier switch
            {
                DartMultiplier.Single => "S",
                DartMultiplier.Double => "D",
                DartMultiplier.Triple => "T",
                _ => ""
            };
            string hit = slot.anyHitType || slot.hitType == HitType.Board ? ""
                : slot.hitType == HitType.Custom ? "@custom"
                : "@" + slot.hitType;

            parts.Add(string.IsNullOrEmpty(mult) ? value + hit : mult + value + hit);
        }

        return string.Join(",", parts);
    }
}
