using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using System;

[System.Serializable]
public class Database
{
    [JsonProperty] private List<GameSummary> gameSummaries;
    [JsonProperty] private List<BasePlayer> activePlayers;
    [JsonProperty] private List<BasePlayer> reservePlayers;

    [JsonProperty] private X01GameSettings x01Settings;
    [JsonProperty] private CricketGameSettings cricketSettings;
    [JsonProperty] private ATCGameSettings atcSettings;

    [JsonProperty] private SetsAndLegs persistentSetsAndLegs = SetsAndLegs.BestOf;
    [JsonProperty] private int persistentSetCount = 1;
    [JsonProperty] private int persistentLegCount = 1;

    [JsonProperty] private GameMode lastGameMode;

    public List<BasePlayer> GetPlayers()
    {
        return activePlayers
            .Union(reservePlayers)
            .Where(p => !p.GotDeleted())
            .OrderBy(p => p.GetName())
            .ToList();
    }

    public List<BasePlayer> GetActivePlayers()
    {
        return activePlayers
            .Where(p => !p.GotDeleted())
            .ToList();
    }

    public List<BasePlayer> GetReservePlayers()
    {
        return reservePlayers
            .Where(p => !p.GotDeleted())
            .ToList();
    }

    public List<BasePlayer> GetAllPlayers()
    {
        return activePlayers
            .Concat(reservePlayers)
            .ToList();
    }

    public BasePlayer GetPlayerByID(Guid id)
    {
        return activePlayers
            .Concat(reservePlayers)
            .FirstOrDefault(p => p.GetID() == id);
    }

    public string GetPlayerNameByID(Guid playerID)
    {
        BasePlayer playerToFind = GetAllPlayers().Find(p => p.GetID() == playerID);

        if (playerToFind != null)
        {
            return playerToFind.GetName();
        }

        Debug.LogWarning($"Kein Spieler mit ID {playerID} gefunden.");
        return null;
    }

    public List<GameSummary> GetGameSummaries()
    {
        return gameSummaries
            .OrderByDescending(g => g.GetSortTimestamp())
            .ToList();
    }

    public GameSummary GetGameSummary(Guid id)
    {
        return gameSummaries.FirstOrDefault(g => g.Id == id);
    }

    public X01GameSettings GetX01Settings()
    {
        return x01Settings;
    }

    public CricketGameSettings GetCricketSettings()
    {
        return cricketSettings;
    }

    public ATCGameSettings GetATCSettings()
    {
        return atcSettings;
    }

    public GameMode GetLastGameMode()
    {
        return lastGameMode;
    }

    public SetsAndLegs GetSetsAndLegsMode()
    {
        return persistentSetsAndLegs;
    }

    public int GetSetCount()
    {
        return persistentSetCount;
    }

    public int GetLegCount()
    {
        return persistentLegCount;
    }

    public Database()
    {
        gameSummaries = new List<GameSummary>();
        activePlayers = new List<BasePlayer>();
        reservePlayers = new List<BasePlayer>();

        ApplyStandardSettings();
    }

    public Guid AddPlayer(string playerName)
    {
        Guid newId = Guid.NewGuid();

        Player newPlayer = new Player(newId, playerName);
        reservePlayers.Add(newPlayer);

        Debug.Log($"Player {playerName} mit ID {newId} wurde angelegt.");

        return newId;
    }

    public Guid AddDartBot(DartBotDifficulty difficulty)
    {
        int nextBotId = GetNextFreeBotNumber();
        Guid newId = Guid.NewGuid();
        DartBot newBot = new DartBot(newId, $"Bot {nextBotId}", difficulty, nextBotId);
        reservePlayers.Add(newBot);

        Debug.Log($"DartBot {newBot.GetName()} mit ID {newId} wurde angelegt.");
        return newId;
    }

    public void RenamePlayer(Guid playerID, string newName)
    {
        BasePlayer renamedPlayer = GetPlayers().Find(p => p.GetID() == playerID);

        if (renamedPlayer != null)
        {
            renamedPlayer.Rename(newName);
        }
        else
        {
            Debug.Log("Fehler beim Umbenennen: Player zur ID " + playerID + " nicht gefunden!");
        }
    }

    public void DeletePlayer(Guid playerID)
    {
        BasePlayer player = GetPlayerByID(playerID);

        if (player == null)
        {
            Debug.LogWarning($"Kein Spieler mit ID {playerID} gefunden.");
            return;
        }

        player.Delete();

        MovePlayerToReserveForDelete(player);

        Debug.Log($"Player {player.GetName()} mit ID {playerID} wurde deaktiviert (Soft Delete).");
    }

