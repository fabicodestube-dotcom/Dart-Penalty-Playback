using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.AI;

[System.Serializable]
public class ATCUIStats
{
    [JsonProperty]
    public Guid playerID;

    [JsonProperty]
    public bool isActive;

    [JsonProperty]
    public string playerName;

    [JsonProperty]
    public int currentTarget;

    [JsonProperty]
    public List<Throw> lastThreeThrows;
    [JsonProperty]
    public int turnHitCount;

    [JsonProperty]
    public int wonSets;

    [JsonProperty]
    public int wonLegs;

    [JsonProperty]
    public int thrownDartsCount;

    [JsonProperty]
    public string progress;

    [JsonProperty]
    public float averageHitRate;

    [JsonProperty]
    public bool isFinished;

    [JsonProperty]
    public bool hasStarted;

    [JsonProperty]
    public ATCTargetType targetType;

    [JsonProperty] public int totalTargets;
    [JsonProperty] public int targetsHit;

    [JsonProperty] public int hitsInRound;


    // ===========================
    // ======= KONSTRUKTOR =======
    // ===========================

    public ATCUIStats(ATCGame game, BasePlayer player)
    {
        playerID = player.GetID();
        playerName = player.GetName();

        isActive = game.GetCurrentPlayerId() == playerID;

        currentTarget = game.GetCurrentTarget(playerID);
        isFinished = game.IsPlayerFinished(playerID);
        hasStarted = game.HasPlayerStarted(playerID);
        targetType = game.GetSettingsAsATC().targetType;

        hitsInRound = game.GetHitsInCurrentRound(playerID);

        wonSets = game.GetWonSets(playerID);
        wonLegs = game.GetWonLegs(playerID);

        thrownDartsCount = game.GetTotalThrows(playerID);

        targetsHit = game.GetTotalHits(playerID);
        totalTargets = game.GetTotalTargets();

        progress = $"{targetsHit}/{totalTargets}";

        averageHitRate = thrownDartsCount > 0 
            ? (float)targetsHit / thrownDartsCount * 100f 
            : 0f;

        lastThreeThrows = GetDisplayThrows(game, isActive);
    }


    // ===========================
    // ========= HELPER ==========
    // ===========================

    private List<Throw> GetDisplayThrows(ATCGame game, bool isActivePlayer)
    {
        Turn turn = isActivePlayer
            ? game.GetCurrentTurnOfPlayer(playerID)
            : game.GetLastTurnInCurrentLeg(playerID);

        return turn?.GetThrows() ?? new List<Throw>();
    }
}
