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

        if (condition.kind != PenaltyConditionKind.HitTypeMiss
            || throwItem.HitType != condition.hitType)
            return false;

        // Custom-Sektor: leere customHitId = jeder Custom-Sektor (analog
        // zu den TurnPattern-Slots).
        if (condition.hitType == HitType.Custom
            && !string.IsNullOrEmpty(condition.customHitId))
        {
            return string.Equals(condition.customHitId, throwItem.CustomHitId,
                System.StringComparison.OrdinalIgnoreCase);
        }

        return true;
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
                return MatchesPattern(condition.pattern, throws, condition.matchOrder);

            case PenaltyConditionKind.HitTypeMiss:
            default:
                return false;
        }
    }

    /// <summary>Gibt true zurück, wenn JEDE der Bedingungen zutrifft (OR-Logik).</summary>
    public static bool MatchesAnyTurn(List<PenaltyCondition> conditions, List<Throw> throws, int turnScore)
    {
        if (conditions == null || throws == null)
            return false;

        foreach (var c in conditions)
        {
            if (c == null)
                continue;
            if (MatchesTurn(c, throws, turnScore))
                return true;
        }

        return false;
    }

    /// <summary>Gibt true zurück, wenn JEDE der Rest-Bedingungen zutrifft (OR-Logik).</summary>
    public static bool MatchesAnyRemaining(List<PenaltyCondition> conditions, int beforeTotal, int afterTotal)
    {
        if (conditions == null)
            return false;

        foreach (var c in conditions)
        {
            if (c == null)
                continue;
            if (MatchesRemaining(c, beforeTotal, afterTotal))
                return true;
        }

        return false;
    }

    /// <summary>Gibt true zurück, wenn JEDE der Instant-Bedingungen zutrifft (OR-Logik).</summary>
    public static bool MatchesAnyInstant(List<PenaltyCondition> conditions, Throw throwItem)
    {
        if (conditions == null || throwItem == null)
            return false;

        foreach (var c in conditions)
        {
            if (c == null)
                continue;
            if (MatchesInstant(c, throwItem))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pattern-Match über die Würfe eines Turns.
    ///
    /// matchOrder = true  → geordnet: die Slots müssen in dieser Reihenfolge
    ///                      im Turn vorkommen.
    /// matchOrder = false → ungeordnet: die Slots müssen im Turn vorkommen,
    ///                      die Reihenfolge ist egal.
    ///
    /// In beiden Fällen sind zusätzliche Würfe im Turn erlaubt; das Pattern
    /// darf nur nicht mehr Slots als der Turn Würfe haben.
    /// Einheitlich für Custom-Penalties und Custom-Audio-Events.
    /// </summary>
    public static bool MatchesPattern(List<TurnPatternEntry> pattern, List<Throw> throws, bool matchOrder)
    {
        if (pattern == null || pattern.Count == 0 || throws == null)
            return false;

        if (throws.Count < pattern.Count)
            return false;

        if (matchOrder)
        {
            int throwIndex = 0;

            foreach (var slot in pattern)
            {
                while (throwIndex < throws.Count
                    && !SlotMatchesThrow(slot, throws[throwIndex]))
                {
                    throwIndex++;
                }

                if (throwIndex >= throws.Count)
                    return false;

                throwIndex++;
            }

            return true;
        }

        return MatchesPatternUnordered(pattern, 0, throws, new bool[throws.Count]);
    }

    private static bool MatchesPatternUnordered(List<TurnPatternEntry> pattern, int slotIndex,
        List<Throw> throws, bool[] used)
    {
        if (slotIndex >= pattern.Count)
            return true;

        for (int i = 0; i < throws.Count; i++)
        {
            if (used[i] || !SlotMatchesThrow(pattern[slotIndex], throws[i]))
                continue;

            used[i] = true;

            if (MatchesPatternUnordered(pattern, slotIndex + 1, throws, used))
                return true;

            used[i] = false;
        }

        return false;
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