    public void SetPlayers(List<Guid> activeIDs, List<Guid> reserveIDs)
    {
        List<BasePlayer> allPlayersSnapshot = activePlayers
            .Concat(reservePlayers)
            .ToList();

        List<BasePlayer> newActive = new List<BasePlayer>();
        List<BasePlayer> newReserve = new List<BasePlayer>();

        foreach (Guid id in activeIDs)
        {
            BasePlayer p = allPlayersSnapshot.FirstOrDefault(x => x.GetID() == id);

            if (p != null)
                newActive.Add(p);
        }

        foreach (Guid id in reserveIDs)
        {
            BasePlayer p = allPlayersSnapshot.FirstOrDefault(x => x.GetID() == id);

            if (p != null)
                newReserve.Add(p);
        }

        activePlayers = newActive;
        reservePlayers = newReserve;
    }

    public void SetActivePlayerOrder(List<Guid> newOrder)
    {
        List<BasePlayer> reordered = new List<BasePlayer>();

        foreach (Guid id in newOrder)
        {
            BasePlayer p = GetPlayerByID(id);
            if (p != null)
                reordered.Add(p);
        }

        activePlayers = reordered;
    }

    public void SetPlayerVisibleInStatistics(Guid id, bool isVisible)
    {
        GetPlayerByID(id).ShowInStatistics(isVisible);
        Debug.Log("[Database] Spieler " + id + " ist sichtbar in der Statistik: " + isVisible);
    }

    public void RebuildPlayerStatistics()
    {
        foreach (var player in GetAllPlayers())
        {
            player.ResetTimeStats();
        }

        var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();

        foreach (var kvp in allStats)
        {
            var summary = GetGameSummary(kvp.Key);
            if (summary == null || !summary.IsFinished)
                continue;

            foreach (var playerStat in kvp.Value)
            {
                var player = GetPlayerByID(playerStat.Key);
                if (player == null)
                    continue;

                player.ApplyTimebasedGameStatsFromSummary(summary, playerStat.Value, 0f);
            }
        }
    }

    public GameMode? DeleteGame(Guid id)
    {
        var summary = gameSummaries.FirstOrDefault(g => g.Id == id);

        if (summary == null)
        {
            Debug.LogWarning($"Kein Game mit ID {id} gefunden.");
            return null;
        }

        gameSummaries.Remove(summary);

        Debug.Log($"Game mit ID {id} wurde gelöscht.");

        return summary.GameMode;
    }

    public X01Game PrepareX01Game(X01GameSettings d)
    {
        X01GameSettings data = (X01GameSettings)d.Clone();

        StorePersistentSetsAndLegs(data.setsAndLegsMode, data.setCount, data.legCount);

        List<Guid> playerIDs = new List<Guid>();

        foreach (BasePlayer p in activePlayers)
            playerIDs.Add(p.GetID());

        data.Penalties = AppSettingsManager.Instance.Settings.Penalties.Clone();
        data.soundEnabled = AppSettingsManager.Instance.Settings.Sound.Enabled;

        X01Game newGame = new X01Game(Guid.NewGuid(), data, playerIDs);

        return newGame;
    }

    public CricketGame PrepareCricketGame(CricketGameSettings data)
    {
        StorePersistentSetsAndLegs(data.setsAndLegsMode, data.setCount, data.legCount);

        List<Guid> playerIDs = new List<Guid>();

        foreach (BasePlayer p in activePlayers)
            playerIDs.Add(p.GetID());

        data.Penalties = AppSettingsManager.Instance.Settings.Penalties.Clone();
        data.soundEnabled = AppSettingsManager.Instance.Settings.Sound.Enabled;

        CricketGame newGame = new CricketGame(Guid.NewGuid(), data, playerIDs);

        return newGame;
    }

    public ATCGame PrepareATCGame(ATCGameSettings data)
    {
        StorePersistentSetsAndLegs(data.setsAndLegsMode, data.setCount, data.legCount);

        List<Guid> playerIDs = new List<Guid>();

        foreach (BasePlayer p in activePlayers)
            playerIDs.Add(p.GetID());

        data.Penalties = AppSettingsManager.Instance.Settings.Penalties.Clone();
        data.soundEnabled = AppSettingsManager.Instance.Settings.Sound.Enabled;

        ATCGame newGame = new ATCGame(Guid.NewGuid(), data, playerIDs);

        return newGame;
    }

    public void InitializeAfterLoad()
    {
        RunIntegrityCheck();
    }

    public void DeleteGames(GameMode? mode = null)
    {
        int removed;

        if (mode == null)
        {
            removed = gameSummaries.Count;
            gameSummaries.Clear();
        }
        else
        {
            removed = gameSummaries.RemoveAll(g => g.GameMode == mode.Value);
        }

        Debug.Log($"[Database] {removed} Games gelöscht.");
    }

    public void SetLastGameMode(GameMode mode)
    {
        lastGameMode = mode;
    }

    public void RunIntegrityCheck()
    {
        List<BasePlayer> deletedPlayers = GetAllPlayers()
            .Where(p => p.GotDeleted())
            .ToList();

        foreach (Player player in deletedPlayers)
        {
            Guid id = player.GetID();

            bool stillUsed = gameSummaries.Any(g =>
                g.PlayerIds.Contains(id)
            );

            if (!stillUsed)
            {
                activePlayers.Remove(player);
                reservePlayers.Remove(player);

                Debug.Log($"[Integrity] Player {id} hard deleted (no references).");
            }
        }
    }

