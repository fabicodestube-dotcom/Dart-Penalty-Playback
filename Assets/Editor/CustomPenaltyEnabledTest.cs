using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regression für die Custom-only Penalty-Welt (Legacy-Buchung stillgelegt):
/// - ExactScore (Turn) + RemainingExactScore (Rest/Total) inkl. enabled,
///   Übergangsregel und Snapshot-Semantik, alle 3 Modi.
/// - Kein Legacy mehr: keine Buchungen, keine Events, Counts bleiben 0.
/// - Abspiel-Reihenfolge: Custom-Penalty vor TurnCompleted.
/// - ATC-Undo storniert Customs.
/// - Custom-Sektor-Instant-Penalty, Sektor-Event (auch X01-Check-in),
///   Manual-Audio-Guard und Snapshot-Roundtrip von customHitId.
/// Menü: Darts/Tests/Custom Penalties Enabled. Batchmode: -executeMethod CustomPenaltyEnabledTest.Run
/// </summary>
public static class CustomPenaltyEnabledTest
{
    [MenuItem("Darts/Tests/Custom Penalties Enabled")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            Debug.Log($"PENALTY_ENABLED: {(cond ? "PASS" : "FAIL")} {name} {detail}");
            if (!cond) failures++;
        }

        try
        {
            TestDisabledExactScoreX01(Check);
            TestEnabledExactScoreX01(Check);
            TestMidGameToggleIgnored(Check);
            TestInstantSuppression(Check);
            TestAudioEventEnabled(Check);
            TestAudioScoreReachedRemaining69(Check);
            TestAudioScoreReachedCricket(Check);
            TestAudioScoreReachedCricketCutthroat(Check);
            TestAudio420SeedGlobal(Check);
            TestExactScoreCricketDisabled(Check);
            TestAtcUndoRefundsCustom(Check);
            TestRemainingX01(Check);
            TestRemainingX01Disabled(Check);
            TestRemainingX01TurnOnlyNoFire(Check);
            TestRemainingCricketNormal(Check);
            TestRemainingCricketCutthroat(Check);
            TestMatchesRemainingUnit(Check);
            TestLegacySilence(Check);
            TestLegacyFallbackMapping(Check);
            TestCustomSectorInstantPenalty(Check);
            TestSectorHitEvent(Check);
            TestManualAudioEventGuard(Check);
            TestCustomSectorSnapshotRoundtrip(Check);
        }
        catch (Exception ex)
        {
            Debug.Log($"PENALTY_ENABLED: EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            failures++;
        }

        Debug.Log($"PENALTY_ENABLED: RESULT failures={failures}");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static X01GameSettings X01Settings201(CheckinType checkin = CheckinType.StraightIn)
    {
        return new X01GameSettings
        {
            pointTarget = 201,
            checkinType = checkin,
            checkoutType = CheckoutType.Single,
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = 1,
            legCount = 1,
            soundEnabled = false,
            Penalties = new PenaltySettings
            {
                Settings = new List<PenaltySetting>()
            }
        };
    }

    private static X01GameSettings X01Settings101()
    {
        var settings = X01Settings201();
        settings.pointTarget = 101;
        return settings;
    }

    private static CricketGameSettings CricketSettings(bool cutThroat)
    {
        return new CricketGameSettings
        {
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = 1,
            legCount = 1,
            soundEnabled = false,
            pointsEnabled = true,
            cutThroatEnabled = cutThroat,
            Penalties = new PenaltySettings()
        };
    }

    private static CustomPenaltyDef Exact111Def(bool enabled)
    {
        return new CustomPenaltyDef
        {
            id = "test111",
            name = "111",
            enabled = enabled,
            cost = 10,
            builtIn = false,
            condition = new PenaltyCondition { kind = PenaltyConditionKind.ExactScore, score = 111 }
        };
    }

    private static CustomPenaltyDef Remaining111Def(bool enabled, string id = "rem111")
    {
        return new CustomPenaltyDef
        {
            id = id,
            name = "111 Rest",
            enabled = enabled,
            cost = 10,
            builtIn = false,
            condition = new PenaltyCondition { kind = PenaltyConditionKind.RemainingExactScore, score = 111 }
        };
    }

    private static CustomPenaltyDef WallInstantDef(bool enabled)
    {
        return new CustomPenaltyDef
        {
            id = "testwall",
            name = "Wall",
            enabled = enabled,
            cost = 5,
            builtIn = false,
            condition = new PenaltyCondition { kind = PenaltyConditionKind.HitTypeMiss, hitType = HitType.Wall }
        };
    }

    private static AudioEventDef AudioScoreDef(string id, bool enabled, int score,
        AudioEventCategory category = AudioEventCategory.X01)
    {
        return new AudioEventDef
        {
            id = id,
            name = id,
            builtIn = false,
            enabled = enabled,
            category = category,
            trigger = new AudioEventTrigger { kind = AudioEventTriggerKind.ScoreReached, score = score }
        };
    }

    /// <summary>111 Punkte in 3 gültigen Darts: T20 (60) + T12 (36) + S15 (15).</summary>
    private static void Throw111Turn(X01Game game)
    {
        game.AddThrow(new Throw(DartMultiplier.Triple, 20, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 12, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 15, HitType.Board));
    }

    /// <summary>90 Punkte: T20 (60) + S20 (20) + S10 (10). Bei 201 -> Rest 111.</summary>
    private static void Throw90Turn(X01Game game)
    {
        game.AddThrow(new Throw(DartMultiplier.Triple, 20, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 20, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 10, HitType.Board));
    }

    private static int CustomCount(Game game, Guid pid, string id)
    {
        if (game.GetPlayerStats().TryGetValue(pid, out var stats) && stats?.customPenaltyCounts != null)
            return stats.customPenaltyCounts.TryGetValue(id, out int c) ? c : 0;
        return 0;
    }

    private static int LegacyWall(Game game, Guid pid)
    {
        if (game.GetPlayerStats().TryGetValue(pid, out var stats) && stats != null)
            return stats.wallCount;
        return -1;
    }

    // =========================================================
    // TESTS (Bestand)
    // =========================================================

    private static void TestDisabledExactScoreX01(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Exact111Def(false) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add(id);

        Throw111Turn(game);

        Check(customEvents.Count == 0, "x01-disabled-no-event", $"events={customEvents.Count}");
        Check(CustomCount(game, p1.GetID(), "test111") == 0, "x01-disabled-no-count", "");
    }

    private static void TestEnabledExactScoreX01(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Exact111Def(true) });

