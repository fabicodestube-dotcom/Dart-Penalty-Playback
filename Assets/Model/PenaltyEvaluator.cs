using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Evaluates CustomPenaltyDef conditions (pure, testable, no Unity dependency).
/// Built-in types (enum) continue to run over the existing checks.
/// </summary>
public static class PenaltyEvaluator
{
    /// <summary>Instant check of a single litter (only HitTypeMiss useful).</summary>
    public static bool MatchesInstant(PenaltyCondition condition, Throw throwItem)
    {
        if (condition == null || throwItem == null)
            return false;

        if (condition.kind != PenaltyConditionKind.HitTypeMiss
            || throwItem.HitType != condition.hitType)
            return false;

        // Custom sector: empty customHitId = any custom sector (analogous
        // to the TurnPattern slots.
        if (condition.hitType == HitType.Custom
            && !string.IsNullOrEmpty(condition.customHitId))
        {
            return string.Equals(condition.customHitId, throwItem.CustomHitId,
                System.StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    /// <summary>Turn-end check (ExactScore, RepdigitScore, TurnPattern).</summary>
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

    /// <summary>Returns true if EVERY of the terms apply (OR logic).</summary>
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

    /// <summary>Returns true if EVERY of the rest conditions applies (OR logic).</summary>
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

    /// <summary>Returns true if EVERY of the instant conditions applies (OR logic).</summary>
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
    /// Pattern match about the throws of a turn.
    ///
    /// matchOrder = true → ordered: the slots must be in this order
    /// occurring in the turn.
    /// matchOrder = false → disordered: the slots must occur in the turn,
    /// The order does not matter.
    ///
    /// In both cases, additional throws in the turn are allowed; the pattern
    /// Just don’t have more slots than the turn throws.
    /// Uniform for custom penalties and custom audio events.
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

        // Custom sector (Tab 1 “Areas”): Throw must be custom, if necessary with
        // same sector ID. Empty customHitId = any custom sector.
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
    /// Rest check (RemainingExactScore): fires only when newly reached
    /// Total (afterTotal!=beforeTotal) — analogous to the previous
    /// Booze Number Transition Rule. RepdigitScore remains turn-based.
    /// </summary>
    public static bool MatchesRemaining(PenaltyCondition condition, int beforeTotal, int afterTotal)
    {
        if (condition == null)
            return false;

        if (condition.kind != PenaltyConditionKind.RemainingExactScore)
            return false;

        return afterTotal == condition.score && afterTotal != beforeTotal;
    }

    /// <summary>Repdigit like X01- IsSchnapszahl (all digits the same).</summary>
    public static bool IsRepdigit(int value)
    {
        if (value < 0)
            return false;

        string s = value.ToString();
        return s.Length > 0 && s.Distinct().Count() == 1;
    }
}
