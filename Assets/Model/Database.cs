using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using System;

[System.Serializable]
public class Database
{
    // =========================================================
    // DATA STORAGE
    // =========================================================

    [JsonProperty] private List<Game> games;
    [JsonProperty] private List<BasePlayer> activePlayers;
    [JsonProperty] private List<BasePlayer> reservePlayers;

    [JsonProperty] private X01GameSettings x01Settings;
    [JsonProperty] private CricketGameSettings cricketSettings;
    [JsonProperty] private ATCGameSettings atcSettings;


    [JsonProperty] private SetsAndLegs persistentSetsAndLegs = SetsAndLegs.BestOf;
    [JsonProperty] private int persistentSetCount = 1;
    [JsonProperty]private int persistentLegCount = 1;

    [JsonProperty] private GameMode lastGameMode;

// ================= PUBLIC GETTERS =================

    // PLAYERS - QUERIES
    // =========================================================

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

    // GAMES - CORE
    // =========================================================

    public List<Game> GetGames()
    {
        return games
            .OrderByDescending(g => g.GetSortTimestamp())
            .ToList();
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

// ================= PUBLIC METHODS =================

    // CONSTRUCTOR / INITIALIZATION
    // =========================================================

    public Database()
    {
        games = new List<Game>();
        activePlayers = new List<BasePlayer>();
        reservePlayers = new List<BasePlayer>();
        
        ApplyStandardSettings();
    }

    // PLAYERS - CRUD
    // =========================================================

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

        // Soft Delete: nur Flag setzen, keine physische Entfernung
        player.Delete();

        MovePlayerToReserveForDelete(player);

        Debug.Log($"Player {player.GetName()} mit ID {playerID} wurde deaktiviert (Soft Delete).");
    }

    // PLAYER STATE MANAGEMENT
    // =========================================================

    public void SetPlayers(List<Guid> activeIDs, List<Guid> reserveIDs)
    {
        // Snapshot aller Spieler, um stabile Referenzen zu behalten
        List<BasePlayer> allPlayersSnapshot = activePlayers
            .Concat(reservePlayers)
            .ToList();

        List<BasePlayer> newActive = new List<BasePlayer>();
        List<BasePlayer> newReserve = new List<BasePlayer>();

        // Active Liste rekonstruieren
        foreach (Guid id in activeIDs)
        {
            BasePlayer p = allPlayersSnapshot.FirstOrDefault(x => x.GetID() == id);

            if (p != null)
                newActive.Add(p);
        }

        // Reserve Liste rekonstruieren
        foreach (Guid id in reserveIDs)
        {
            BasePlayer p = allPlayersSnapshot.FirstOrDefault(x => x.GetID() == id);

            if (p != null)
                newReserve.Add(p);
        }

        // atomarer Swap der Listen
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

        foreach (var game in games)
        {
            if (game.IsFinished())
            {
                foreach (var playerId in game.GetPlayerIDs())
                {
                    GetPlayerByID(playerId)?.ApplyTimebasedGameStats(game);
                }
            }    
        }
    }

    public Game LoadGame(Guid id)
    {
        return games
            .FirstOrDefault(g => g.GetID() == id);
    }

    public GameMode? DeleteGame(Guid id)
    {
        var game = games.FirstOrDefault(g => g.GetID() == id);

        if (game == null)
        {
            Debug.LogWarning($"Kein Game mit ID {id} gefunden.");
            return null;
        }

        games.Remove(game);

        Debug.Log($"Game mit ID {id} wurde gelöscht.");

        return game.GetGameMode();
    }

    // GAMES - X01
    // =========================================================

