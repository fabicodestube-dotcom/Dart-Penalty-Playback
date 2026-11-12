using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repro/Regression for: ATC Undo after match ending shows Target "Finished".
/// Builds a real ATC game (Singles, 1 Set/1 Leg), plays a player
/// Until match end, Undo, checks Target + Finish flags.
/// Menu: Darts/Tests/ATC Undo After MatchEnd. Batchmode: - executeMethod0@@
/// </summary>
public static class ATCUndoReproTest
{
    /// <summary>
    /// Regression for: Bot does not throw if there is a bot after Leg win.
    /// Checks event order: After OnLegWon must OnBotCheck for the new
    /// Starting players come (ATCGame.StartNewLeg triggers, like X01/Cricket).
    /// </summary>
    [MenuItem("Darts/Tests/ATC BotCheck After LegWin")]
    public static void RunLegWinBotCheckFromMenu()
    {
        RunLegWinBotCheck();
    }

    public static void RunLegWinBotCheck()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            Debug.Log($"ATC_BOT: {(cond ? "PASS" : "FAIL")} {name} {detail}");
            if (!cond) failures++;
        }

        try
        {
            var p1 = new Player(Guid.NewGuid(), "T_P1");
            var p2 = new Player(Guid.NewGuid(), "T_P2");

            var settings = new ATCGameSettings
            {
                targetType = ATCTargetType.Singles,
                order = ATCOrder.Ascending,
                setsAndLegsMode = SetsAndLegs.FirstTo,
                setCount = 1,
                legCount = 2,
                soundEnabled = false,
                Penalties = new PenaltySettings()
            };

            var game = new ATCGame(Guid.NewGuid(), settings, new List<Guid> { p1.GetID(), p2.GetID() });

            var events = new List<string>();
            game.OnLegWon += pid => events.Add("legwon:" + pid.ToString().Substring(0, 4));
            game.OnBotCheck += pid => events.Add("botcheck:" + pid.ToString().Substring(0, 4));

            int guard = 0;
            while (game.GetSets()[^1].GetLegs().Count < 2 && guard++ < 2000)
            {
                Guid cur = game.GetCurrentPlayerId();
                if (cur == p1.GetID())
                {
                    int t = game.GetCurrentTarget(cur);
                    if (t == -1) break;
                    game.AddThrow(new Throw(DartMultiplier.Single, t, HitType.Board, true, t));
                }
                else
                {
                    game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1));
                }
            }

            Check(game.GetSets()[^1].GetLegs().Count >= 2, "second-leg-started");

            int legWonAt = events.FindIndex(e => e.StartsWith("legwon:"));
            Check(legWonAt >= 0, "legwon-fired");

            Guid newStarter = game.GetCurrentPlayerId();
            bool botCheckAfterWin = events
                .Select((e, i) => new { e, i })
                .Any(x => x.i > legWonAt && x.e == "botcheck:" + newStarter.ToString().Substring(0, 4));
            Check(botCheckAfterWin, "botcheck-for-new-starter",
                $"starter={newStarter.ToString().Substring(0, 4)} events=[{string.Join(",", events)}]");
        }
        catch (Exception ex)
        {
            Debug.Log($"ATC_BOT: EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            failures++;
        }

        Debug.Log($"ATC_BOT: RESULT failures={failures}");
    }

    /// <summary>
    /// Variant 2: simulates the LoadGameById state (playerCurrentIndex is
    /// never persists → after load set all 0, finishedAt, then Undo.
    /// </summary>
    [MenuItem("Darts/Tests/ATC Undo From Load State")]
    public static void RunFromLoadStateMenu()
    {
        RunFromLoadState();
    }

    public static void RunFromLoadState()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            Debug.Log($"ATC_UNDO_LOAD: {(cond ? "PASS" : "FAIL")} {name} {detail}");
            if (!cond) failures++;
        }

        try
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

            int guard = 0;
            while (!game.IsPlayerFinished(p1.GetID()) && guard++ < 2000)
            {
                Guid cur = game.GetCurrentPlayerId();
                if (cur == p1.GetID())
                {
                    int t = game.GetCurrentTarget(cur);
                    if (t == -1) break;
                    game.AddThrow(new Throw(DartMultiplier.Single, t, HitType.Board, true, t));
                }
                else
                {
                    game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1));
                }
            }

            Check(game.IsFinished(), "match-finished");

            // ---- Simulate LoadGameById: indices are lost, finished remains ----
            var indexField = typeof(ATCGame).GetField("playerCurrentIndex",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var indices = (Dictionary<Guid, int>)indexField.GetValue(game);
            foreach (var pid in game.GetPlayerIDs())
                indices[pid] = 0;

            game.Undo();

            bool uiFinished = game.IsPlayerFinished(p1.GetID());
            int uiTarget = game.GetCurrentTarget(p1.GetID());
            Check(!game.IsFinished(), "match-not-finished-after-undo");
            Check(!uiFinished, "player-not-finished-after-undo");
            Check(uiTarget == 25, "target-back-to-last", $"got={uiTarget} expect=25");

            // Exactly the label data source of the UI (ATCPlayerListItem.SetTarget /
            // ATCInputHandler -Label) check, not just model getters.
            var uiStats = new ATCUIStats(game, p1);
            Check(!uiStats.isFinished, "uistats-not-finished");
            Check(uiStats.currentTarget == 25, "uistats-target", $"got={uiStats.currentTarget}");
            string prefix = uiStats.currentTargetMultiplier switch
            {
                DartMultiplier.Double => "D",
                DartMultiplier.Triple => "T",
                _ => ""
            };
            string label = uiStats.isFinished ? "Finished" : prefix + uiStats.currentTarget;
            Check(label == "25", "uistats-label", $"got={label}");
        }
        catch (Exception ex)
        {
            Debug.Log($"ATC_UNDO_LOAD: EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            failures++;
        }

        Debug.Log($"ATC_UNDO_LOAD: RESULT failures={failures}");
    }

    public static void Run()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            Debug.Log($"ATC_UNDO: {(cond ? "PASS" : "FAIL")} {name} {detail}");
            if (!cond) failures++;
        }

        try
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

            int guard = 0;
            while (!game.IsPlayerFinished(p1.GetID()) && guard++ < 2000)
            {
                Guid cur = game.GetCurrentPlayerId();
                if (cur == p1.GetID())
                {
                    int t = game.GetCurrentTarget(cur);
                    if (t == -1) break;
                    game.AddThrow(new Throw(DartMultiplier.Single, t, HitType.Board, true, t));
                }
                else
                {
                    // Opponent fits (board miss without penalty)
                    game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1));
                }
            }

            Check(game.IsPlayerFinished(p1.GetID()), "p1-finished", $"guard={guard}");
            Check(game.IsFinished(), "match-finished");

            int lastTarget = 25;
            Check(game.GetCurrentTarget(p1.GetID()) == -1, "target-minus1-when-finished");

            // Note: no JSON total round trip — production loads relational
            // (Sets/Legs/Turns), never by Game-JSON.
            // RunTriplesLoadUndo with exact load semantics.
            game.Undo();

            bool uiFinished = game.IsPlayerFinished(p1.GetID());
            int uiTarget = game.GetCurrentTarget(p1.GetID());
            string targetLabel = uiFinished
                ? "Finished"
                : (uiTarget == -1 ? "N/A" : uiTarget.ToString());
            string hitButtonLabel = uiTarget == -1 ? "Finished" : uiTarget.ToString();

            Check(!game.IsFinished(), "match-not-finished-after-undo");
            Check(!uiFinished, "player-not-finished-after-undo");
            Check(uiTarget == lastTarget, "target-back-to-last", $"got={uiTarget} expect={lastTarget}");
            Check(targetLabel == lastTarget.ToString(), "playerlist-label", $"got={targetLabel}");
            Check(hitButtonLabel == lastTarget.ToString(), "hitbutton-label", $"got={hitButtonLabel}");
        }
        catch (Exception ex)
        {
            Debug.Log($"ATC_UNDO: EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            failures++;
        }

        Debug.Log($"ATC_UNDO: RESULT failures={failures}");
    }

    /// <summary>
    /// Exact LoadGameById semantics: fresh singles steps + zeroed indices
    /// (this is how the repo load builds), then RestoreProgressAfterLoad + Undo.
    /// Triples variant of the reported bug (Single20 instead of T20).
    /// </summary>
    [MenuItem("Darts/Tests/ATC Triples Load Undo")]
    public static void RunTriplesLoadUndoFromMenu()
    {
        RunTriplesLoadUndo();
    }

    public static void RunTriplesLoadUndo()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            Debug.Log($"ATC_TRIPLES: {(cond ? "PASS" : "FAIL")} {name} {detail}");
            if (!cond) failures++;
        }

        try
        {
            var p1 = new Player(Guid.NewGuid(), "T_P1");
            var p2 = new Player(Guid.NewGuid(), "T_P2");

            var settings = new ATCGameSettings
            {
                targetType = ATCTargetType.Triples,
                order = ATCOrder.Ascending,
                setsAndLegsMode = SetsAndLegs.FirstTo,
                setCount = 1,
                legCount = 1,
                soundEnabled = false,
                Penalties = new PenaltySettings()
            };

            var game = new ATCGame(Guid.NewGuid(), settings, new List<Guid> { p1.GetID(), p2.GetID() });

            int guard = 0;
            while (!game.IsPlayerFinished(p1.GetID()) && guard++ < 2000)
            {
                Guid cur = game.GetCurrentPlayerId();
                if (cur == p1.GetID())
                {
                    int t = game.GetCurrentTarget(cur);
                    if (t == -1) break;
                    game.AddThrow(new Throw(DartMultiplier.Triple, t, HitType.Board, true, t));
                }
                else
                {
                    game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1));
                }
            }

            Check(game.IsFinished(), "match-finished");

            // Secure persistent payloads (such as game_meta), including JSON format check.
            string jTargets = Newtonsoft.Json.JsonConvert.SerializeObject(game.GetTargetNumbers());
            string jMults = Newtonsoft.Json.JsonConvert.SerializeObject(
                game.GetTargetMultipliers().ConvertAll(m => (int)m));
            string jProgress = Newtonsoft.Json.JsonConvert.SerializeObject(
                game.GetTargetIndices().ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));

            // ---- Simulate load: fresh singles steps + indices 0 ----
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ATCGame).GetField("targets", flags).SetValue(game,
                Enumerable.Range(1, 20).Concat(new[] { 25 }).ToList());
            typeof(ATCGame).GetField("targetMultipliers", flags).SetValue(game,
                Enumerable.Repeat(DartMultiplier.Single, 21).ToList());
            var indices = (Dictionary<Guid, int>)typeof(ATCGame).GetField("playerCurrentIndex", flags).GetValue(game);
            foreach (var pid in game.GetPlayerIDs())
                indices[pid] = 0;

            // ---- Restore like repository (JSON read back) ----
            var restoredTargets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<int>>(jTargets);
            var restoredMultInts = Newtonsoft.Json.JsonConvert.DeserializeObject<List<int>>(jMults);
            var restoredMults = restoredMultInts.ConvertAll(i => (DartMultiplier)i);
            var restoredRaw = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(jProgress);
            var restoredIndices = new Dictionary<Guid, int>();
            foreach (var kvp in restoredRaw)
                restoredIndices[Guid.Parse(kvp.Key)] = kvp.Value;

            game.RestoreProgressAfterLoad(restoredTargets, restoredMults, restoredIndices);

            Check(game.GetCurrentTarget(p1.GetID()) == -1, "still-finished-before-undo");

            game.Undo();

            int uiTarget = game.GetCurrentTarget(p1.GetID());
            var uiMult = game.GetCurrentTargetMultiplier(p1.GetID());
            Check(!game.IsFinished(), "match-not-finished-after-undo");
            Check(!game.IsPlayerFinished(p1.GetID()), "player-not-finished-after-undo");
            Check(uiTarget == 20 && uiMult == DartMultiplier.Triple,
                "target-back-to-T20", $"got={(uiMult == DartMultiplier.Double ? "D" : uiMult == DartMultiplier.Triple ? "T" : "")}{uiTarget}");

            // Fallback path (old saves without keys): deterministic new + replay.
            game.RestoreProgressAfterLoad(null, null, null);
            Check(game.GetTargetNumbers().Count == 20, "fallback-no-bull",
                $"count={game.GetTargetNumbers().Count}");
        }
        catch (Exception ex)
        {
            Debug.Log($"ATC_TRIPLES: EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            failures++;
        }

        Debug.Log($"ATC_TRIPLES: RESULT failures={failures}");
    }

    /// <summary>
    /// Regression for: Undo after several misses of the second player (bot)
    /// threw ArgumentOutOfRangeException in CalculateStreakBeforeUndo
    /// (targets[-1]), because there was no valid hit.
    /// </summary>
    [MenuItem("Darts/Tests/ATC Undo Bot All-Miss")]
    public static void RunBotAllMissUndoFromMenu()
    {
        RunBotAllMissUndo();
    }

    public static void RunBotAllMissUndo()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            Debug.Log($"ATC_UNDO_MISS: {(cond ? "PASS" : "FAIL")} {name} {detail}");
            if (!cond) failures++;
        }

        try
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

            Throw Miss() => new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);

            // Player 1 ends his visit with 3 misses -> Switch to Player 2.
            for (int i = 0; i < 3; i++)
                game.AddThrow(Miss());

            Check(game.GetCurrentPlayerId() == p2.GetID(), "p2-active-after-p1-visit");

            // Player 2 (bot role) throws 2 misses.
            // Here, because the Streak rebuild accessed targets[-1].
            game.AddThrow(Miss());
            game.AddThrow(Miss());

            int targetBefore = game.GetCurrentTarget(p2.GetID());
            var turnBefore = game.GetCurrentTurnOfPlayer(p2.GetID());
            int throwsBefore = turnBefore != null ? turnBefore.GetThrowCount() : -1;

            game.Undo();

            var turnAfter = game.GetCurrentTurnOfPlayer(p2.GetID());
            int throwsAfter = turnAfter != null ? turnAfter.GetThrowCount() : -1;

            Check(game.GetCurrentPlayerId() == p2.GetID(), "p2-stays-active-after-undo");
            Check(targetBefore == 1 && game.GetCurrentTarget(p2.GetID()) == 1, "target-unchanged",
                $"before={targetBefore} after={game.GetCurrentTarget(p2.GetID())}");
            Check(throwsBefore == 2 && throwsAfter == 1, "one-throw-removed",
                $"before={throwsBefore} after={throwsAfter}");
            Check(!game.IsFinished(), "match-not-finished");
        }
        catch (Exception ex)
        {
            Debug.Log($"ATC_UNDO_MISS: EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            failures++;
        }

        Debug.Log($"ATC_UNDO_MISS: RESULT failures={failures}");
    }
}
