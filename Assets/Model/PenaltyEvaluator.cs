using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Wertet CustomPenaltyDef-Bedingungen aus (rein, testbar, keine Unity-Abhängigkeit).
/// Built-in-Typen (Enum) laufen weiter über die bestehenden Checks.
/// </summary>
public static class PenaltyEvaluator
{
    /// <summary>Instant-Prüfung eines einzelnen Wurfs (nur HitTypeMiss sinnvoll).</summary>
    public static bool MatchesInstant(PenaltyCondition condition, Throw throwItem)
    {
        if (condition == null || throwItem == null)
            return false;

        return condition.kind == PenaltyConditionKind.HitTypeMiss
            && throwItem.HitType == condition.hitType;
    }

    /// <summary>Turn-Ende-Prüfung (ExactScore, RepdigitScore, TurnPattern).</summary>
    public static bool MatchesTurn(PenaltyCondition condition, List<Throw> throws, int turnScore)
    {
        if (condition == null || throws == null)
            return false;

        switch (condition.kind)
        {
            case PenaltyConditionKind.ExactScore:
                return turnScore == condition.score;

            case PenaltyConditionKind.RepdigitScore:
                return turnScore >= condition.minScore && IsRepdigit(turnScore);

            case PenaltyConditionKind.TurnPattern:
                return MatchesPattern(condition.pattern, throws, true, condition.matchOrder);

            case PenaltyConditionKind.HitTypeMiss:
            default:
                return false;
        }
    }

    /// <summary>
    /// Pattern-Match über Würfe. matchOrder=false: reihenfolgenfrei, exakte
    /// Anzahl (bisheriges matchExact=true-Verhalten). matchOrder=true: geordnet
    /// + exakt (Slot i entspricht Wurf i). matchExact=false (nur Audio-Events):
    /// Pattern als Teilmenge, reihenfolgenfrei.
    /// </summary>
    public static bool MatchesPattern(List<TurnPatternEntry> pattern, List<Throw> throws, bool matchExact, bool matchOrder = false)
    {
        if (pattern == null || pattern.Count == 0 || throws == null)
            return false;

        if (matchOrder)
        {
            if (throws.Count != pattern.Count)
                return false;

            for (int i = 0; i < pattern.Count; i++)
            {
                if (!SlotMatchesThrow(pattern[i], throws[i]))
                    return false;
            }

            return true;
        }

        if (matchExact && throws.Count != pattern.Count)
            return false;

        if (!matchExact && throws.Count < pattern.Count)
            return false;

        var remaining = new List<Throw>(throws);

        foreach (var slot in pattern)
        {
            int hit = remaining.FindIndex(t => SlotMatchesThrow(slot, t));
            if (hit < 0)
                return false;

            remaining.RemoveAt(hit);
        }

        return true;
    }

    public static bool SlotMatchesThrow(TurnPatternEntry slot, Throw throwItem)
    {
        if (slot == null || throwItem == null)
            return false;

        if (!slot.anyValue && throwItem.Value != slot.value)
            return false;

        if (!slot.anyMultiplier && throwItem.Multiplier != slot.multiplier)
            return false;

        if (!slot.anyHitType && !slot.anyMissHitType && throwItem.HitType != slot.hitType)
            return false;

        // Custom-Sektor (Tab 1 „Bereiche“): Wurf muss Custom sein, ggf. mit
        // gleicher Sektor-ID. Leere customHitId = jeder Custom-Sektor.
        if (!slot.anyHitType && !slot.anyMissHitType && slot.hitType == HitType.Custom)
        {
            if (throwItem.HitType != HitType.Custom)
                return false;

            if (!string.IsNullOrEmpty(slot.customHitId)
                && !string.Equals(slot.customHitId, throwItem.CustomHitId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (slot.anyMissHitType && throwItem.HitType == HitType.Board)
            return false;

        return true;
    }

    /// <summary>
    /// Rest-Prüfung (RemainingExactScore): feuert nur bei neu erreichtem
    /// Total (afterTotal != beforeTotal) — analog der früheren
    /// Schnapszahl-Übergangsregel. RepdigitScore bleibt turn-basiert.
    /// </summary>
    public static bool MatchesRemaining(PenaltyCondition condition, int beforeTotal, int afterTotal)
    {
        if (condition == null)
            return false;

        if (condition.kind != PenaltyConditionKind.RemainingExactScore)
            return false;

        return afterTotal == condition.score && afterTotal != beforeTotal;
    }

    /// <summary>Repdigit wie X01-IsSchnapszahl (alle Ziffern gleich).</summary>
    public static bool IsRepdigit(int value)
    {
        if (value < 0)
            return false;

        string s = value.ToString();
        return s.Length > 0 && s.Distinct().Count() == 1;
    }
}