    private void MovePlayerToReserveForDelete(BasePlayer p)
    {
        activePlayers.Remove(p);
        reservePlayers.Add(p);
    }

    private void ApplyStandardSettings()
    {
        ApplyStandardSettingsX01();
        ApplyStandardSettingsCricket();
        ApplyStandardSettingsATC();
        persistentSetsAndLegs = SetsAndLegs.BestOf;
        persistentLegCount = 1;
        persistentSetCount = 1;
    }

    private void ApplyStandardSettingsX01()
    {
        x01Settings = new X01GameSettings
        {
            pointTarget = 501,
            checkoutType = CheckoutType.Double,
            setCount = 1,
            legCount = 1
        };
    }

    private void ApplyStandardSettingsCricket()
    {
        cricketSettings = new CricketGameSettings
        {
            pointsEnabled = true,
            cutThroatEnabled = false,
            setCount = 1,
            legCount = 1
        };
    }

    private void ApplyStandardSettingsATC()
    {
        atcSettings = new ATCGameSettings
        {
            targetType = ATCTargetType.Singles,
            order = ATCOrder.Ascending,
            setCount = 1,
            legCount = 1
        };
    }

    private void StorePersistentSetsAndLegs(SetsAndLegs setsAndLegs, int sets, int legs)
    {
        persistentSetsAndLegs = setsAndLegs;
        persistentSetCount = sets;
        persistentLegCount = legs;
    }

    private int GetNextFreeBotNumber()
    {
        HashSet<int> usedNumbers = GetAllPlayers()
            .OfType<DartBot>()
            .Select(bot => bot.GetNumber())
            .ToHashSet();

        int candidate = 1;

        while (usedNumbers.Contains(candidate))
        {
            candidate++;
        }

        return candidate;
    }

    public void AddLoadedPlayer(BasePlayer player)
    {
        if (player == null)
            return;

        if (activePlayers.Any(p => p.GetID() == player.GetID()))
            return;

        if (reservePlayers.Any(p => p.GetID() == player.GetID()))
            return;

        reservePlayers.Add(player);
    }

    internal void AddGameSummary(GameSummary summary)
    {
        if (summary == null)
            return;

        var existing = gameSummaries.FirstOrDefault(g => g.Id == summary.Id);
        if (existing != null)
            gameSummaries.Remove(existing);

        gameSummaries.Add(summary);
    }

    internal void RemoveGameSummary(Guid id)
    {
        var summary = gameSummaries.FirstOrDefault(g => g.Id == id);
        if (summary != null)
            gameSummaries.Remove(summary);
    }

    internal void UpdateSetupStartPositionsFromLists()
    {
        for (int i = 0; i < activePlayers.Count; i++)
            activePlayers[i].SetSetupStartPosition(i);

        foreach (var player in reservePlayers)
            player.SetSetupStartPosition(-1);
    }

    internal void ApplySetupPlayerOrderFromFlags()
    {
        var allPlayers = GetAllPlayers();

        var newActive = allPlayers
            .Where(p => !p.GotDeleted() && p.GetSetupStartPosition() >= 0)
            .OrderBy(p => p.GetSetupStartPosition())
            .ToList();

        if (newActive.Count == 0 && activePlayers.Count > 0)
            return;

        activePlayers = newActive;
        reservePlayers = allPlayers
            .Where(p => !newActive.Contains(p))
            .ToList();
    }

    internal void SetX01Settings(X01GameSettings x01Settings)
    {
        if (x01Settings != null)
            this.x01Settings = x01Settings;
    }

    internal void SetCricketSettings(CricketGameSettings cricketSettings)
    {
        if (cricketSettings != null)
            this.cricketSettings = cricketSettings;
    }

    internal void SetATCSettings(ATCGameSettings atcSettings)
    {
        if (atcSettings != null)
            this.atcSettings = atcSettings;
    }

    internal void SetSetupState(GameMode gameMode, SetsAndLegs setsAndLegsMode, int setCount, int legCount)
    {
        lastGameMode = gameMode;
        StorePersistentSetsAndLegs(setsAndLegsMode, System.Math.Max(1, setCount), System.Math.Max(1, legCount));
    }

    internal void RebuildAllPlayerStatistics()
    {
        foreach (var player in GetAllPlayers())
            player.ResetAllStats();

        var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();

        foreach (var kvp in allStats)
        {
            var summary = GetGameSummary(kvp.Key);
            if (summary == null || !summary.IsFinished)
                continue;

            foreach (var playerStat in kvp.Value)
            {
                var player = GetPlayerByID(playerStat.Key);
                if (player == null)
                    continue;

                player.ApplyGameStatsFromSummary(summary, playerStat.Value, 0f);
            }
        }
    }
}
