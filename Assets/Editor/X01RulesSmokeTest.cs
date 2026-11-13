using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class X01RulesSmokeTest
{
    [MenuItem("Tools/X01/Run Rules Smoke Test")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        CheckMasterOutBust();
        CheckMasterOutDoubleFinish();
        CheckMasterOutBullFinish();
        CheckMasterOutSuddenDeath();

        Debug.Log("[X01-SMOKE] PASS");
    }

    private static void CheckMasterOutBust()
    {
        var playerId = Guid.NewGuid();
        var game = CreateGame(2, playerId, out var bust);

        game.AddThrow(new Throw(DartMultiplier.Single, 2));

        Check(bust.value == true, "Master-out must bust a single 2 finish.");
        Check(game.GetScore(playerId) == 2, "Bust must restore the score before the turn.");
    }

    private static void CheckMasterOutDoubleFinish()
    {
        var playerId = Guid.NewGuid();
        var game = CreateGame(2, playerId, out var bust);

        game.AddThrow(new Throw(DartMultiplier.Double, 1));

        Check(bust.value != true, "Master-out must accept a double finish.");
        Check(game.IsFinished(), "Master-out double finish must win the match.");
    }

    private static void CheckMasterOutBullFinish()
    {
        var playerId = Guid.NewGuid();
        var game = CreateGame(50, playerId, out var bust);

        game.AddThrow(new Throw(DartMultiplier.Double, 25));

        Check(bust.value != true, "Master-out must accept a double bull finish.");
        Check(game.IsFinished(), "Master-out double bull finish must win the match.");
    }

    private static void CheckMasterOutSuddenDeath()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var game = new X01Game(
            Guid.NewGuid(),
            CreateSettings(2),
            new List<Guid> { playerA, playerB });

        bool triggered = false;
        game.OnSuddenDeath += () => triggered = true;

        var miss = new Throw(DartMultiplier.Single, 0, HitType.Wall);
        game.AddThrow(miss);
        game.AddThrow(miss);
        game.AddThrow(miss);

        Check(triggered, "Master-out sudden death must trigger when both players are on D1.");
    }

    private static X01Game CreateGame(int pointTarget, Guid playerId, out BustFlag bust)
    {
        var game = new X01Game(
            Guid.NewGuid(),
            CreateSettings(pointTarget),
            new List<Guid> { playerId });
        var flag = new BustFlag();

        game.OnTurnCompleted += (id, turn) => flag.value = turn.IsBust;

        bust = flag;
        return game;
    }

    private static X01GameSettings CreateSettings(int pointTarget)
    {
        return new X01GameSettings
        {
            setsAndLegsMode = SetsAndLegs.BestOf,
            setCount = 1,
            legCount = 1,
            pointTarget = pointTarget,
            checkinType = CheckinType.StraightIn,
            checkoutType = CheckoutType.Master
        };
    }

    private sealed class BustFlag
    {
        public bool? value;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("[X01-SMOKE] " + message);
    }
}
