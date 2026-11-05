using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LegChangeAnnouncementTest
{
    [MenuItem("Darts/Tests/Leg Change Announcements")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            Debug.Log($"LEG_ANNOUNCE: {(cond ? "PASS" : "FAIL")} {name} {detail}");
            if (!cond) failures++;
        }

        try
        {
            TestX01LegChangeNonMatch(Check);
            TestX01MatchEndSilent(Check);
            TestCricketLegChangeNonMatch(Check);
            TestCricketMatchEndSilent(Check);
            TestAtcLegChangeNonMatch(Check);
            TestAtcMatchEndSilent(Check);
        }
        catch (Exception ex)
        {
            Debug.Log($"LEG_ANNOUNCE: EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            failures++;
        }

        Debug.Log($"LEG_ANNOUNCE: RESULT failures={failures}");
    }

    private static X01GameSettings X01Settings(int setCount, int legCount)
    {
        return new X01GameSettings
        {
            pointTarget = 1,
            checkinType = CheckinType.StraightIn,
            checkoutType = CheckoutType.Single,
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = setCount,
            legCount = legCount,
            soundEnabled = false,
            Penalties = new PenaltySettings
            {
                Settings = new List<PenaltySetting>()
            }
        };
    }

    private static CricketGameSettings CricketSettings(int setCount, int legCount)
    {
        return new CricketGameSettings
        {
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = setCount,
            legCount = legCount,
            soundEnabled = false,
            pointsEnabled = false,
            cutThroatEnabled = false,
            Penalties = new PenaltySettings()
        };
    }

    private static ATCGameSettings AtcSettings(int setCount, int legCount)
    {
        return new ATCGameSettings
        {
            targetType = ATCTargetType.Singles,
            order = ATCOrder.Ascending,
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = setCount,
            legCount = legCount,
            soundEnabled = false,
            Penalties = new PenaltySettings()
        };
    }

    private static void TestX01LegChangeNonMatch(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings(2, 1), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(LegSetAudioEvents());

        var events = new List<string>();
        game.OnLegWon += _ => events.Add("leg");
        game.OnSetWon += _ => events.Add("set");
        game.OnMatchWon += _ => events.Add("match");
        game.OnTurnCompleted += (_, __) => events.Add("turn");
        game.OnCustomAudioEvent += id => events.Add("audio:" + id);

        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));

        int legAt = events.LastIndexOf("leg");
        int setAt = events.LastIndexOf("set");
        int turnAt = events.LastIndexOf("turn");
        int legEndAt = events.IndexOf("audio:" + SeededAudioEvents.LegEndId, legAt + 1);
        int setEndAt = events.IndexOf("audio:" + SeededAudioEvents.SetEndId, legAt + 1);
        Check(legAt >= 0 && setAt > legAt && turnAt > setAt && legEndAt > turnAt && setEndAt > legEndAt,
            "x01-set-leg-set-audio-order", $"events=[{string.Join(",", events)}]");
        Check(events.Skip(legAt + 1).Count(e => e == "turn") == 1, "x01-set-turn-once", $"events=[{string.Join(",", events)}]");
        Check(!events.Contains("match"), "x01-set-no-match", $"events=[{string.Join(",", events)}]");
    }

    private static void TestX01MatchEndSilent(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new X01Game(Guid.NewGuid(), X01Settings(1, 1), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(LegSetAudioEvents());

        var events = new List<string>();
        game.OnLegWon += _ => events.Add("leg");
        game.OnSetWon += _ => events.Add("set");
        game.OnMatchWon += _ => events.Add("match");
        game.OnTurnCompleted += (_, __) => events.Add("turn");
        game.OnCustomAudioEvent += id => events.Add("audio:" + id);

        game.AddThrow(new Throw(DartMultiplier.Single, 1, HitType.Board));

        int legAt = events.LastIndexOf("leg");
        Check(legAt >= 0 && events.Contains("match"), "x01-match-finished", $"events=[{string.Join(",", events)}]");
        Check(!events.Skip(legAt + 1).Any(e => e == "turn"), "x01-match-no-turn", $"events=[{string.Join(",", events)}]");
        Check(!events.Any(e => e == "audio:" + SeededAudioEvents.LegEndId
                               || e == "audio:" + SeededAudioEvents.SetEndId),
            "x01-match-no-leg-set-audio", $"events=[{string.Join(",", events)}]");
    }

    private static void TestCricketLegChangeNonMatch(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new CricketGame(Guid.NewGuid(), CricketSettings(2, 1), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(LegSetAudioEvents());

        var events = new List<string>();
        game.OnLegWon += _ => events.Add("leg");
        game.OnSetWon += _ => events.Add("set");
        game.OnMatchWon += _ => events.Add("match");
        game.OnTurnCompleted += _ => events.Add("turn");
        game.OnCustomAudioEvent += id => events.Add("audio:" + id);

        PlayCricketToLegWin(game, p1.GetID(), events);

        int legAt = events.LastIndexOf("leg");
        int setAt = events.LastIndexOf("set");
        int turnAt = events.LastIndexOf("turn");
        int legEndAt = events.IndexOf("audio:" + SeededAudioEvents.LegEndId, legAt + 1);
        int setEndAt = events.IndexOf("audio:" + SeededAudioEvents.SetEndId, legAt + 1);
        Check(legAt >= 0 && setAt > legAt && turnAt > setAt && legEndAt > turnAt && setEndAt > legEndAt,
            "cricket-set-leg-set-audio-order", $"events=[{string.Join(",", events)}]");
        Check(events.Skip(legAt + 1).Count(e => e == "turn") == 1,
            "cricket-set-turn-once", $"events=[{string.Join(",", events)}]");
        Check(!events.Contains("match"), "cricket-set-no-match", $"events=[{string.Join(",", events)}]");
    }

    private static void TestCricketMatchEndSilent(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new CricketGame(Guid.NewGuid(), CricketSettings(1, 1), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(LegSetAudioEvents());

        var events = new List<string>();
        game.OnLegWon += _ => events.Add("leg");
        game.OnSetWon += _ => events.Add("set");
        game.OnMatchWon += _ => events.Add("match");
        game.OnTurnCompleted += _ => events.Add("turn");
        game.OnCustomAudioEvent += id => events.Add("audio:" + id);

        PlayCricketToLegWin(game, p1.GetID(), events);

        int legAt = events.LastIndexOf("leg");
        Check(legAt >= 0 && events.Contains("match"), "cricket-match-finished", $"events=[{string.Join(",", events)}]");
        Check(!events.Skip(legAt + 1).Any(e => e == "turn"), "cricket-match-no-turn", $"events=[{string.Join(",", events)}]");
        Check(!events.Any(e => e == "audio:" + SeededAudioEvents.LegEndId
                               || e == "audio:" + SeededAudioEvents.SetEndId),
            "cricket-match-no-leg-set-audio", $"events=[{string.Join(",", events)}]");
    }

    private static void TestAtcLegChangeNonMatch(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new ATCGame(Guid.NewGuid(), AtcSettings(2, 1), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(LegSetAudioEvents(new AudioEventDef
        {
            id = SeededAudioEvents.HitsId,
            name = "Hits",
            enabled = true,
            category = AudioEventCategory.ATC,
            triggers = new List<AudioEventTrigger>
            {
                new AudioEventTrigger
                {
                    kind = AudioEventTriggerKind.HitsInTurnThreshold,
                    threshold = 1
                }
            }
        }));

        var events = new List<string>();
        game.OnLegWon += _ => events.Add("leg");
        game.OnSetWon += _ => events.Add("set");
        game.OnMatchWon += _ => events.Add("match");
        game.OnATCTurnCompleted += (_, hits) => events.Add("score:" + hits);
        game.OnCustomAudioEvent += id => events.Add("audio:" + id);

        PlayAtcToLegWin(game, p1.GetID(), events);

        int legAt = events.LastIndexOf("leg");
        int setAt = events.LastIndexOf("set");
        int scoreAt = events.FindIndex(legAt + 1, e => e.StartsWith("score:"));
        int audioAt = events.FindIndex(legAt + 1, e => e == "audio:" + SeededAudioEvents.HitsId);
        int legEndAt = events.IndexOf("audio:" + SeededAudioEvents.LegEndId, legAt + 1);
        int setEndAt = events.IndexOf("audio:" + SeededAudioEvents.SetEndId, legAt + 1);
        Check(legAt >= 0 && setAt > legAt && scoreAt > setAt && audioAt > scoreAt
              && legEndAt > audioAt && setEndAt > legEndAt,
            "atc-set-score-hits-leg-set", $"events=[{string.Join(",", events)}]");
        Check(!events.Contains("match"), "atc-set-no-match", $"events=[{string.Join(",", events)}]");
    }

    private static void TestAtcMatchEndSilent(Action<bool, string, string> Check)
    {
        var p1 = new Player(Guid.NewGuid(), "T_P1");
        var p2 = new Player(Guid.NewGuid(), "T_P2");
        var game = new ATCGame(Guid.NewGuid(), AtcSettings(1, 1), new List<Guid> { p1.GetID(), p2.GetID() });
        game.RestorePenaltySnapshot(new List<CustomPenaltyDef>());
        game.RestoreAudioEventSnapshot(LegSetAudioEvents(new AudioEventDef
        {
            id = SeededAudioEvents.HitsId,
            name = "Hits",
            enabled = true,
            category = AudioEventCategory.ATC,
            triggers = new List<AudioEventTrigger>
            {
                new AudioEventTrigger
                {
                    kind = AudioEventTriggerKind.HitsInTurnThreshold,
                    threshold = 1
                }
            }
        }));

        var events = new List<string>();
        game.OnLegWon += _ => events.Add("leg");
        game.OnSetWon += _ => events.Add("set");
        game.OnMatchWon += _ => events.Add("match");
        game.OnATCTurnCompleted += (_, hits) => events.Add("score:" + hits);
        game.OnCustomAudioEvent += id => events.Add("audio:" + id);

        PlayAtcToLegWin(game, p1.GetID(), events);

        int legAt = events.LastIndexOf("leg");
        Check(legAt >= 0 && events.Contains("match"), "atc-match-finished", $"events=[{string.Join(",", events)}]");
        Check(!events.Skip(legAt + 1).Any(e => e.StartsWith("score:") || e.StartsWith("audio:")),
            "atc-match-silent", $"events=[{string.Join(",", events)}]");
    }

    private static List<AudioEventDef> LegSetAudioEvents(params AudioEventDef[] extra)
    {
        var events = new List<AudioEventDef>
        {
            new AudioEventDef
            {
                id = SeededAudioEvents.LegEndId,
                name = "Leg End",
                enabled = true,
            category = AudioEventCategory.Global,
            triggers = new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } }
            },
            new AudioEventDef
            {
                id = SeededAudioEvents.SetEndId,
                name = "Set End",
                enabled = true,
                category = AudioEventCategory.Global,
                triggers = new List<AudioEventTrigger> { new AudioEventTrigger { kind = AudioEventTriggerKind.Manual } }
            }
        };

        if (extra != null)
            events.AddRange(extra);

        return events;
    }

    private static void PlayCricketToLegWin(CricketGame game, Guid playerId, List<string> events)
    {
        int guard = 0;
        while (!events.Contains("leg") && guard++ < 200)
        {
            Guid current = game.GetCurrentPlayerId();
            if (current == playerId)
            {
                int number = game.GetNumbers().FirstOrDefault(n => !game.IsClosed(playerId, n));
                if (number != 0)
                    game.AddThrow(new Throw(DartMultiplier.Triple, number, HitType.Board));
                else
                    game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
            }
            else
            {
                game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Wall));
            }
        }
    }

    private static void PlayAtcToLegWin(ATCGame game, Guid playerId, List<string> events)
    {
        int guard = 0;
        while (!events.Contains("leg") && guard++ < 500)
        {
            Guid current = game.GetCurrentPlayerId();
            if (current == playerId)
            {
                int target = game.GetCurrentTarget(playerId);
                DartMultiplier multiplier = game.GetCurrentTargetMultiplier(playerId);
                game.AddThrow(new Throw(multiplier, target, HitType.Board, true, target));
            }
            else
            {
                game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1));
            }
        }
    }
}
