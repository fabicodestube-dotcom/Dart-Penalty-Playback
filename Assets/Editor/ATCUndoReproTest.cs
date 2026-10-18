using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repro/Regression für: ATC Undo nach Match-Ende zeigt Target "Finished".
/// Baut ein echtes ATC-Spiel (Singles, 1 Set/1 Leg), spielt einen Spieler
/// bis Match-Ende, Undo, prüft Target + Finish-Flags.
/// Menü: Darts/Tests/ATC Undo After MatchEnd. Batchmode: -executeMethod ATCUndoReproTest.Run
/// </summary>
public static class ATCUndoReproTest
{
    /// <summary>
    /// Regression für: Bot wirft nicht, wenn nach Leg-Gewinn ein Bot dran ist.
    /// Prüft Event-Reihenfolge: Nach OnLegWon muss OnBotCheck für den neuen
    /// Startspieler kommen (ATCGame.StartNewLeg triggert, wie X01/Cricket).
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
    /// Variante 2: simuliert den LoadGameById-Zustand (playerCurrentIndex wird
    /// nie persistiert → nach Load alle 0, finishedAt gesetzt), dann Undo.
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

            // ---- LoadGameById simulieren: Indizes gehen verloren, finished bleibt ----
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

            // Exakt die Label-Datenquelle der UI (ATCPlayerListItem.SetTarget /
            // ATCInputHandler-Label) prüfen, nicht nur Modell-Getter.
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
                    // Gegner passt (Board-Miss ohne Penalty)
                    game.AddThrow(new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1));
                }
            }

            Check(game.IsPlayerFinished(p1.GetID()), "p1-finished", $"guard={guard}");
            Check(game.IsFinished(), "match-finished");

            int lastTarget = 25;
            Check(game.GetCurrentTarget(p1.GetID()) == -1, "target-minus1-when-finished");

            // Hinweis: kein JSON-Gesamt-Roundtrip — Produktion lädt relational
            // (Sets/Legs/Turns), nie per Game-JSON. Roundtrip-Fälle deckt
            // RunTriplesLoadUndo mit exakter Load-Semantik ab.
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
    /// Exakte LoadGameById-Semantik: frische Singles-Steps + genullte Indizes
    /// (so baut der Repo-Load), dann RestoreProgressAfterLoad + Undo.
    /// Triples-Variante des gemeldeten Bugs (Single20 statt T20).
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

            // Persistierte Payloads sichern (wie game_meta), inkl. JSON-Format-Check.
            string jTargets = Newtonsoft.Json.JsonConvert.SerializeObject(game.GetTargetNumbers());
            string jMults = Newtonsoft.Json.JsonConvert.SerializeObject(
                game.GetTargetMultipliers().ConvertAll(m => (int)m));
            string jProgress = Newtonsoft.Json.JsonConvert.SerializeObject(
                game.GetTargetIndices().ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));

            // ---- Load simulieren: frische Singles-Steps + Indizes 0 ----
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ATCGame).GetField("targets", flags).SetValue(game,
                Enumerable.Range(1, 20).Concat(new[] { 25 }).ToList());
            typeof(ATCGame).GetField("targetMultipliers", flags).SetValue(game,
                Enumerable.Repeat(DartMultiplier.Single, 21).ToList());
            var indices = (Dictionary<Guid, int>)typeof(ATCGame).GetField("playerCurrentIndex", flags).GetValue(game);
            foreach (var pid in game.GetPlayerIDs())
                indices[pid] = 0;

            // ---- Restore wie Repository (JSON zurücklesen) ----
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

            // Fallback-Pfad (alte Saves ohne Keys): deterministisch neu + replayen.
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
}
