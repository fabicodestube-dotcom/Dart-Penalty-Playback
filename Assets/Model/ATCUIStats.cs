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

    [JsonProperty]
    public DartMultiplier currentTargetMultiplier;

    [JsonProperty] public int totalTargets;
    [JsonProperty] public int targetsHit;

    [JsonProperty] public int hitsInRound;

    [JsonProperty] public bool hasStreak;
    [JsonProperty] public int currentHitStreak;


    // ===========================
    // ======= KONSTRUKTOR =======
    // ===========================

    public ATCUIStats(ATCGame game, BasePlayer player)
    {
        playerID = player.GetID();
        playerName = player.GetName();

        isActive = game.GetCurrentPlayerId() == playerID;

        currentTarget = game.GetCurrentTarget(playerID);
        currentTargetMultiplier = game.GetCurrentTargetMultiplier(playerID);
        isFinished = game.IsPlayerFinished(playerID);
        hasStarted = game.HasPlayerStarted(playerID);
        targetType = game.GetSettingsAsATC().targetType;

        hitsInRound = game.GetHitsInCurrentRound(playerID);

        // Live-Streak aus den Modell-Stats (nicht aus hitsInRound ableiten:
        // das enthält bei inaktiven Spielern die Treffer des letzten Besuchs).
        // Wird bei Fehlwurf sofort zurückgesetzt und bei Undo wiederhergestellt.
        if (game.GetPlayerStats().TryGetValue(playerID, out var baseStats)
            && baseStats is GameStatsATC atcStats)
        {
            hasStreak = atcStats.hasStreak;
            currentHitStreak = atcStats.currentHitStreak;
        }
        else
        {
            hasStreak = false;
            currentHitStreak = 0;
        }

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
