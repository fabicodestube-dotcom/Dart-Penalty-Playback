using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tests für das Turn-Pattern-Matching. Gilt identisch für Custom-Penalties
/// (PenaltyEvaluator.MatchesTurn) und Custom-Audio-Events
/// (AudioEventEngine.Matches).
///
/// Semantik:
/// - matchOrder = true  → geordnet + exakt: Slot i entspricht Wurf i.
/// - matchOrder = false → ungeordnet + exakt: der Turn besteht genau aus den
///   Pattern-Slots (Multimenge, Reihenfolge egal).
/// In beiden Fällen muss die Anzahl der Würfe der Slot-Anzahl entsprechen.
///
/// Menü: Darts/Tests/Turn Pattern.
/// Batchmode: -executeMethod TurnPatternMatchTest.Run
/// </summary>
public static class TurnPatternMatchTest
{
    [MenuItem("Darts/Tests/Turn Pattern")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            if (cond)
            {
                Debug.Log($"  OK   {name}");
            }
            else
            {
                failures++;
                Debug.LogError($"  FAIL {name} {detail}");
            }
        }

        // ---------- A) 3 Slots (1/5/20), Reihenfolge AN ----------
        var ordered = new List<TurnPatternEntry>
        {
            Slot(1), Slot(5), Slot(20)
        };