        var order = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => order.Add("penalty:" + id);
        game.OnTurnCompleted += (pid, turn) => order.Add("turn");

        Throw111Turn(game);

        int penaltyAt = order.IndexOf("penalty:test111");
        int turnAt = order.IndexOf("turn");
        Check(penaltyAt >= 0, "x01-enabled-fires", $"order=[{string.Join(",", order)}]");
        Check(turnAt > penaltyAt, "x01-order-penalty-before-turncompleted", "");
        Check(CustomCount(game, p1.GetID(), "test111") == 1, "x01-enabled-count-1", "");
        game.GetPlayerStats().TryGetValue(p1.GetID(), out var stats);
        Check(stats != null && Math.Abs(stats.customPenaltyCost - 10f) < 0.01f, "x01-enabled-cost", "");
    }

    private static void TestMidGameToggleIgnored(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Exact111Def(true) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add(id);

        Throw111Turn(game); // P1: 111 -> Buchung 1
        // Simulierter Mid-Game-Toggle: Snapshot des Spiels bleibt unverändert.
        // (Echte Toggles schreiben nur den Live-Store, nicht den Snapshot.)
        Throw111Turn(game); // P2: 111 -> Buchung 2 (noch aktiv)

        Check(customEvents.Count == 2, "snapshot-fires-while-active", $"events={customEvents.Count}");

        // Deaktivieren NACH Spielstart darf das laufende Spiel nicht ändern:
        // Snapshot ist eingefroren, Neubewertung nutzt ihn weiter.
        var frozen = game.GetPenaltySnapshot();
        Check(frozen.Count == 1 && frozen[0].enabled, "snapshot-frozen-enabled", "");
    }

    private static void TestInstantSuppression(Action<bool, string, string> Check)
    {
        // Deaktiviert: kein Custom, kein Legacy-Count (System stillgelegt).
        {
            var p1 = new Player(Guid.NewGuid(), "T_P1");
            var p2 = new Player(Guid.NewGuid(), "T_P2");
            var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
            game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { WallInstantDef(false) });

            var customs = new List<string>();
            game.OnCustomPenaltyTriggered += (id, pid, turn) => customs.Add(id);

            game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));

            Check(customs.Count == 0, "instant-disabled-no-custom", "");
            Check(CustomCount(game, p1.GetID(), "testwall") == 0, "instant-disabled-no-count", "");
            Check(LegacyWall(game, p1.GetID()) == 0, "instant-disabled-no-legacy", "");
        }

        // Aktiviert: Custom feuert, Legacy bleibt 0.
        {
            var p1 = new Player(Guid.NewGuid(), "T_P1");
            var p2 = new Player(Guid.NewGuid(), "T_P2");
            var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
            game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { WallInstantDef(true) });

            var customs = new List<string>();
            game.OnCustomPenaltyTriggered += (id, pid, turn) => customs.Add(id);

            game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));

            Check(customs.Count == 1 && customs[0] == "testwall", "instant-enabled-custom-fires", "");
            Check(LegacyWall(game, p1.GetID()) == 0, "instant-enabled-no-legacy", "");
        }
    }

    private static void TestAudioEventEnabled(Action<bool, string, string> Check)
    {
        AudioEventDef ScoreEvent(bool enabled)
        {
            return new AudioEventDef
            {
                id = "testrest90",
                name = "90",
                builtIn = false,
                enabled = enabled,
                category = AudioEventCategory.X01,
                trigger = new AudioEventTrigger { kind = AudioEventTriggerKind.ScoreReached, score = 90 }
            };
        }

        // Deaktiviert: kein Event.
        {
            var p1 = new Player(Guid.NewGuid(), "T_P1");
            var p2 = new Player(Guid.NewGuid(), "T_P2");
            var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
            game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
            game.RestoreAudioEventSnapshot(new List<AudioEventDef> { ScoreEvent(false) });

            var audioEvents = new List<string>();
            game.OnCustomAudioEvent += id => audioEvents.Add(id);

            Throw111Turn(game);

            Check(audioEvents.Count == 0, "audio-disabled-silent", $"events={audioEvents.Count}");
        }

        // Aktiviert: Event feuert.
        {
            var p1 = new Player(Guid.NewGuid(), "T_P1");
            var p2 = new Player(Guid.NewGuid(), "T_P2");
            var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
            game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
            game.RestoreAudioEventSnapshot(new List<AudioEventDef> { ScoreEvent(true) });

            var audioEvents = new List<string>();
            game.OnCustomAudioEvent += id => audioEvents.Add(id);

            Throw111Turn(game);

            Check(audioEvents.Count == 1 && audioEvents[0] == "testrest90", "audio-enabled-fires", $"events=[{string.Join(",", audioEvents)}]");
        }
    }

    private static void TestAudioScoreReachedRemaining69(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings101(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(new List<AudioEventDef>
        {
            AudioScoreDef("testrest69", true, 69),
            AudioScoreDef("testturn32", true, 32)
        });

        var audioEvents = new List<string>();
        game.OnCustomAudioEvent += id => audioEvents.Add(id);

        game.AddThrow(new Throw(DartMultiplier.Triple, 10, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 2, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));

        Check(audioEvents.Count == 1 && audioEvents[0] == "testrest69",
            "audio-score-remaining-69", $"events=[{string.Join(",", audioEvents)}]");

        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));

        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));

        Check(audioEvents.Count == 1, "audio-score-remaining-no-repeat",
            $"events=[{string.Join(",", audioEvents)}]");
    }

    private static void TestAudioScoreReachedCricket(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new CricketGame(Guid.NewGuid(), CricketSettings(false), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(new List<AudioEventDef>
        {
            AudioScoreDef("testglobal57", true, 57, AudioEventCategory.Global)
        });

        var audioEvents = new List<string>();
        game.OnCustomAudioEvent += id => audioEvents.Add(id);

        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 18, HitType.Board));

        Check(audioEvents.Count == 1 && audioEvents[0] == "testglobal57",
            "audio-score-cricket-57", $"events=[{string.Join(",", audioEvents)}]");

        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));

        Check(audioEvents.Count == 1, "audio-score-cricket-no-repeat",
            $"events=[{string.Join(",", audioEvents)}]");
    }

    private static void TestAudioScoreReachedCricketCutthroat(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new CricketGame(Guid.NewGuid(), CricketSettings(true), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(new List<AudioEventDef>
        {
            AudioScoreDef("testcutthroat57", true, 57, AudioEventCategory.Cricket)
        });

        var audioEvents = new List<string>();
        game.OnCustomAudioEvent += id => audioEvents.Add(id);

        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 18, HitType.Board));

        Check(audioEvents.Count == 1 && audioEvents[0] == "testcutthroat57",
            "audio-score-cutthroat-57", $"events=[{string.Join(",", audioEvents)}]");
    }

    private static void TestAudio420SeedGlobal(Action<bool, string, string> Check)
    {
        var seed = SeededAudioEvents.All.Find(s => s != null && s.Id == SeededAudioEvents.Score420Id);
        Check(seed != null && seed.Category == AudioEventCategory.Global,
            "audio-420-seed-global", "");

        var builtIn = AudioEventCatalog.BuildBuiltIns()
            .Find(x => x != null && x.id == SeededAudioEvents.Score420Id);
        Check(builtIn != null && builtIn.category == AudioEventCategory.Global,
            "audio-420-builtin-global", "");
    }

    private static void TestExactScoreCricketDisabled(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new CricketGame(Guid.NewGuid(), CricketSettings(false), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Exact111Def(false) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add(id);

        game.AddThrow(new Throw(DartMultiplier.Triple, 20, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 12, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 15, HitType.Board));

        Check(customEvents.Count == 0, "cricket-disabled-no-event", $"events={customEvents.Count}");
        Check(CustomCount(game, p1.GetID(), "test111") == 0, "cricket-disabled-no-count", "");
    }

    private static void TestAtcUndoRefundsCustom(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var settings = new ATCGameSettings
        {
            targetType = ATCTargetType.Singles,
            order = ATCOrder.Ascending,
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = 1,
            legCount = 1,
            soundEnabled = false,
            Penalties = new PenaltySettings()
        };
        var game = new ATCGame(Guid.NewGuid(), settings, new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { WallInstantDef(true) });

        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        Check(CustomCount(game, p1.GetID(), "testwall") == 1, "atc-wall-booked", "");

        game.Undo();

        Check(CustomCount(game, p1.GetID(), "testwall") == 0, "atc-undo-refunds-custom", "");
        game.GetPlayerStats().TryGetValue(p1.GetID(), out var stats);
        Check(stats != null && Math.Abs(stats.customPenaltyCost) < 0.01f, "atc-undo-refunds-cost", "");
    }

    // =========================================================
    // TESTS (Migration: RemainingExactScore)
    // =========================================================

    private static void TestRemainingX01(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Remaining111Def(true) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add(id);

        Throw90Turn(game); // P1: 90 geworfen -> Rest 111 -> feuert
        Check(customEvents.Count == 1 && customEvents[0] == "rem111", "x01-remaining-fires", $"events=[{string.Join(",", customEvents)}]");
        Check(CustomCount(game, p1.GetID(), "rem111") == 1, "x01-remaining-count", "");

        // P2 wirft 60 (Rest 141) -> kein Feuer.
        game.AddThrow(new Throw(DartMultiplier.Single, 20, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 20, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 20, HitType.Board));
        Check(customEvents.Count == 1, "x01-remaining-other-rest-silent", "");

        // P1: 0-Punkte-Turn (3x Wall) bei Rest 111 -> Übergangsregel, kein Feuer.
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        Check(customEvents.Count == 1, "x01-remaining-transition-silent", $"events={customEvents.Count}");
        Check(CustomCount(game, p1.GetID(), "rem111") == 1, "x01-remaining-count-stable", "");
    }

    private static void TestRemainingX01Disabled(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Remaining111Def(false) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add(id);

        Throw90Turn(game); // Rest 111, aber deaktiviert

        Check(customEvents.Count == 0, "x01-remaining-disabled-silent", $"events={customEvents.Count}");
        Check(CustomCount(game, p1.GetID(), "rem111") == 0, "x01-remaining-disabled-no-count", "");
    }

    private static void TestRemainingX01TurnOnlyNoFire(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Remaining111Def(true) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add(id);

        Throw111Turn(game); // 111 geworfen, Rest 90 -> kein Feuer (Rest-Semantik)

        Check(customEvents.Count == 0, "x01-remaining-turn-only-silent", $"events={customEvents.Count}");
        Check(CustomCount(game, p1.GetID(), "rem111") == 0, "x01-remaining-turn-only-no-count", "");
    }

    private static void TestRemainingCricketNormal(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new CricketGame(Guid.NewGuid(), CricketSettings(false), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Remaining111Def(true) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add($"{id}:{pid == p1.GetID()}");

        // Turn 1 (P1): T19 (19 zu), T19 (+57), T18 (18 zu). Total 57 -> kein Feuer.
        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 18, HitType.Board));
        Check(customEvents.Count == 0, "cricket-remaining-t1-silent", $"events={customEvents.Count}");

        // Turn 2 (P2): 3x S1 (0 Punkte) -> kein Feuer.
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        Check(customEvents.Count == 0, "cricket-remaining-t2-silent", $"events={customEvents.Count}");

        // Turn 3 (P1): T18 (+54 = Total 111), S1, S1 (füllen). -> feuert an P1.
        game.AddThrow(new Throw(DartMultiplier.Triple, 18, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));

        Check(customEvents.Count == 1 && customEvents[0] == "rem111:True", "cricket-remaining-fires", $"events=[{string.Join(",", customEvents)}]");
        Check(CustomCount(game, p1.GetID(), "rem111") == 1, "cricket-remaining-count", "");
    }

    private static void TestRemainingCricketCutthroat(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new CricketGame(Guid.NewGuid(), CricketSettings(true), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef> { Remaining111Def(true) });

        var customEvents = new List<string>();
        game.OnCustomPenaltyTriggered += (id, pid, turn) => customEvents.Add($"{id}:{pid == p1.GetID()}:{pid == p2.GetID()}");

        // P1-Turn 1: Punkte landen bei P2 (57). P2-Total 57 -> kein Feuer.
        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 19, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Triple, 18, HitType.Board));
        Check(customEvents.Count == 0, "cutthroat-t1-silent", $"events={customEvents.Count}");

        // P2-Turn: 3x S1 (0 Punkte, Total P1 unverändert 0) -> kein Feuer.
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        Check(customEvents.Count == 0, "cutthroat-t2-silent", $"events={customEvents.Count}");

        // P1-Turn 2: T18 (+54 an P2 = Total 111), S1, S1 -> feuert, Buchung an P2.
        game.AddThrow(new Throw(DartMultiplier.Triple, 18, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));

        Check(customEvents.Count == 1, "cutthroat-fires", $"events=[{string.Join(",", customEvents)}]");
        Check(CustomCount(game, p2.GetID(), "rem111") == 1, "cutthroat-booked-to-owner", "");
        Check(CustomCount(game, p1.GetID(), "rem111") == 0, "cutthroat-thrower-clean", "");
    }

    private static void TestMatchesRemainingUnit(Action<bool, string, string> Check)
    {
        var cond = new PenaltyCondition { kind = PenaltyConditionKind.RemainingExactScore, score = 111 };
        Check(PenaltyEvaluator.MatchesRemaining(cond, 201, 111), "remaining-match", "");
        Check(!PenaltyEvaluator.MatchesRemaining(cond, 111, 111), "remaining-transition", "");
        Check(!PenaltyEvaluator.MatchesRemaining(cond, 201, 90), "remaining-nomatch", "");
        Check(!PenaltyEvaluator.MatchesRemaining(null, 201, 111), "remaining-null", "");
        Check(!PenaltyEvaluator.MatchesRemaining(
            new PenaltyCondition { kind = PenaltyConditionKind.ExactScore, score = 111 }, 201, 111),
            "remaining-wrong-kind", "");
    }

    private static void TestLegacyFallbackMapping(Action<bool, string, string> Check)
    {
        bool maps(string id, PenaltyType expected)
        {
            return AudioLibrary.TryGetPenaltyTypeForKey(id, out PenaltyType t) && t == expected;
        }

        Check(maps("builtin:score111", PenaltyType.Schnapszahl), "fallback-score111", "");
        Check(maps("builtin:score999", PenaltyType.Schnapszahl), "fallback-score999", "");
        Check(maps("builtin:threeones", PenaltyType.ThreeOnes), "fallback-threeones", "");
        Check(maps("builtin:allmiss", PenaltyType.AllMiss), "fallback-allmiss", "");
        Check(maps("builtin:leglost", PenaltyType.LostGame), "fallback-leglost", "");
        Check(maps("builtin:setlost", PenaltyType.LostGame), "fallback-setlost", "");
        Check(maps("builtin:gamelost", PenaltyType.LostGame), "fallback-gamelost", "");
        Check(maps("custom_wall", PenaltyType.Wall), "fallback-wall", "");
        Check(maps("custom_ceiling", PenaltyType.Ceiling), "fallback-ceiling", "");
        Check(!AudioLibrary.TryGetPenaltyTypeForKey("irgendwas", out _), "fallback-unknown", "");
        Check(!AudioLibrary.TryGetPenaltyTypeForKey(null, out _), "fallback-null", "");
        Check(!AudioLibrary.TryGetPenaltyTypeForKey("", out _), "fallback-empty", "");
    }

    private static CustomPenaltyDef CustomSectorInstantDef(bool enabled, string sectorId,
        string id = "testfloor")
    {
        return new CustomPenaltyDef
        {
            id = id,
            name = "Custom sector",
            enabled = enabled,
            cost = 7,
            builtIn = false,
            condition = new PenaltyCondition
            {
                kind = PenaltyConditionKind.HitTypeMiss,
                hitType = HitType.Custom,
                customHitId = sectorId
            }
        };
    }

    private static void TestCustomSectorInstantPenalty(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(),
            new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>
        {
            CustomSectorInstantDef(true, "sec1")
        });

        var order = new List<string>();
        game.OnSectorHit += (pid, sid) => order.Add("sector:" + sid);
        game.OnCustomPenaltyTriggered += (id, pid, turn) => order.Add("penalty:" + id);

        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Custom, false, -1, "sec1"));

        Check(order.Count == 2 && order[0] == "sector:sec1" && order[1] == "penalty:testfloor",
            "custom-sector-order", $"order=[{string.Join(",", order)}]");
        Check(CustomCount(game, p1.GetID(), "testfloor") == 1,
            "custom-sector-booked", "");

        // Anderer Sektor: eigener Sektor-Sound, aber keine Strafe.
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Custom, false, -1, "sec2"));
        Check(order.Count == 3 && order[2] == "sector:sec2",
            "custom-sector-other-no-penalty", $"order=[{string.Join(",", order)}]");
    }

    private static void TestSectorHitEvent(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(CheckinType.DoubleIn),
            new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>
        {
            CustomSectorInstantDef(true, "sec1")
        });

        var sectors = new List<string>();
        var penalties = new List<string>();
        game.OnSectorHit += (pid, sid) => sectors.Add(sid);
        game.OnCustomPenaltyTriggered += (id, pid, turn) => penalties.Add(id);

        // Check-in-Aufnahme: Sektor-Sound ja, Instant-Penalty bleibt wie bisher aus.
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Custom, false, -1, "sec1"));

        Check(sectors.Count == 1 && sectors[0] == "sec1", "sector-hit-custom",
            $"sectors=[{string.Join(",", sectors)}]");
        Check(penalties.Count == 0, "sector-hit-checkin-instant-unchanged", "");
    }

    private static void TestManualAudioEventGuard(Action<bool, string, string> Check)
    {
        AudioEventDef ManualEvent(bool enabled, AudioEventCategory category,
            AudioEventTriggerKind kind = AudioEventTriggerKind.Manual)
        {
            return new AudioEventDef
            {
                id = "testmanual",
                name = "Manual",
                enabled = enabled,
                category = category,
                trigger = new AudioEventTrigger { kind = kind }
            };
        }

        (X01Game game, List<string> fired) CreateGame(AudioEventDef def)
        {
            var p1 = new Player(Guid.NewGuid(), "T_P1");
            var p2 = new Player(Guid.NewGuid(), "T_P2");
            var game = new X01Game(Guid.NewGuid(), X01Settings201(),
                new List<Guid> { p1.GetID(), p2.GetID() });
            game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
            game.RestoreAudioEventSnapshot(new List<AudioEventDef> { def });
            var fired = new List<string>();
            game.OnCustomAudioEvent += id => fired.Add(id);
            return (game, fired);
        }

        var (disabled, disabledFired) = CreateGame(ManualEvent(false, AudioEventCategory.X01));
        Check(!disabled.TryTriggerManualAudioEvent("testmanual") && disabledFired.Count == 0,
            "manual-disabled-silent", "");

        var (wrongCategory, catFired) = CreateGame(ManualEvent(true, AudioEventCategory.Cricket));
        Check(!wrongCategory.TryTriggerManualAudioEvent("testmanual") && catFired.Count == 0,
            "manual-wrong-category-silent", "");

        var (autoKind, autoFired) = CreateGame(ManualEvent(true, AudioEventCategory.X01,
            AudioEventTriggerKind.ScoreReached));
        Check(!autoKind.TryTriggerManualAudioEvent("testmanual") && autoFired.Count == 0,
            "manual-auto-kind-silent", "");

        var (enabled, enabledFired) = CreateGame(ManualEvent(true, AudioEventCategory.X01));
        Check(enabled.TryTriggerManualAudioEvent("testmanual")
            && enabledFired.Count == 1 && enabledFired[0] == "testmanual",
            "manual-enabled-fires", "");
    }

    private static void TestCustomSectorSnapshotRoundtrip(Action<bool, string, string> Check)
    {
        var defs = new List<CustomPenaltyDef> { CustomSectorInstantDef(true, "sec1", "snapsec") };
        string json = JsonConvert.SerializeObject(defs, Formatting.None);
        var restored = JsonConvert.DeserializeObject<List<CustomPenaltyDef>>(json);

        Check(restored != null && restored.Count == 1
            && restored[0].condition != null
            && restored[0].condition.customHitId == "sec1",
            "custom-sector-snapshot-json", json);

        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(),
            new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(restored);

        var snapshot = game.GetPenaltySnapshot();
        Check(snapshot.Count == 1 && snapshot[0].condition.customHitId == "sec1",
            "custom-sector-snapshot-restored", "");
    }

    private static void TestLegacySilence(Action<bool, string, string> Check)
    {
        // Drei volle Miss-Turns + Three-Ones-Turn: ohne Customs darf NICHTS
        // mehr gebucht werden (Legacy stillgelegt), Counts bleiben 0.
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings201(), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());

        // AllMiss-Turn (P1).
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
        game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));

        // Three-Ones-Turn (P2): 1,1,1.
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));
        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));

        game.GetPlayerStats().TryGetValue(p1.GetID(), out var s1);
        game.GetPlayerStats().TryGetValue(p2.GetID(), out var s2);
        Check(s1 != null && s1.wallCount == 0 && s1.allMissCount == 0, "legacy-silence-p1", "");
        Check(s2 != null && s2.tripleOnesCount == 0, "legacy-silence-p2", "");
        Check(s1 != null && s1.customPenaltyCost == 0f, "legacy-silence-nocost", "");
    }
}
