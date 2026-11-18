using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

[Serializable]
public class CricketNumberStats
{
    public int number;
    public int hits;
    public bool isLocked;
}

[Serializable]
public class PlayerLiveStats
{
    public GameMode mode;

    public Guid playerID;
    public string playerName;
    public bool isActive;

    public int currentScore;
    public int currentTarget;
    public DartMultiplier currentTargetMultiplier;
    public ATCTargetType targetType;

    public List<Throw> lastThreeThrows = new List<Throw>();
    public List<CricketNumberStats> numbers = new List<CricketNumberStats>();

    public bool isBust;
    public int turnScore;

    public int wonSets;
    public int wonLegs;
    public int thrownDartsCount;

    public float averageScorePerTurn;
    public float averageMPR;
    public float averageHitRate;

    public int totalTargets;
    public int targetsHit;
    public int hitsInRound;
    public bool hasStreak;
    public int currentHitStreak;

    public string progress;
    public bool isFinished;
    public bool hasStarted;

    public static PlayerLiveStats Create(Game game, BasePlayer player)
    {
        return game switch
        {
            X01Game x01 => CreateX01(x01, player),
            CricketGame cricket => CreateCricket(cricket, player),
            ATCGame atc => CreateATC(atc, player),
            _ => new PlayerLiveStats
            {
                playerID = player.GetID(),
                playerName = player.GetName()
            }
        };
    }

    public static PlayerLiveStats CreateX01(X01Game game, BasePlayer player)
    {
        Guid playerID = player.GetID();
        bool isActive = game.GetCurrentPlayerId() == playerID;

        Turn turn = isActive
            ? game.GetCurrentTurnOfPlayer(playerID)
            : game.GetLastTurnInCurrentLeg(playerID);

        var throws = turn?.GetThrows() ?? new List<Throw>();

        int turnScore = 0;
        foreach (var t in throws)
            turnScore += t != null ? t.GetScore() : 0;

        var finishedTurns = game.GetAllTurns(playerID).Where(t => t.IsCompleted()).ToList();

        return new PlayerLiveStats
        {
            mode = GameMode.X01,
            playerID = playerID,
            playerName = player.GetName(),
            isActive = isActive,
            currentScore = game.GetScore(playerID),
            lastThreeThrows = throws,
            isBust = turn != null && turn.IsBust,
            turnScore = turnScore,
            wonSets = game.GetWonSets(playerID),
            wonLegs = game.GetWonLegs(playerID),
            thrownDartsCount = game.GetDartsInCurrentLeg(playerID),
            averageScorePerTurn = finishedTurns.Count == 0
                ? 0f
                : (float)Math.Round(finishedTurns.Sum(t => t.GetTurnScore()) / (float)finishedTurns.Count, 2)
        };
    }

    public static PlayerLiveStats CreateCricket(CricketGame game, BasePlayer player)
    {
        Guid playerID = player.GetID();
        bool isActive = game.GetCurrentPlayerId() == playerID;

        Turn turn = isActive
            ? game.GetCurrentTurnOfPlayer(playerID)
            : game.GetLastTurnForPlayer(playerID);

        var numberOrder = new[] { 20, 19, 18, 17, 16, 15, 25 };
        var numbers = new List<CricketNumberStats>(numberOrder.Length);

        foreach (var number in numberOrder)
        {
            numbers.Add(new CricketNumberStats
            {
                number = number,
                hits = game.GetHits(playerID, number),
                isLocked = game.IsLocked(number)
            });
        }

        return new PlayerLiveStats
        {
            mode = GameMode.Cricket,
            playerID = playerID,
            playerName = player.GetName(),
            isActive = isActive,
            currentScore = game.GetScore(playerID),
            wonSets = game.GetWonSets(playerID),
            wonLegs = game.GetWonLegs(playerID),
            thrownDartsCount = game.GetDartsInCurrentLeg(playerID),
            averageMPR = game.GetMPR(playerID),
            numbers = numbers,
            lastThreeThrows = turn?.GetThrows() ?? new List<Throw>()
        };
    }

    public static PlayerLiveStats CreateATC(ATCGame game, BasePlayer player)
    {
        Guid playerID = player.GetID();
        bool isActive = game.GetCurrentPlayerId() == playerID;

        Turn turn = isActive
            ? game.GetCurrentTurnOfPlayer(playerID)
            : game.GetLastTurnInCurrentLeg(playerID);

        bool hasStreak = false;
        int currentHitStreak = 0;

        if (game.GetPlayerStats().TryGetValue(playerID, out var baseStats) && baseStats.atc != null)
        {
            hasStreak = baseStats.atc.hasStreak;
            currentHitStreak = baseStats.atc.currentHitStreak;
        }

        int targetsHit = game.GetTotalHits(playerID);
        int totalTargets = game.GetTotalTargets();
        int thrownDartsCount = game.GetTotalThrows(playerID);

        return new PlayerLiveStats
        {
            mode = GameMode.ATC,
            playerID = playerID,
            playerName = player.GetName(),
            isActive = isActive,
            currentTarget = game.GetCurrentTarget(playerID),
            currentTargetMultiplier = game.GetCurrentTargetMultiplier(playerID),
            isFinished = game.IsPlayerFinished(playerID),
            hasStarted = game.HasPlayerStarted(playerID),
            targetType = game.GetSettingsAsATC().targetType,
            hitsInRound = game.GetHitsInCurrentRound(playerID),
            hasStreak = hasStreak,
            currentHitStreak = currentHitStreak,
            wonSets = game.GetWonSets(playerID),
            wonLegs = game.GetWonLegs(playerID),
            thrownDartsCount = thrownDartsCount,
            targetsHit = targetsHit,
            totalTargets = totalTargets,
            progress = $"{targetsHit}/{totalTargets}",
            averageHitRate = thrownDartsCount > 0 ? (float)targetsHit / thrownDartsCount * 100f : 0f,
            lastThreeThrows = turn?.GetThrows() ?? new List<Throw>()
        };
    }
}
