using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.AI;



[System.Serializable]
public class CricketNumberStats
{
    public int number;
    public int hits;
    public bool isLocked;
}


[System.Serializable]
public class CricketUIStats
{
    [JsonProperty]
    public bool isActive;

    [JsonProperty]
    public Guid playerID;

    [JsonProperty]
    public string playerName;

    [JsonProperty]
    public int currentScore;

    [JsonProperty]
    public List<CricketNumberStats> numbers;

    [JsonProperty]
    public int wonSets;

    [JsonProperty]
    public int wonLegs;

    [JsonProperty]
    public int thrownDartsCount;

    [JsonProperty]
    public List<Throw> lastThreeThrows;

    [JsonProperty]
    public float averageMPR;

    private static readonly int[] numberOrder = { 20, 19, 18, 17, 16, 15, 25 };



    // ===========================
    // ======= KONSTRUKTOR =======
    // ===========================

    public CricketUIStats(CricketGame game, BasePlayer player)
    {
        playerID = player.GetID();
        playerName = player.GetName();

        isActive = game.GetCurrentPlayerId() == playerID;

        currentScore = game.GetScore(playerID);

        wonSets = game.GetWonSets(playerID);
        wonLegs = game.GetWonLegs(playerID);
        thrownDartsCount = game.GetDartsInCurrentLeg(playerID);
        averageMPR = game.GetMPR(playerID);

        numbers = new List<CricketNumberStats>();

        foreach (var number in numberOrder)
        {
            numbers.Add(new CricketNumberStats
            {
                number = number,
                hits = game.GetHits(playerID, number),
                isLocked = game.IsLocked(number)
            });
        }

        lastThreeThrows = GetDisplayThrows(game, isActive);
    }


    // ===========================
    // ========= HELPER ==========
    // ===========================

    private List<Throw> GetDisplayThrows(CricketGame game, bool isActivePlayer)
    {
        Turn turn = isActivePlayer
            ? game.GetCurrentTurnOfPlayer(playerID)
            : game.GetLastTurnForPlayer(playerID);

        // Immer alle Darts anzeigen
        return turn?.GetThrows() ?? new List<Throw>();
    }
}