        Check(PenaltyEvaluator.MatchesPattern(ordered, Throws(1, 5, 20), true),
            "ordered-1-5-20-matches");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, Throws(20, 5, 1), true),
            "ordered-1-5-20-rejects-20-5-1");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, Throws(1, 20, 5), true),
            "ordered-1-5-20-rejects-1-20-5");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, Throws(1, 5, 20, 20), true),
            "ordered-1-5-20-rejects-4-darts");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, Throws(1, 5), true),
            "ordered-1-5-20-rejects-2-darts");

        // Slot mit festem Multiplier: Double 20 passt nicht auf Single 20.
        var singleOnly = new List<TurnPatternEntry>
        {
            Slot(1), Slot(5), Slot(20, DartMultiplier.Single, anyMultiplier: false)
        };

        Check(!PenaltyEvaluator.MatchesPattern(singleOnly, new List<Throw>
        {
            T(1), T(5), T(20, HitType.Board, null, DartMultiplier.Double)
        }, true), "ordered-rejects-double-20");

        // ---------- B) 3 Slots, Reihenfolge AUS ----------
        Check(PenaltyEvaluator.MatchesPattern(ordered, Throws(20, 5, 1), false),
            "unordered-accepts-20-5-1");
        Check(PenaltyEvaluator.MatchesPattern(ordered, Throws(20, 1, 5), false),
            "unordered-accepts-20-1-5");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, Throws(1, 5), false),
            "unordered-rejects-2-darts");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, Throws(1, 5, 20, 20), false),
            "unordered-rejects-4-darts");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, Throws(1, 5, 19), false),
            "unordered-rejects-other-value");

        // ---------- C) Wildcards ----------
        var wildcards = new List<TurnPatternEntry>
        {
            AnySlot(), AnySlot(), AnySlot()
        };

        Check(PenaltyEvaluator.MatchesPattern(wildcards, Throws(3, 7, 19), true),
            "wildcards-ordered-matches");
        Check(PenaltyEvaluator.MatchesPattern(wildcards, Throws(19, 7, 3), false),
            "wildcards-unordered-matches");

        // ---------- D) Miss-Slots (Wand/Decke/Custom) ----------
        var misses = new List<TurnPatternEntry>
        {
            MissSlot(), MissSlot(), MissSlot()
        };

        Check(PenaltyEvaluator.MatchesPattern(misses, new List<Throw>
        {
            T(0, HitType.Wall), T(0, HitType.Ceiling), T(0, HitType.Custom, "sec1")
        }, false), "miss-slots-match-wall-ceiling-custom");

        Check(!PenaltyEvaluator.MatchesPattern(misses, new List<Throw>
        {
            T(20), T(0, HitType.Wall), T(0, HitType.Ceiling)
        }, false), "miss-slots-reject-board");

        // ---------- E) 1 Slot (Turn mit genau einem Wurf) ----------
        var single = new List<TurnPatternEntry> { Slot(20) };

        Check(PenaltyEvaluator.MatchesPattern(single, Throws(20), false),
            "single-slot-1-dart-unordered");
        Check(PenaltyEvaluator.MatchesPattern(single, Throws(20), true),
            "single-slot-1-dart-ordered");
        Check(!PenaltyEvaluator.MatchesPattern(single, Throws(20, 5, 1), false),
            "single-slot-rejects-3-darts");

        // ---------- F) Custom-Sektor-Slot ----------
        var sector = new List<TurnPatternEntry> { SectorSlot("sec1") };

        Check(PenaltyEvaluator.MatchesPattern(sector, new List<Throw> { T(0, HitType.Custom, "sec1") }, false),
            "sector-slot-matches-own-sector");
        Check(!PenaltyEvaluator.MatchesPattern(sector, new List<Throw> { T(0, HitType.Custom, "sec2") }, false),
            "sector-slot-rejects-other-sector");
        Check(!PenaltyEvaluator.MatchesPattern(sector, new List<Throw> { T(20) }, false),
            "sector-slot-rejects-board");

        // ---------- G) Leeres Pattern ----------
        Check(!PenaltyEvaluator.MatchesPattern(new List<TurnPatternEntry>(), Throws(20), true),
            "empty-pattern-rejects");
        Check(!PenaltyEvaluator.MatchesPattern(ordered, null, true),
            "null-throws-rejects");

        // ---------- H) Penalty-Pfad (MatchesTurn) ----------
        var condition = new PenaltyCondition
        {
            kind = PenaltyConditionKind.TurnPattern,
            matchOrder = true,
            pattern = ordered
        };

        Check(PenaltyEvaluator.MatchesTurn(condition, Throws(1, 5, 20), 26), "penalty-ordered-matches");
        Check(!PenaltyEvaluator.MatchesTurn(condition, Throws(20, 5, 1), 26), "penalty-ordered-rejects-20-5-1");

        condition.matchOrder = false;
        Check(PenaltyEvaluator.MatchesTurn(condition, Throws(20, 5, 1), 26), "penalty-unordered-matches");

        // ---------- I) AudioEvent-Pfad (AudioEventEngine) ----------
        var trigger = new AudioEventTrigger
        {
            kind = AudioEventTriggerKind.TurnPattern,
            matchOrder = true,
            pattern = ordered
        };

        var values = new AudioTriggerValues { throws = Throws(1, 5, 20) };
        Check(AudioEventEngine.Matches(trigger, values), "event-ordered-matches");

        values.throws = Throws(20, 5, 1);
        Check(!AudioEventEngine.Matches(trigger, values), "event-ordered-rejects-20-5-1");

        trigger.matchOrder = false;
        Check(AudioEventEngine.Matches(trigger, values), "event-unordered-matches");

        // Manual/ScoreTriggeren bleiben unberührt.
        trigger.kind = AudioEventTriggerKind.Manual;
        Check(!AudioEventEngine.Matches(trigger, values), "event-manual-never-auto-matches");

        trigger.kind = AudioEventTriggerKind.ScoreReached;
        trigger.score = 26;
        var scoreValues = new AudioTriggerValues { throws = Throws(1, 5, 20), reachedScores = new List<int> { 26 } };
        Check(AudioEventEngine.Matches(trigger, scoreValues), "event-score-reached-matches");

        if (failures == 0)
        {
            Debug.Log("[PATTERN-TEST] Alle Prüfungen bestanden.");
        }
        else
        {
            Debug.LogError($"[PATTERN-TEST] {failures} Prüfung(en) fehlgeschlagen.");
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    /// <summary>Pattern-Slot: fester Wert, Multiplier/Hit-Typ frei (Wildcard).</summary>
    private static TurnPatternEntry Slot(int value, DartMultiplier multiplier = DartMultiplier.Single,
        bool anyMultiplier = true)
    {
        return new TurnPatternEntry
        {
            anyValue = false,
            value = value,
            anyMultiplier = anyMultiplier,
            multiplier = multiplier,
            anyHitType = true
        };
    }

    /// <summary>Pattern-Slot: alles frei (Wert/Multiplier/Hit-Typ).</summary>
    private static TurnPatternEntry AnySlot()
    {
        return new TurnPatternEntry
        {
            anyValue = true,
            anyMultiplier = true,
            anyHitType = true
        };
    }

    /// <summary>Pattern-Slot: Fehlwurf (Wand/Decke/Custom-Sektor).</summary>
    private static TurnPatternEntry MissSlot()
    {
        return new TurnPatternEntry
        {
            anyValue = true,
            anyMultiplier = true,
            anyHitType = false,
            anyMissHitType = true,
            hitType = HitType.Board
        };
    }

    /// <summary>Pattern-Slot: Custom-Sektor (Tab 1 „Bereiche“).</summary>
    private static TurnPatternEntry SectorSlot(string sectorId)
    {
        return new TurnPatternEntry
        {
            anyValue = true,
            anyMultiplier = true,
            anyHitType = false,
            hitType = HitType.Custom,
            customHitId = sectorId
        };
    }

    private static List<Throw> Throws(params int[] values)
    {
        var list = new List<Throw>(values.Length);

        foreach (var value in values)
            list.Add(T(value));

        return list;
    }

    private static Throw T(int value, HitType hitType = HitType.Board, string customHitId = null,
        DartMultiplier multiplier = DartMultiplier.Single)
    {
        return new Throw(multiplier, value, hitType, false, -1, customHitId);
    }
}
