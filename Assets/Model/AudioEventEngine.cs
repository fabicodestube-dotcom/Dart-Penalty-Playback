using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Vom Aufrufer (Engine/Game je Modus) gefüllte Kennzahlen für die Auswertung.
/// Nicht zutreffende Felder bleiben 0; reachedScores ist dann leer.
/// </summary>
public class AudioTriggerValues
{
    public int turnScore;
    public List<Throw> throws = new List<Throw>();
    public int marks;
    public int points;
    public int hits;
    public int streak;
    public List<int> reachedScores = new List<int>();

    public void AddReachedScore(int before, int after)
    {
        if (after != before)
            reachedScores.Add(after);
    }
}

/// <summary>
/// Wertet AudioEventDef-Trigger aus (rein, testbar). Manual-Trigger werden
/// übersprungen (direkte Aufrufe im Code bleiben bestehen).
/// </summary>
public static class AudioEventEngine
{
    /// <summary>Gibt die IDs aller zutreffenden (nicht-manuellen) Events zurück.</summary>
    public static List<string> Evaluate(IEnumerable<AudioEventDef> defs, AudioTriggerValues values)
    {
        var matched = new List<string>();

        if (defs == null || values == null)
            return matched;

        foreach (var def in defs)
        {
            if (def == null || !def.enabled || def.triggers == null || def.triggers.Count == 0)
                continue;

            if (def.triggers.All(t => t.kind == AudioEventTriggerKind.Manual))
                continue;

            if (MatchesAny(def.triggers, values))
                matched.Add(def.id);
        }

        return matched;
    }

    public static bool Matches(AudioEventTrigger trigger, AudioTriggerValues v)
    {
        switch (trigger.kind)
        {
            case AudioEventTriggerKind.ScoreReached:
                return v.reachedScores != null && v.reachedScores.Contains(trigger.score);

            case AudioEventTriggerKind.TurnPattern:
                return PenaltyEvaluator.MatchesPattern(trigger.pattern, v.throws, trigger.matchOrder);

            case AudioEventTriggerKind.MarksThreshold:
                return v.marks >= trigger.threshold;

            case AudioEventTriggerKind.PointsReached:
                return v.points >= trigger.threshold;

            case AudioEventTriggerKind.HitsInTurnThreshold:
                return v.hits >= trigger.threshold;

            case AudioEventTriggerKind.StreakReached:
                return v.streak >= trigger.threshold;

            case AudioEventTriggerKind.Manual:
            default:
                return false;
        }
    }

    /// <summary>Gibt true zurück, wenn JEDE Trigger-Übereinstimmung (OR-Logik, Manual übersprungen).</summary>
    public static bool MatchesAny(List<AudioEventTrigger> triggers, AudioTriggerValues v)
    {
        if (triggers == null || v == null)
            return false;

        foreach (var t in triggers)
        {
            if (t == null || t.kind == AudioEventTriggerKind.Manual)
                continue;

            if (Matches(t, v))
                return true;
        }

        return false;
    }
}
