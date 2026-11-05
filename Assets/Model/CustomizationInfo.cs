using System.Collections.Generic;
using System.Linq;

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
        if (def?.conditions == null || def.conditions.Count == 0)
            return "Penalty";

        var parts = new List<string>(def.conditions.Count);
        foreach (var c in def.conditions)
        {
            if (c == null)
                continue;

            switch (c.kind)
            {
                case PenaltyConditionKind.HitTypeMiss:
                    parts.Add(c.hitType switch
                    {
                        HitType.Wall => "Penalty on wall hit",
                        HitType.Ceiling => "Penalty on ceiling hit",
                        HitType.Custom => "Penalty on custom miss",
                        _ => $"Penalty on {c.hitType}"
                    });
                    break;

                case PenaltyConditionKind.ExactScore:
                    parts.Add($"Turn score: {c.score}");
                    break;

                case PenaltyConditionKind.RemainingExactScore:
                    parts.Add($"Reach exact score: {c.score}");
                    break;

                case PenaltyConditionKind.RepdigitScore:
                    parts.Add($"Repdigit turn score \u2265 {c.minScore}");
                    break;

                case PenaltyConditionKind.TurnPattern:
                    parts.Add($"Turn: {DescribePattern(c.pattern)}"
                        + (c.matchOrder ? " (in order)" : " (any order)"));
                    break;

                case PenaltyConditionKind.LegLost:
                    parts.Add("When a leg is lost");
                    break;

                case PenaltyConditionKind.SetLost:
                    parts.Add("When a set is lost");
                    break;

                case PenaltyConditionKind.GameLost:
                    parts.Add("When the game is lost");
                    break;

                default:
                    parts.Add("Penalty");
                    break;
            }
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "Penalty";
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
        if (def?.triggers == null || def.triggers.Count == 0)
            return "Manual event";

        var parts = new List<string>(def.triggers.Count);
        foreach (var t in def.triggers)
        {
            if (t == null)
                continue;

            switch (t.kind)
            {
                case AudioEventTriggerKind.Manual:
                    parts.Add(ManualInfo(def.id));
                    break;

                case AudioEventTriggerKind.ScoreReached:
                    parts.Add($"Reach exact score: {t.score}");
                    break;

                case AudioEventTriggerKind.TurnPattern:
                    parts.Add($"Turn: {DescribePattern(t.pattern)}"
                        + (t.matchOrder ? " (in order)" : " (any order)"));
                    break;

                case AudioEventTriggerKind.MarksThreshold:
                    parts.Add($"Marks \u2265 {t.threshold}");
                    break;

                case AudioEventTriggerKind.PointsReached:
                    parts.Add($"Points \u2265 {t.threshold}");
                    break;

                case AudioEventTriggerKind.HitsInTurnThreshold:
                    parts.Add($"Hits per turn \u2265 {t.threshold}");
                    break;

                case AudioEventTriggerKind.StreakReached:
                    parts.Add($"Streak \u2265 {t.threshold}");
                    break;

                default:
                    parts.Add("Manual event");
                    break;
            }
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "Manual event";
    }

    /// <summary>Fixed functional descriptions for built-in manual events.</summary>
    private static string ManualInfo(string id)
    {
        switch (id)
        {
            case "menu_undo": return "Sound on undo";
            case "menu_matchwon": return "Sound when the match is won";
            case "menu_startgame": return "Sound at match start";
            case "menu_leg_end": return "Sound when a leg ends";
            case "menu_set_end": return "Sound when a set ends";
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


    public static string LocalizeInfo(string info)
    {
        if (string.IsNullOrEmpty(info))
            return info;

        if (info.StartsWith("Turn score: "))
            return LocalizationText.Format("Turn_score_value", "Turn score: {0}", info.Substring("Turn score: ".Length));
        if (info.StartsWith("Reach exact score: "))
            return LocalizationText.Format("Reach_exact_score_value", "Reach exact score: {0}", info.Substring("Reach exact score: ".Length));
        if (info.StartsWith("Repdigit turn score \u2265 "))
            return LocalizationText.Format("Repdigit_turn_score_min", "Repdigit turn score \u2265 {0}", info.Substring("Repdigit turn score \u2265 ".Length));
        if (info.StartsWith("Marks \u2265 "))
            return LocalizationText.Format("Marks_at_least", "Marks \u2265 {0}", info.Substring("Marks \u2265 ".Length));
        if (info.StartsWith("Points \u2265 "))
            return LocalizationText.Format("Points_at_least", "Points \u2265 {0}", info.Substring("Points \u2265 ".Length));
        if (info.StartsWith("Hits per turn \u2265 "))
            return LocalizationText.Format("Hits_per_turn_at_least", "Hits/Turn \u2265 {0}", info.Substring("Hits per turn \u2265 ".Length));
        if (info.StartsWith("Streak \u2265 "))
            return LocalizationText.Format("Streak_at_least", "Streak \u2265 {0}", info.Substring("Streak \u2265 ".Length));

        if (info.StartsWith("Turn: "))
        {
            string pattern = info.Substring("Turn: ".Length);
            if (pattern.EndsWith(" (in order)"))
                return LocalizationText.Format("Turn_pattern_order", "Turn [{0}] (in order)", pattern.Substring(0, pattern.Length - " (in order)".Length));
            if (pattern.EndsWith(" (any order)"))
                return LocalizationText.Format("Turn_pattern_any_order", "Turn [{0}] (any order)", pattern.Substring(0, pattern.Length - " (any order)".Length));
        }

        if (info.StartsWith("Custom area (") && info.EndsWith(")"))
        {
            string value = info.Substring("Custom area (".Length, info.Length - "Custom area (".Length - 1);
            string mult = value.StartsWith("D") || value.StartsWith("T") ? value.Substring(0, 1) : "";
            string number = mult.Length > 0 ? value.Substring(1) : value;
            return LocalizationText.Format("Custom_area_mult_value", "Custom area ({0}{1})", mult, number);
        }

        return LocalizationText.GetByEnglish(info, info);
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
