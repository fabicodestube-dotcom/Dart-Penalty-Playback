using System.Collections.Generic;

/// <summary>
/// Vom Aufrufer (Engine/Game je Modus) gefüllte Kennzahlen für die Auswertung.
/// Nicht zutreffende Felder bleiben 0.
/// </summary>
public class AudioTriggerValues
{
    public int turnScore;
    public List<Throw> throws = new List<Throw>();
    public int marks;
    public int points;
    public int hits;
    public int streak;
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
            if (def == null || !def.enabled || def.trigger == null)
                continue;

            if (def.trigger.kind == AudioEventTriggerKind.Manual)
                continue;

            if (Matches(def.trigger, values))
                matched.Add(def.id);
        }

        return matched;
    }

    public static bool Matches(AudioEventTrigger trigger, AudioTriggerValues v)
    {
        switch (trigger.kind)
        {
            case AudioEventTriggerKind.ScoreReached:
                return v.turnScore == trigger.score;

            case AudioEventTriggerKind.TurnPattern:
                return PenaltyEvaluator.MatchesPattern(trigger.pattern, v.throws, trigger.matchExact);

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
}