    public X01Game PrepareX01Game(X01GameSettings d)
    {
        X01GameSettings data = (X01GameSettings) d.Clone();

        StorePersistentSetsAndLegs(data.setsAndLegsMode, data.setCount, data.legCount);

        List<Guid> playerIDs = new List<Guid>();

        foreach (BasePlayer p in activePlayers)
            playerIDs.Add(p.GetID());

        // Penalty Settings aus globaler App-Konfiguration kopieren
        data.Penalties = AppSettingsManager.Instance.Settings.Penalties.Clone();

        // Sound Settings aus globaler App-Konfiguration kopieren
        data.soundEnabled = AppSettingsManager.Instance.Settings.Sound.Enabled;

        X01Game newGame = new X01Game(Guid.NewGuid(), data, playerIDs);

        games.Add(newGame);

        return newGame;
    }

    public CricketGame PrepareCricketGame(CricketGameSettings data)
    {
        StorePersistentSetsAndLegs(data.setsAndLegsMode, data.setCount, data.legCount);

        List<Guid> playerIDs = new List<Guid>();

        foreach (BasePlayer p in activePlayers)
            playerIDs.Add(p.GetID());

        // Penalty Settings aus globaler App-Konfiguration kopieren
        data.Penalties = AppSettingsManager.Instance.Settings.Penalties.Clone();

        // Sound Settings aus globaler App-Konfiguration kopieren
        data.soundEnabled = AppSettingsManager.Instance.Settings.Sound.Enabled;

        CricketGame newGame = new CricketGame(Guid.NewGuid(), data, playerIDs);

        games.Add(newGame);

        return newGame;
    }

    public ATCGame PrepareATCGame(ATCGameSettings data)
    {
        StorePersistentSetsAndLegs(data.setsAndLegsMode, data.setCount, data.legCount);

        List<Guid> playerIDs = new List<Guid>();

        foreach (BasePlayer p in activePlayers)
            playerIDs.Add(p.GetID());

        // Penalty Settings aus globaler App-Konfiguration kopieren
        data.Penalties = AppSettingsManager.Instance.Settings.Penalties.Clone();

        // Sound Settings aus globaler App-Konfiguration kopieren
        data.soundEnabled = AppSettingsManager.Instance.Settings.Sound.Enabled;

        ATCGame newGame = new ATCGame(Guid.NewGuid(), data, playerIDs);

        games.Add(newGame);

        return newGame;
    }

    // GAMES - GENERAL UTIL
    // =========================================================

    public void InitializeAfterLoad()
    {
        // 🔥 Integrity Check direkt beim Laden der Datenbank
        RunIntegrityCheck();

        // foreach (var game in games)
        // {
        //     game.InitializeAfterLoad();
        // }
    }

    public void DeleteGames(GameMode? mode = null)
    {
        int removed;

        if (mode == null)
        {
            removed = games.Count;
            games.Clear();
        }
        else
        {
            removed = games.RemoveAll(g => g.GetGameMode() == mode.Value);
        }

        Debug.Log($"[Database] {removed} Games gelöscht.");
    }

    public void SetLastGameMode(GameMode mode)
    {
        lastGameMode = mode;
    }

    // INTEGRITY SYSTEM (ADDED - NO LOGIC CHANGES ELSEWHERE)
    // =========================================================

    /// <summary>
    /// Entfernt dauerhaft (Hard Delete) alle gelöschten Spieler,
    /// die in keinem Game mehr referenziert werden.
    /// </summary>
    public void RunIntegrityCheck()
    {
        List<BasePlayer> deletedPlayers = GetAllPlayers()
            .Where(p => p.GotDeleted())
            .ToList();

        foreach (Player player in deletedPlayers)
        {
            Guid id = player.GetID();

            bool stillUsed = games.Any(g =>
                g.GetPlayerIDs().Contains(id)
            );

            if (!stillUsed)
            {
                activePlayers.Remove(player);
                reservePlayers.Remove(player);

                Debug.Log($"[Integrity] Player {id} hard deleted (no references).");
            }
        }
    }

// ================= PRIVATE HELPERS =================

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
            // Default: points on, normal mode
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
























































































}
