using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using SQLite;
using UnityEngine;

public static class SqliteDatabaseRepository
{
    public static void SaveDatabase(Database database)
    {
        if (database == null)
            return;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            try
            {
                connection.BeginTransaction();

                database.UpdateSetupStartPositionsFromLists();

                foreach (var player in database.GetAllPlayers())
                {
                    SavePlayer(connection, player);
                }

                SaveSetupState(connection, database);

                connection.Commit();
                Debug.Log("<color=green>SQLite repository saved game data.</color>");
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.SaveDatabase failed: " + e.Message);
            }
        }
    }

    public static void AggregatePlayerStatistics(Database database)
    {
        if (database == null)
            return;

        foreach (var player in database.GetAllPlayers())
        {
            player.ResetAllStats();
        }

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var finishedGames = connection.Query<GameRow>(@"
                SELECT
                    id AS Id,
                    game_mode AS GameMode,
                    created_at AS CreatedAt,
                    last_activity_at AS LastActivityAt,
                    finished_at AS FinishedAt,
                    current_player_index AS CurrentPlayerIndex,
                    starting_player_index AS StartingPlayerIndex,
                    winner_player_id AS WinnerPlayerId,
                    player_ids AS PlayerIdsRaw
                FROM games
                WHERE finished_at IS NOT NULL
                ORDER BY last_activity_at DESC;");

            foreach (var gameRow in finishedGames)
            {
                var game = CreateGameFromRow(gameRow);
                if (game == null)
                    continue;

                RestoreGameStructure(connection, game);
                RestoreRuntimeGameMeta(connection, gameRow.Id, game);
                game.InitializeAfterLoad();
                game.CalculatePlayerStatsOnSave();

                foreach (var playerId in game.GetPlayerIDs())
                {
                    var player = database.GetPlayerByID(playerId);
                    if (player == null)
                        continue;

                    if (game.GetPlayerStats().TryGetValue(playerId, out var stats))
                    {
                        player.ApplyGameStats(game);
                    }
                }
            }
        }
    }

    public static Database LoadDatabase()
    {
        var database = new Database();

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var players = ReadPlayers(connection);
            foreach (var player in players)
            {
                database.AddLoadedPlayer(player);
            }

            database.ApplySetupPlayerOrderFromFlags();

            RestoreSetupState(connection, database);

            foreach (var gameRow in ReadGames(connection))
            {
                var summary = CreateGameSummaryFromRow(gameRow, connection);
                if (summary == null)
                    continue;

                database.AddGameSummary(summary);
            }
        }

        return database;
    }

    public static Game LoadGameById(Guid id)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var gameRow = ReadGameById(connection, id.ToString());
            if (gameRow == null)
                return null;

            var game = CreateGameFromRow(gameRow);
            if (game == null)
                return null;

            RestoreGameStructure(connection, game);
            RestoreRuntimeGameMeta(connection, gameRow.Id, game);
            game.InitializeAfterLoad();

            return game;
        }
    }

    public static void SaveGame(Game game)
    {
        if (game == null)
            return;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            try
            {
                connection.BeginTransaction();

                var currentPlayerIndex = game.GetPlayerIDs().IndexOf(game.GetCurrentPlayerId());
                if (currentPlayerIndex < 0)
                    currentPlayerIndex = 0;

                var startingPlayerIndex = GetPrivateField(game, "startingPlayerIndex", 0);
                var winnerPlayerId = GetPrivateField(game, "matchWinnerId", (Guid?)null);

                var settingsJson = game.GetSettings() != null ? JsonConvert.SerializeObject(game.GetSettings()) : null;

                connection.Execute(
                    "INSERT OR REPLACE INTO games (id, game_mode, created_at, last_activity_at, finished_at, current_player_index, starting_player_index, winner_player_id, player_ids, settings_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                    game.GetID().ToString(),
                    game.GetGameMode().ToString(),
                    game.GetCreatedAt().ToString("o"),
                    game.GetLastActivityAt().ToString("o"),
                    game.GetFinishedAt() == null ? null : game.GetFinishedAt().Value.ToString("o"),
                    currentPlayerIndex,
                    startingPlayerIndex,
                    winnerPlayerId.HasValue ? winnerPlayerId.Value.ToString() : null,
                    string.Join("|", game.GetPlayerIDs()),
                    settingsJson
                );

                connection.Execute("DELETE FROM game_players WHERE game_id = ?;", game.GetID().ToString());
                for (int i = 0; i < game.GetPlayerIDs().Count; i++)
                {
                    connection.Execute(
                        "INSERT OR REPLACE INTO game_players (game_id, player_id, player_order) VALUES (?, ?, ?);",
                        game.GetID().ToString(),
                        game.GetPlayerIDs()[i].ToString(),
                        i
                    );
                }

                connection.Execute("DELETE FROM game_meta WHERE game_id = ?;", game.GetID().ToString());
                SaveMeta(connection, "game_meta", "game_id", game.GetID().ToString(), game);
                SaveRuntimeGameMeta(connection, game);
                SaveGameSettings(connection, game);

                // Alte Struktur-Zeilen löschen, sonst bleiben nach erneutem
                // Speichern verwaiste Legs/Turns/Throws übrig (neue IDs pro
                // Save) und Restore hängt sie fälschlich ans aktuelle Set.
                DeleteGameStructure(connection, game.GetID().ToString());
                for (int i = 0; i < game.GetSets().Count; i++)
                {
                    SaveSet(connection, game.GetID(), game.GetSets()[i], i);
                }

                SavePlayerGameStats(connection, game);

                connection.Commit();
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.SaveGame failed: " + e.Message);
            }
        }
    }

    private static void DeleteGameStructure(SQLiteConnection connection, string gameId)
    {
        connection.Execute("DELETE FROM throw_meta WHERE throw_id IN (SELECT id FROM throws WHERE game_id = ?);", gameId);
        connection.Execute("DELETE FROM throws WHERE game_id = ?;", gameId);
        connection.Execute("DELETE FROM turn_meta WHERE turn_id IN (SELECT id FROM turns WHERE game_id = ?);", gameId);
        connection.Execute("DELETE FROM turns WHERE game_id = ?;", gameId);
        connection.Execute("DELETE FROM leg_meta WHERE leg_id IN (SELECT id FROM legs WHERE game_id = ?);", gameId);
        connection.Execute("DELETE FROM legs WHERE game_id = ?;", gameId);
        connection.Execute("DELETE FROM set_meta WHERE set_id IN (SELECT id FROM sets WHERE game_id = ?);", gameId);
        connection.Execute("DELETE FROM sets WHERE game_id = ?;", gameId);
    }

    public static void DeleteGame(Guid id)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            DeleteGameStructure(connection, id.ToString());
            connection.Execute("DELETE FROM game_meta WHERE game_id = ?;", id.ToString());
            connection.Execute("DELETE FROM game_players WHERE game_id = ?;", id.ToString());
            connection.Execute("DELETE FROM player_game_stats WHERE game_id = ?;", id.ToString());
            connection.Execute("DELETE FROM games WHERE id = ?;", id.ToString());
        }
    }

    public static void DeleteGamesByMode(GameMode mode)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<GameRow>("SELECT id AS Id FROM games WHERE game_mode = ?;", mode.ToString());
            foreach (var row in rows)
            {
                DeleteGameStructure(connection, row.Id);
                connection.Execute("DELETE FROM game_meta WHERE game_id = ?;", row.Id);
                connection.Execute("DELETE FROM game_players WHERE game_id = ?;", row.Id);
                connection.Execute("DELETE FROM player_game_stats WHERE game_id = ?;", row.Id);
            }
            connection.Execute("DELETE FROM games WHERE game_mode = ?;", mode.ToString());
        }
    }

    public static void DeleteAllGames()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.Execute("DELETE FROM throw_meta;");
            connection.Execute("DELETE FROM throws;");
            connection.Execute("DELETE FROM turn_meta;");
            connection.Execute("DELETE FROM turns;");
            connection.Execute("DELETE FROM leg_meta;");
            connection.Execute("DELETE FROM legs;");
            connection.Execute("DELETE FROM set_meta;");
            connection.Execute("DELETE FROM sets;");
            connection.Execute("DELETE FROM game_players;");
            connection.Execute("DELETE FROM game_meta;");
            connection.Execute("DELETE FROM player_game_stats;");
            connection.Execute("DELETE FROM games;");
        }
    }

    public static Dictionary<Guid, Dictionary<Guid, GameStats>> LoadAllPlayerGameStats()
    {
        var result = new Dictionary<Guid, Dictionary<Guid, GameStats>>();

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<PlayerGameStatRow>(
                "SELECT game_id AS GameId, player_id AS PlayerId, stat_type AS StatType, stat_data AS StatData FROM player_game_stats;");

            foreach (var row in rows)
            {
                if (!Guid.TryParse(row.GameId, out var gameId))
                    continue;
                if (!Guid.TryParse(row.PlayerId, out var playerId))
                    continue;

                GameStats stats = row.StatType switch
                {
                    "x01" => JsonConvert.DeserializeObject<GameStatsX01>(row.StatData),
                    "cricket" => JsonConvert.DeserializeObject<GameStatsCricket>(row.StatData),
                    "atc" => JsonConvert.DeserializeObject<GameStatsATC>(row.StatData),
                    _ => null
                };

                if (stats == null)
                    continue;

                if (!result.TryGetValue(gameId, out var playerStats))
                {
                    playerStats = new Dictionary<Guid, GameStats>();
                    result[gameId] = playerStats;
                }

                playerStats[playerId] = stats;
            }
        }

        return result;
    }

    private static void SavePlayerGameStats(SQLiteConnection connection, Game game)
    {
        connection.Execute("DELETE FROM player_game_stats WHERE game_id = ?;", game.GetID().ToString());

        var stats = game.GetPlayerStats();
        if (stats == null)
            return;

        foreach (var kvp in stats)
        {
            string statType = game.GetGameMode() switch
            {
                GameMode.X01 => "x01",
                GameMode.Cricket => "cricket",
                GameMode.ATC => "atc",
                _ => "unknown"
            };

            string statData = JsonConvert.SerializeObject(kvp.Value, Formatting.None);

            connection.Execute(
                "INSERT OR REPLACE INTO player_game_stats (game_id, player_id, stat_type, stat_data) VALUES (?, ?, ?, ?);",
                game.GetID().ToString(),
                kvp.Key.ToString(),
                statType,
                statData
            );
        }
    }

    public static void SaveAggregatedPlayerStats(Game game)
    {
        if (game == null)
            return;

        SqliteDatabaseService.EnsureSchema();

        var stats = game.GetPlayerStats();
        if (stats == null)
            return;

        foreach (var kvp in stats)
        {
            Guid playerId = kvp.Key;
            GameStats gameStats = kvp.Value;

            var allStats = LoadPlayerStatsAll(playerId) ?? new GameStats(playerId);
            allStats.AddGameStat(gameStats, game.GetID(), gameStats.totalPenaltyCost);
            SavePlayerStatsAll(playerId, allStats);

            GameStats modeStats = LoadPlayerStatsForMode(playerId, game.GetGameMode()) ?? CreateStatsForMode(playerId, game.GetGameMode());
            modeStats.AddGameStat(gameStats, game.GetID(), gameStats.totalPenaltyCost);
            SavePlayerStatsForMode(playerId, modeStats, game.GetGameMode());
        }
    }

    private static GameStats CreateStatsForMode(Guid playerId, GameMode mode)
    {
        return mode switch
        {
            GameMode.X01 => new GameStatsX01(playerId),
            GameMode.Cricket => new GameStatsCricket(playerId),
            GameMode.ATC => new GameStatsATC(playerId),
            _ => new GameStats(playerId)
        };
    }

    public static void SavePlayerStatsAll(Guid playerId, GameStats stats)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var json = JsonConvert.SerializeObject(stats, Formatting.None);
            connection.Execute(
                "INSERT OR REPLACE INTO player_stats_all (player_id, stat_data) VALUES (?, ?);",
                playerId.ToString(),
                json
            );
        }
    }

    public static void SavePlayerStatsForMode(Guid playerId, GameStats stats, GameMode mode)
    {
        SqliteDatabaseService.EnsureSchema();

        string tableName = mode switch
        {
            GameMode.X01 => "player_stats_x01",
            GameMode.Cricket => "player_stats_cricket",
            GameMode.ATC => "player_stats_atc",
            _ => null
        };

        if (tableName == null)
            return;

        using (var connection = SqliteDatabaseService.Open())
        {
            var json = JsonConvert.SerializeObject(stats, Formatting.None);
            connection.Execute(
                $"INSERT OR REPLACE INTO {tableName} (player_id, stat_data) VALUES (?, ?);",
                playerId.ToString(),
                json
            );
        }
    }

    public static GameStats LoadPlayerStatsAll(Guid playerId)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var row = connection.Query<PlayerStatsRow>(
                "SELECT stat_data AS StatData FROM player_stats_all WHERE player_id = ?;",
                playerId.ToString()
            ).FirstOrDefault();

            if (row == null || string.IsNullOrWhiteSpace(row.StatData))
                return null;

            return JsonConvert.DeserializeObject<GameStats>(row.StatData);
        }
    }

    public static GameStats LoadPlayerStatsForMode(Guid playerId, GameMode mode)
    {
        SqliteDatabaseService.EnsureSchema();

        string tableName = mode switch
        {
            GameMode.X01 => "player_stats_x01",
            GameMode.Cricket => "player_stats_cricket",
            GameMode.ATC => "player_stats_atc",
            _ => null
        };

        if (tableName == null)
            return null;

        using (var connection = SqliteDatabaseService.Open())
        {
            var row = connection.Query<PlayerStatsRow>(
                $"SELECT stat_data AS StatData FROM {tableName} WHERE player_id = ?;",
                playerId.ToString()
            ).FirstOrDefault();

            if (row == null || string.IsNullOrWhiteSpace(row.StatData))
                return null;

            string statType = mode switch
            {
                GameMode.X01 => "x01",
                GameMode.Cricket => "cricket",
                GameMode.ATC => "atc",
                _ => "unknown"
            };

            GameStats stats = statType switch
            {
                "x01" => JsonConvert.DeserializeObject<GameStatsX01>(row.StatData),
                "cricket" => JsonConvert.DeserializeObject<GameStatsCricket>(row.StatData),
                "atc" => JsonConvert.DeserializeObject<GameStatsATC>(row.StatData),
                _ => null
            };

            return stats;
        }
    }

    public static void RunAggregatedStatsMigrationIfNeeded()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var flag = connection.Query<MigrationFlagRow>(
                "SELECT value_text AS ValueText FROM settings WHERE key_name = ?;",
                "aggregated_stats_migrated").FirstOrDefault();

            if (flag != null && flag.ValueText == "1")
                return;

            var hasSourceRows = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM player_game_stats;") > 0;

            if (hasSourceRows)
                RebuildAggregatedPlayerStats();

            connection.Execute(
                "INSERT OR REPLACE INTO settings (key_name, value_text) VALUES (?, ?);",
                "aggregated_stats_migrated",
                "1");
        }
    }

    private class MigrationFlagRow
    {
        public string ValueText { get; set; }
    }

    public static void RebuildAggregatedPlayerStats()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.Execute("DELETE FROM player_stats_all;");
            connection.Execute("DELETE FROM player_stats_x01;");
            connection.Execute("DELETE FROM player_stats_cricket;");
            connection.Execute("DELETE FROM player_stats_atc;");
        }

        var finishedGames = new Dictionary<Guid, GameMode>();
        List<GameSummary> summaries;

        using (var connection = SqliteDatabaseService.Open())
        {
            var finishedRows = connection.Query<FinishedGameRow>(
                "SELECT id AS Id, game_mode AS GameMode FROM games WHERE finished_at IS NOT NULL;");

            foreach (var row in finishedRows)
            {
                if (Guid.TryParse(row.Id, out var gameId))
                    finishedGames[gameId] = row.GameMode;
            }

            summaries = ReadGames(connection)
                .Select(CreateGameSummaryFromRowShallow)
                .Where(s => s != null && s.IsFinished)
                .ToList();
        }

        var aggregated = new Dictionary<Guid, GameStats>();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<PlayerGameStatRow>(
                "SELECT game_id AS GameId, player_id AS PlayerId, stat_type AS StatType, stat_data AS StatData FROM player_game_stats;");

            foreach (var row in rows)
            {
                if (!Guid.TryParse(row.GameId, out var gameId)) continue;
                if (!Guid.TryParse(row.PlayerId, out var playerId)) continue;
                if (!finishedGames.TryGetValue(gameId, out var mode)) continue;

                GameStats gameStats = DeserializeStats(row.StatType, row.StatData);
                if (gameStats == null) continue;

                if (!aggregated.TryGetValue(playerId, out var allStats))
                {
                    allStats = new GameStats(playerId);
                    aggregated[playerId] = allStats;
                }

                allStats.AddGameStat(gameStats, gameId, gameStats.totalPenaltyCost);
            }
        }

        foreach (var kvp in aggregated)
        {
            SavePlayerStatsAll(kvp.Key, kvp.Value);
        }

        foreach (var summary in summaries)
        {
            SaveAggregatedPlayerStatsFromSummary(summary);
        }
    }

    private static void SaveAggregatedPlayerStatsFromSummary(GameSummary summary)
    {
        if (summary == null || !summary.IsFinished)
            return;

        foreach (var pid in summary.PlayerIds)
        {
            if (!LoadPlayerGameStatsForPlayer(summary.Id, pid, out var gameStats))
                continue;

            var modeStats = LoadPlayerStatsForMode(pid, summary.GameMode) ?? CreateStatsForMode(pid, summary.GameMode);
            modeStats.AddGameStat(gameStats, summary.Id, gameStats.totalPenaltyCost);
            SavePlayerStatsForMode(pid, modeStats, summary.GameMode);
        }
    }

    private static bool LoadPlayerGameStatsForPlayer(Guid gameId, Guid playerId, out GameStats stats)
    {
        stats = null;

        using (var connection = SqliteDatabaseService.Open())
        {
            var row = connection.Query<PlayerGameStatRow>(
                "SELECT game_id AS GameId, player_id AS PlayerId, stat_type AS StatType, stat_data AS StatData FROM player_game_stats WHERE game_id = ? AND player_id = ?;",
                gameId.ToString(), playerId.ToString()).FirstOrDefault();

            if (row == null) return false;

            stats = DeserializeStats(row.StatType, row.StatData);
            return stats != null;
        }
    }

    private static GameStats DeserializeStats(string statType, string statData)
    {
        if (string.IsNullOrWhiteSpace(statData)) return null;

        try
        {
            return statType switch
            {
                "x01" => JsonConvert.DeserializeObject<GameStatsX01>(statData),
                "cricket" => JsonConvert.DeserializeObject<GameStatsCricket>(statData),
                "atc" => JsonConvert.DeserializeObject<GameStatsATC>(statData),
                _ => null
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SQLite] Failed to deserialize stats: " + e.Message);
            return null;
        }
    }

    private static GameSummary CreateGameSummaryFromRowShallow(GameRow row)
    {
        var playerIds = row.GetPlayerIds();

        return new GameSummary
        {
            Id = Guid.Parse(row.Id),
            GameMode = row.GameMode,
            CreatedAt = DateTime.Parse(row.CreatedAt),
            LastActivityAt = DateTime.Parse(row.LastActivityAt),
            FinishedAt = string.IsNullOrEmpty(row.FinishedAt) ? (DateTime?)null : DateTime.Parse(row.FinishedAt),
            CurrentPlayerIndex = row.CurrentPlayerIndex,
            StartingPlayerIndex = row.StartingPlayerIndex,
            WinnerPlayerId = row.WinnerPlayerId,
            PlayerIds = playerIds,
            SettingsJson = row.SettingsJson
        };
    }

    private class FinishedGameRow
    {
        public string Id { get; set; }
        public GameMode GameMode { get; set; }
    }

    public static void DeletePlayerStats(Guid playerId)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.Execute("DELETE FROM player_stats_all WHERE player_id = ?;", playerId.ToString());
            connection.Execute("DELETE FROM player_stats_x01 WHERE player_id = ?;", playerId.ToString());
            connection.Execute("DELETE FROM player_stats_cricket WHERE player_id = ?;", playerId.ToString());
            connection.Execute("DELETE FROM player_stats_atc WHERE player_id = ?;", playerId.ToString());
        }
    }

    private class PlayerStatsRow
    {
        public string StatData { get; set; }
    }

    private static GameSummary CreateGameSummaryFromRow(GameRow row, SQLiteConnection connection)
    {
        var playerIds = row.GetPlayerIds();
        var settingsJson = row.SettingsJson;

        var summary = new GameSummary
        {
            Id = Guid.Parse(row.Id),
            GameMode = row.GameMode,
            CreatedAt = DateTime.Parse(row.CreatedAt),
            LastActivityAt = DateTime.Parse(row.LastActivityAt),
            FinishedAt = string.IsNullOrEmpty(row.FinishedAt) ? (DateTime?)null : DateTime.Parse(row.FinishedAt),
            CurrentPlayerIndex = row.CurrentPlayerIndex,
            StartingPlayerIndex = row.StartingPlayerIndex,
            WinnerPlayerId = row.WinnerPlayerId,
            PlayerIds = playerIds,
            SettingsJson = settingsJson
        };

        var playerStats = ReadPlayerGameStatsForGame(connection, row.Id);
        foreach (var pid in playerIds)
        {
            playerStats.TryGetValue(pid, out var gs);
            var entry = new PlayerSummaryEntry
            {
                PlayerId = pid,
                Score = GetScoreForPlayer(connection, row.Id, pid, row.GameMode),
                SetsWon = GetWonSetsForPlayer(connection, row.Id, pid),
                LegsWon = summary.IsFinished ? 0 : GetWonLegsForPlayer(connection, row.Id, pid),
                TargetsHit = GetTargetsHitForPlayer(connection, row.Id, pid),
                TotalTargets = GetTotalTargetsForPlayer(connection, row.Id, pid),
                WallCount = gs?.wallCount ?? 0,
                CeilingCount = gs?.ceilingCount ?? 0,
                AllMissCount = gs?.allMissCount ?? 0,
                ThreeOnesCount = gs?.tripleOnesCount ?? 0,
                TripleDigitCount = gs?.tripleDigitCount ?? 0,
                LostGameCount = gs?.lostGame ?? 0
            };
            summary.PlayerEntries.Add(entry);
        }

        return summary;
    }

    private static string GetGameSettingsJson(SQLiteConnection connection, string gameId)
    {
        var row = connection.Query<GameRow>(
            "SELECT settings_json AS SettingsJson FROM games WHERE id = ?;",
            gameId).FirstOrDefault();

        return row?.SettingsJson;
    }

    private static Dictionary<Guid, GameStats> ReadPlayerGameStatsForGame(SQLiteConnection connection, string gameId)
    {
        var result = new Dictionary<Guid, GameStats>();

        var rows = connection.Query<PlayerGameStatRow>(
            "SELECT game_id AS GameId, player_id AS PlayerId, stat_type AS StatType, stat_data AS StatData FROM player_game_stats WHERE game_id = ?;",
            gameId);

        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.PlayerId, out var playerId))
                continue;

            GameStats stats = row.StatType switch
            {
                "x01" => JsonConvert.DeserializeObject<GameStatsX01>(row.StatData),
                "cricket" => JsonConvert.DeserializeObject<GameStatsCricket>(row.StatData),
                "atc" => JsonConvert.DeserializeObject<GameStatsATC>(row.StatData),
                _ => null
            };

            if (stats != null)
                result[playerId] = stats;
        }

        return result;
    }

    private static int GetScoreForPlayer(SQLiteConnection connection, string gameId, Guid playerId, GameMode mode)
    {
        var stats = ReadPlayerGameStatsForGame(connection, gameId);
        if (!stats.TryGetValue(playerId, out var gameStats))
            return 0;

        return mode switch
        {
            GameMode.X01 => (gameStats as GameStatsX01)?.currentScore ?? 0,
            GameMode.Cricket => (gameStats as GameStatsCricket)?.turnScores.Values.Sum() ?? 0,
            GameMode.ATC => (gameStats as GameStatsATC)?.targetsHit ?? 0,
            _ => 0
        };
    }

    private static int GetWonSetsForPlayer(SQLiteConnection connection, string gameId, Guid playerId)
    {
        var setRows = connection.Query<SetRow>(
            "SELECT winner_player_id AS WinnerPlayerId FROM sets WHERE game_id = ?;", gameId);

        return setRows.Count(s => s.WinnerPlayerId == playerId);
    }

    private static int GetWonLegsForPlayer(SQLiteConnection connection, string gameId, Guid playerId)
    {
        // Nur Legs des letzten (laufenden) Sets zählen – wie Game.GetWonLegs.
        var setRows = connection.Query<SetRow>(
            "SELECT id AS Id, order_index AS OrderIndex FROM sets WHERE game_id = ? ORDER BY order_index DESC LIMIT 1;", gameId);
        var lastSet = setRows.FirstOrDefault();
        if (lastSet == null)
            return 0;

        var legRows = connection.Query<LegRow>(
            "SELECT set_id AS SetId, winner_player_id AS WinnerPlayerId FROM legs WHERE game_id = ? AND set_id = ?;", gameId, lastSet.Id);

        return legRows.Count(l => l.WinnerPlayerId == playerId);
    }

    private static int GetTargetsHitForPlayer(SQLiteConnection connection, string gameId, Guid playerId)
    {
        var stats = ReadPlayerGameStatsForGame(connection, gameId);
        if (!stats.TryGetValue(playerId, out var gameStats))
            return 0;

        return (gameStats as GameStatsATC)?.targetsHit ?? 0;
    }

    private static int GetTotalTargetsForPlayer(SQLiteConnection connection, string gameId, Guid playerId)
    {
        var stats = ReadPlayerGameStatsForGame(connection, gameId);
        if (!stats.TryGetValue(playerId, out var gameStats))
            return 0;

        return (gameStats as GameStatsATC)?.totalTargets ?? 0;
    }

    private static GameRow ReadGameById(SQLiteConnection connection, string gameId)
    {
        return connection.Query<GameRow>(@"
            SELECT
                id AS Id,
                game_mode AS GameMode,
                created_at AS CreatedAt,
                last_activity_at AS LastActivityAt,
                finished_at AS FinishedAt,
                current_player_index AS CurrentPlayerIndex,
                starting_player_index AS StartingPlayerIndex,
                winner_player_id AS WinnerPlayerId,
                player_ids AS PlayerIdsRaw,
                settings_json AS SettingsJson
            FROM games
            WHERE id = ?;
        ", gameId).FirstOrDefault();
    }

    private class PlayerGameStatRow
    {
        public string GameId { get; set; }
        public string PlayerId { get; set; }
        public string StatType { get; set; }
        public string StatData { get; set; }
    }

    private static void SaveSetupState(SQLiteConnection connection, Database database)
    {
        var payload = new
        {
            lastGameMode = database.GetLastGameMode().ToString(),
            x01Settings = database.GetX01Settings(),
            cricketSettings = database.GetCricketSettings(),
            atcSettings = database.GetATCSettings(),
            setsAndLegsMode = database.GetSetsAndLegsMode(),
            setCount = database.GetSetCount(),
            legCount = database.GetLegCount()
        };

        connection.Execute(
            "INSERT OR REPLACE INTO settings (key_name, value_text) VALUES (?, ?);",
            "database_setup",
            JsonConvert.SerializeObject(payload)
        );
    }

    private static void RestoreSetupState(SQLiteConnection connection, Database database)
    {
        var row = connection.Query<SettingsRow>("SELECT key_name AS KeyName, value_text AS ValueText FROM settings WHERE key_name = ?;", "database_setup").FirstOrDefault();
        if (row == null || string.IsNullOrWhiteSpace(row.ValueText))
            return;

        try
        {
            var payload = JsonConvert.DeserializeObject<DatabaseSetupSnapshot>(row.ValueText);
            if (payload == null)
                return;

            if (Enum.TryParse<GameMode>(payload.LastGameMode, true, out var mode))
                database.SetLastGameMode(mode);

            if (payload.X01Settings != null)
                database.SetX01Settings(payload.X01Settings);

            if (payload.CricketSettings != null)
                database.SetCricketSettings(payload.CricketSettings);

            if (payload.ATCSettings != null)
                database.SetATCSettings(payload.ATCSettings);

            if (Enum.TryParse<SetsAndLegs>(payload.SetsAndLegsMode, true, out var setsAndLegsMode))
                database.SetSetupState(database.GetLastGameMode(), setsAndLegsMode, payload.SetCount > 0 ? payload.SetCount : 1, payload.LegCount > 0 ? payload.LegCount : 1);
            else
                database.SetSetupState(database.GetLastGameMode(), SetsAndLegs.BestOf, payload.SetCount > 0 ? payload.SetCount : 1, payload.LegCount > 0 ? payload.LegCount : 1);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SQLite] Failed to restore setup snapshot: " + e.Message);
        }
    }

    private static void RestoreRuntimeGameMeta(SQLiteConnection connection, string gameId, Game game)
    {
        var rows = connection.Query<GameMetaRow>("SELECT key_name AS KeyName, value_text AS ValueText FROM game_meta WHERE game_id = ?;", gameId);

        foreach (var row in rows)
        {
            switch (row.KeyName)
            {
                case "player_scores":
                    if (game is X01Game x01)
                    {
                        var scoreMap = JsonConvert.DeserializeObject<Dictionary<string, int>>(row.ValueText ?? "{}") ?? new Dictionary<string, int>();
                        var scores = scoreMap.ToDictionary(kvp => Guid.Parse(kvp.Key), kvp => kvp.Value);
                        SetPrivateField(x01, "playerScores", scores);
                    }
                    break;
                case "player_checked_in":
                    if (game is X01Game x01CheckedIn)
                    {
                        var checkInMap = JsonConvert.DeserializeObject<Dictionary<string, bool>>(row.ValueText ?? "{}") ?? new Dictionary<string, bool>();
                        var checkedIn = checkInMap.ToDictionary(kvp => Guid.Parse(kvp.Key), kvp => kvp.Value);
                        SetPrivateField(x01CheckedIn, "playerCheckedIn", checkedIn);
                    }
                    break;
                case "sudden_death_triggered":
                    if (game is X01Game x01SuddenDeath)
                        SetPrivateField(x01SuddenDeath, "suddenDeathTriggered", bool.TryParse(row.ValueText, out var triggered) && triggered);
                    break;
                case "player_x01_stats":
                    if (!string.IsNullOrWhiteSpace(row.ValueText) && game is X01Game x01StatsGame)
                    {
                        var x01Stats = JsonConvert.DeserializeObject<Dictionary<string, GameStatsX01>>(row.ValueText) ?? new Dictionary<string, GameStatsX01>();
                        var playerStatsField = typeof(Game).GetField("playerStats", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        var playerStatsMap = playerStatsField?.GetValue(game) as Dictionary<Guid, GameStats>;

                        foreach (var kv in x01Stats)
                        {
                            if (Guid.TryParse(kv.Key, out var playerId) && x01StatsGame.GetPlayerIDs().Contains(playerId) && playerStatsMap != null)
                                playerStatsMap[playerId] = kv.Value;
                        }
                    }
                    break;
                case "game_settings":
                    if (!string.IsNullOrWhiteSpace(row.ValueText))
                    {
                        var json = row.ValueText;
                        switch (game.GetGameMode())
                        {
                            case GameMode.X01:
                                SetPrivateField(game, "settings", JsonConvert.DeserializeObject<X01GameSettings>(json));
                                break;
                            case GameMode.Cricket:
                                SetPrivateField(game, "settings", JsonConvert.DeserializeObject<CricketGameSettings>(json));
                                break;
                            case GameMode.ATC:
                                SetPrivateField(game, "settings", JsonConvert.DeserializeObject<ATCGameSettings>(json));
                                break;
                        }
                    }
                    break;
            }
        }
    }

    private static void ApplyTurnMeta(SQLiteConnection connection, string turnId, Turn turn)
    {
        var rows = connection.Query<GameMetaRow>("SELECT key_name AS KeyName, value_text AS ValueText FROM turn_meta WHERE turn_id = ?;", turnId);
        foreach (var row in rows)
        {
            var key = row.KeyName ?? string.Empty;
            var normalized = key
                .Replace("<", string.Empty)
                .Replace(">", string.Empty)
                .Replace("k__BackingField", string.Empty)
                .Replace("_", string.Empty)
                .ToLowerInvariant();

            switch (normalized)
            {
                case "scorebeforeturn":
                    if (int.TryParse(row.ValueText, out var scoreBeforeTurn))
                        turn.ScoreBeforeTurn = scoreBeforeTurn;
                    break;
                case "checkinthrowindex":
                    if (int.TryParse(row.ValueText, out var checkinIndex))
                        turn.CheckinThrowIndex = checkinIndex;
                    break;
                case "isbust":
                    if (bool.TryParse(row.ValueText, out var isBust) && isBust)
                        turn.MarkAsBust();
                    else
                        turn.ClearBust();
                    break;
            }
        }
    }

    private static void ClearTables(SQLiteConnection connection)
    {
        connection.Execute("DELETE FROM throws;");
        connection.Execute("DELETE FROM throw_meta;");
        connection.Execute("DELETE FROM turn_meta;");
        connection.Execute("DELETE FROM turns;");
        connection.Execute("DELETE FROM leg_meta;");
        connection.Execute("DELETE FROM legs;");
        connection.Execute("DELETE FROM set_meta;");
        connection.Execute("DELETE FROM sets;");
        connection.Execute("DELETE FROM game_players;");
        connection.Execute("DELETE FROM game_meta;");
        connection.Execute("DELETE FROM games;");
        connection.Execute("DELETE FROM player_meta;");
        connection.Execute("DELETE FROM players;");
    }

    private static void SavePlayer(SQLiteConnection connection, BasePlayer player)
    {
        connection.Execute(
            "INSERT OR REPLACE INTO players (id, name, type_name, deleted, show_in_statistics) VALUES (?, ?, ?, ?, ?);",
            player.GetID().ToString(),
            player.GetName(),
            player.GetType().Name,
            player.GotDeleted() ? 1 : 0,
            player.IsVisibleInStatistics() ? 1 : 0
        );

        SaveMeta(connection, "player_meta", "player_id", player.GetID().ToString(), player);
    }

    private static void SaveRuntimeGameMeta(SQLiteConnection connection, Game game)
    {
        if (game is X01Game x01)
        {
            var scoreField = typeof(X01Game).GetField("playerScores", BindingFlags.Instance | BindingFlags.NonPublic);
            var checkedInField = typeof(X01Game).GetField("playerCheckedIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var suddenDeathField = typeof(X01Game).GetField("suddenDeathTriggered", BindingFlags.Instance | BindingFlags.NonPublic);

            if (scoreField != null)
            {
                var scores = scoreField.GetValue(x01) as Dictionary<Guid, int>;
                if (scores != null)
                    connection.Execute("INSERT OR REPLACE INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);", game.GetID().ToString(), "player_scores", JsonConvert.SerializeObject(scores.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)));
            }

            if (checkedInField != null)
            {
                var checkedIn = checkedInField.GetValue(x01) as Dictionary<Guid, bool>;
                if (checkedIn != null)
                    connection.Execute("INSERT OR REPLACE INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);", game.GetID().ToString(), "player_checked_in", JsonConvert.SerializeObject(checkedIn.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)));
            }

            if (suddenDeathField != null)
            {
                connection.Execute("INSERT OR REPLACE INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);", game.GetID().ToString(), "sudden_death_triggered", Convert.ToString((bool)suddenDeathField.GetValue(x01)));
            }
        }

        if (game.GetPlayerStats() != null)
        {
            var x01Stats = game.GetPlayerStats()
                .Where(kvp => kvp.Value is GameStatsX01)
                .ToDictionary(kvp => kvp.Key.ToString(), kvp => (GameStatsX01)kvp.Value);

            if (x01Stats.Count > 0)
            {
                connection.Execute("INSERT OR REPLACE INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);", game.GetID().ToString(), "player_x01_stats", JsonConvert.SerializeObject(x01Stats));
            }
        }

        if (game.GetSettings() != null)
        {
            connection.Execute("INSERT OR REPLACE INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);", game.GetID().ToString(), "game_settings", JsonConvert.SerializeObject(game.GetSettings()));
        }
    }

    private static void SaveGameSettings(SQLiteConnection connection, Game game)
    {
        if (game.GetSettings() != null)
        {
            connection.Execute("INSERT OR REPLACE INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);", game.GetID().ToString(), "game_settings", JsonConvert.SerializeObject(game.GetSettings()));
        }
    }

    private static void SaveSet(SQLiteConnection connection, Guid gameId, Set set, int order)
    {
        var setId = Guid.NewGuid();

        connection.Execute(
            "INSERT INTO sets (id, game_id, order_index, winner_player_id) VALUES (?, ?, ?, ?);",
            setId.ToString(),
            gameId.ToString(),
            order,
            set.WinnerPlayerId.HasValue ? set.WinnerPlayerId.Value.ToString() : null
        );

        SaveMeta(connection, "set_meta", "set_id", setId.ToString(), set);

        var legs = set.GetLegs();
        for (int i = 0; i < legs.Count; i++)
        {
            SaveLeg(connection, gameId, setId, legs[i], i);
        }
    }

    private static void SaveLeg(SQLiteConnection connection, Guid gameId, Guid setId, Leg leg, int order)
    {
        var legId = Guid.NewGuid();

        connection.Execute(
            "INSERT INTO legs (id, game_id, set_id, order_index, winner_player_id) VALUES (?, ?, ?, ?, ?);",
            legId.ToString(),
            gameId.ToString(),
            setId.ToString(),
            order,
            leg.WinnerPlayerId == Guid.Empty ? null : leg.WinnerPlayerId.ToString()
        );

        SaveMeta(connection, "leg_meta", "leg_id", legId.ToString(), leg);

        var turns = leg.GetTurns();
        for (int i = 0; i < turns.Count; i++)
        {
            SaveTurn(connection, gameId, legId, turns[i], i);
        }
    }

    private static void SaveTurn(SQLiteConnection connection, Guid gameId, Guid legId, Turn turn, int order)
    {
        var turnId = Guid.NewGuid();

        connection.Execute(
            "INSERT INTO turns (id, game_id, leg_id, order_index, player_id, is_bust) VALUES (?, ?, ?, ?, ?, ?);",
            turnId.ToString(),
            gameId.ToString(),
            legId.ToString(),
            order,
            turn.PlayerId.ToString(),
            turn.IsBust ? 1 : 0
        );

        SaveMeta(connection, "turn_meta", "turn_id", turnId.ToString(), turn);

        var throws = turn.GetThrows();
        for (int i = 0; i < throws.Count; i++)
        {
            SaveThrow(connection, gameId, turnId, throws[i], i);
        }
    }

    private static void SaveThrow(SQLiteConnection connection, Guid gameId, Guid turnId, Throw dart, int order)
    {
        var throwId = Guid.NewGuid();
        var rawValue = dart.Value > 0 ? dart.Value : 0;
        var targetValue = dart.TargetValue > 0 ? (int?)dart.TargetValue : null;

        connection.Execute(
            "INSERT INTO throws (id, game_id, turn_id, order_index, hit_type, multiplier, score, value, target_value, is_target_hit, segment) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
            throwId.ToString(),
            gameId.ToString(),
            turnId.ToString(),
            order,
            dart.HitType.ToString(),
            (int)dart.Multiplier,
            dart.GetScore(),
            rawValue,
            targetValue,
            dart.IsTargetHit ? 1 : 0,
            targetValue
        );

        SaveMeta(connection, "throw_meta", "throw_id", throwId.ToString(), dart);
    }

    private static void SaveMeta(SQLiteConnection connection, string tableName, string ownerColumn, string ownerId, object instance)
    {
        var type = instance.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var field in fields)
        {
            if (field.IsStatic)
                continue;

            var value = field.GetValue(instance);
            if (value == null)
                continue;

            if (IsSimpleValue(value))
            {
                connection.Execute(
                    $"INSERT OR REPLACE INTO {tableName} ({ownerColumn}, key_name, value_text) VALUES (?, ?, ?);",
                    ownerId,
                    field.Name,
                    ConvertToString(value)
                );
            }
        }
    }

    private static List<BasePlayer> ReadPlayers(SQLiteConnection connection)
    {
        var players = new List<BasePlayer>();
        var rows = connection.Query<PlayerRow>("SELECT id AS Id, name AS Name, type_name AS TypeName, deleted AS Deleted, show_in_statistics AS ShowInStatistics FROM players;");

        foreach (var row in rows)
        {
            var metaRows = connection.Query<PlayerMetaRow>("SELECT key_name AS KeyName, value_text AS ValueText FROM player_meta WHERE player_id = ?;", row.Id);
            var difficulty = DartBotDifficulty.Easy;
            var botNumber = 1;

            foreach (var metaRow in metaRows)
            {
                if (metaRow.KeyName == "difficulty" && Enum.TryParse<DartBotDifficulty>(metaRow.ValueText, true, out var parsedDifficulty))
                    difficulty = parsedDifficulty;

                if (metaRow.KeyName == "number" && int.TryParse(metaRow.ValueText, out var parsedNumber))
                    botNumber = parsedNumber;
            }

            BasePlayer player = row.TypeName == "DartBot"
                ? new DartBot(Guid.Parse(row.Id), row.Name, difficulty, botNumber)
                : new Player(Guid.Parse(row.Id), row.Name);

            player.ShowInStatistics(row.ShowInStatistics == 1);
            if (row.Deleted == 1)
                player.Delete();

            foreach (var metaRow in metaRows)
            {
                if (metaRow.KeyName == "setupStartPosition" && int.TryParse(metaRow.ValueText, out var setupStartPosition))
                    player.SetSetupStartPosition(setupStartPosition);
            }

            players.Add(player);
        }

        return players;
    }

    private static List<GameRow> ReadGames(SQLiteConnection connection)
    {
        return connection.Query<GameRow>(@"
            SELECT
                id AS Id,
                game_mode AS GameMode,
                created_at AS CreatedAt,
                last_activity_at AS LastActivityAt,
                finished_at AS FinishedAt,
                current_player_index AS CurrentPlayerIndex,
                starting_player_index AS StartingPlayerIndex,
                winner_player_id AS WinnerPlayerId,
                player_ids AS PlayerIdsRaw,
                settings_json AS SettingsJson
            FROM games
            ORDER BY last_activity_at DESC;
        ");
    }

    private static List<Guid> ReadPlayerIds(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<Guid>();

        return raw
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var guid) ? guid : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    private static Game CreateGameFromRow(GameRow row)
    {
        var playerIds = row.GetPlayerIds();
        var settings = CreateDefaultSettings(row.GameMode);
        Game game = row.GameMode switch
        {
            GameMode.X01 => new X01Game(Guid.Parse(row.Id), (X01GameSettings)settings, playerIds),
            GameMode.Cricket => new CricketGame(Guid.Parse(row.Id), (CricketGameSettings)settings, playerIds),
            GameMode.ATC => new ATCGame(Guid.Parse(row.Id), (ATCGameSettings)settings, playerIds),
            _ => null
        };

        if (game == null)
            return null;

        SetPrivateField(game, "id", Guid.Parse(row.Id));
        SetPrivateField(game, "createdAt", DateTime.Parse(row.CreatedAt));
        SetPrivateField(game, "lastActivityAt", DateTime.Parse(row.LastActivityAt));
        SetPrivateField(game, "finishedAt", string.IsNullOrEmpty(row.FinishedAt) ? (DateTime?)null : DateTime.Parse(row.FinishedAt));
        SetPrivateField(game, "currentPlayerIndex", row.CurrentPlayerIndex);
        SetPrivateField(game, "startingPlayerIndex", row.StartingPlayerIndex);
        SetPrivateField(game, "playerIDs", playerIds);
        SetPrivateField(game, "matchWinnerId", row.WinnerPlayerId);

        return game;
    }

    private static void RestoreGameStructure(SQLiteConnection connection, Game game)
    {
        game.GetSets().Clear();

        var setRows = ReadSetsForGame(connection, game.GetID().ToString());
        var setById = new Dictionary<string, Set>();

        foreach (var setRow in setRows)
        {
            var set = new Set();
            if (setRow.WinnerPlayerId.HasValue)
                set.SetWinner(setRow.WinnerPlayerId.Value);

            setById[setRow.Id] = set;
            game.GetSets().Add(set);
        }

        var legRows = ReadLegsForGame(connection, game.GetID().ToString());
        var legById = new Dictionary<string, Leg>();

        foreach (var legRow in legRows)
        {
            var leg = new Leg();
            if (legRow.WinnerPlayerId != Guid.Empty)
                leg.SetWinner(legRow.WinnerPlayerId);

            legById[legRow.Id] = leg;

            if (setById.TryGetValue(legRow.SetId, out var targetSet))
                targetSet.AddLeg(leg);
            else if (game.GetSets().Count > 0)
                game.GetSets()[^1].AddLeg(leg);
        }

        var turnRows = ReadTurnsForGame(connection, game.GetID().ToString());
        var turnById = new Dictionary<string, Turn>();

        foreach (var turnRow in turnRows)
        {
            var turn = new Turn(turnRow.PlayerId);
            if (turnRow.IsBust)
                turn.MarkAsBust();

            turnById[turnRow.Id] = turn;
            ApplyTurnMeta(connection, turnRow.Id, turn);

            if (legById.TryGetValue(turnRow.LegId, out var targetLeg))
                targetLeg.AddTurn(turn);
            else if (game.GetSets().Count > 0 && game.GetSets()[^1].GetLegs().Count > 0)
                game.GetSets()[^1].GetLegs()[^1].AddTurn(turn);
        }

        foreach (var throwRow in ReadThrowsForGame(connection, game.GetID().ToString()))
        {
            if (!turnById.TryGetValue(throwRow.TurnId, out var turn))
                continue;

            var multiplier = (throwRow.MultiplierValue >= 0 && throwRow.MultiplierValue <= 2)
                ? (DartMultiplier)throwRow.MultiplierValue
                : DartMultiplier.Single;

            var hitType = Enum.TryParse<HitType>(throwRow.HitType, true, out var parsedHitType)
                ? parsedHitType
                : HitType.Board;

            var value = throwRow.Value > 0 ? throwRow.Value : (throwRow.TargetValue > 0 ? throwRow.TargetValue : throwRow.Score);
            var targetValue = throwRow.IsTargetHit || throwRow.TargetValue > 0 ? throwRow.TargetValue > 0 ? throwRow.TargetValue : throwRow.Segment : -1;
            var dart = new Throw(multiplier, value, hitType, targetValue > 0, targetValue);
            turn.AddThrow(dart);
        }

    }

    private static void SetGameDates(SQLiteConnection connection, Game game)
    {
        // 1. Abfrage an die 'games' Tabelle senden, um die Daten zur Game-ID zu laden
        var gameRows = connection.Query<GameRow>(@"
            SELECT id AS Id, created_at AS CreatedAt, last_activity_at AS LastActivityAt, finished_at AS FinishedAt
            FROM games
            WHERE id = ?;
        ", game.GetID().ToString());

        // 2. Prüfen, ob überhaupt ein Datensatz gefunden wurde
        if (gameRows != null && gameRows.Count > 0)
        {
            var row = gameRows[0];

            // 3. Werte im Game-Objekt setzen (Nutze deine entsprechenden Setter-Methoden)
            game.SetCreatedAt(DateTime.Parse(row.CreatedAt));
            game.SetLastActivityAt(DateTime.Parse(row.LastActivityAt));
            game.SetFinishedAt(string.IsNullOrEmpty(row.FinishedAt) ? (DateTime?)null : DateTime.Parse(row.FinishedAt));
            
            // 4. Deine Validierungslogik nach dem Laden triggern
            game.InitializeAfterLoad();
        }
        else
        {
            Debug.LogWarning($"Keine Datumswerte in der DB für Game-ID {game.GetID()} gefunden.");
        }
    }



    private static List<SetRow> ReadSetsForGame(SQLiteConnection connection, string gameId)
    {
        return connection.Query<SetRow>(@"
            SELECT id AS Id, game_id AS GameId, order_index AS OrderIndex, winner_player_id AS WinnerPlayerId
            FROM sets
            WHERE game_id = ?
            ORDER BY order_index, id;
        ", gameId);
    }

    private static List<LegRow> ReadLegsForGame(SQLiteConnection connection, string gameId)
    {
        return connection.Query<LegRow>(@"
            SELECT id AS Id, game_id AS GameId, set_id AS SetId, order_index AS OrderIndex, winner_player_id AS WinnerPlayerId
            FROM legs
            WHERE game_id = ?
            ORDER BY set_id, order_index, id;
        ", gameId);
    }

    private static List<TurnRow> ReadTurnsForGame(SQLiteConnection connection, string gameId)
    {
        return connection.Query<TurnRow>(@"
            SELECT id AS Id, game_id AS GameId, leg_id AS LegId, order_index AS OrderIndex, player_id AS PlayerId, is_bust AS IsBust
            FROM turns
            WHERE game_id = ?
            ORDER BY leg_id, order_index, id;
        ", gameId);
    }

    private static List<ThrowRow> ReadThrowsForGame(SQLiteConnection connection, string gameId)
    {
        return connection.Query<ThrowRow>(@"
            SELECT id AS Id, game_id AS GameId, turn_id AS TurnId, order_index AS OrderIndex, hit_type AS HitType, multiplier AS MultiplierValue, score AS Score, value AS Value, target_value AS TargetValue, is_target_hit AS IsTargetHit, segment AS Segment
            FROM throws
            WHERE game_id = ?
            ORDER BY turn_id, order_index, id;
        ", gameId);
    }

    private static GameSettings CreateDefaultSettings(GameMode gameMode)
    {
        switch (gameMode)
        {
            case GameMode.X01:
                return new X01GameSettings
                {
                    pointTarget = 501,
                    checkinType = CheckinType.StraightIn,
                    checkoutType = CheckoutType.Double,
                    setsAndLegsMode = SetsAndLegs.BestOf,
                    setCount = 1,
                    legCount = 1,
                    soundEnabled = true,
                    Penalties = new PenaltySettings()
                };
            case GameMode.Cricket:
                return new CricketGameSettings
                {
                    pointsEnabled = true,
                    cutThroatEnabled = false,
                    setsAndLegsMode = SetsAndLegs.BestOf,
                    setCount = 1,
                    legCount = 1,
                    soundEnabled = true,
                    Penalties = new PenaltySettings()
                };
            case GameMode.ATC:
                return new ATCGameSettings
                {
                    targetType = ATCTargetType.Singles,
                    order = ATCOrder.Ascending,
                    setsAndLegsMode = SetsAndLegs.BestOf,
                    setCount = 1,
                    legCount = 1,
                    soundEnabled = true,
                    Penalties = new PenaltySettings()
                };
            default:
                return new X01GameSettings
                {
                    pointTarget = 501,
                    checkinType = CheckinType.StraightIn,
                    checkoutType = CheckoutType.Double,
                    setsAndLegsMode = SetsAndLegs.BestOf,
                    setCount = 1,
                    legCount = 1,
                    soundEnabled = true,
                    Penalties = new PenaltySettings()
                };
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null)
            field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName, T fallback)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            return fallback;

        var value = field.GetValue(target);
        if (value == null)
            return fallback;

        return (T)value;
    }

    private class PlayerRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int Deleted { get; set; }
        public int ShowInStatistics { get; set; }
    }

    private class PlayerMetaRow
    {
        public string KeyName { get; set; }
        public string ValueText { get; set; }
    }

    private class GameRow
    {
        public string Id { get; set; }
        public GameMode GameMode { get; set; }
        public string CreatedAt { get; set; }
        public string LastActivityAt { get; set; }
        public string FinishedAt { get; set; }
        public int CurrentPlayerIndex { get; set; }
        public int StartingPlayerIndex { get; set; }
        public Guid? WinnerPlayerId { get; set; }
        public string PlayerIdsRaw { get; set; }
        public string SettingsJson { get; set; }

        public List<Guid> GetPlayerIds()
        {
            return ReadPlayerIds(PlayerIdsRaw);
        }
    }

    private class SetRow
    {
        public string Id { get; set; }
        public string GameId { get; set; }
        public int OrderIndex { get; set; }
        public Guid? WinnerPlayerId { get; set; }
    }

    private class LegRow
    {
        public string Id { get; set; }
        public string GameId { get; set; }
        public string SetId { get; set; }
        public int OrderIndex { get; set; }
        public Guid WinnerPlayerId { get; set; }
    }

    private class TurnRow
    {
        public string Id { get; set; }
        public string GameId { get; set; }
        public string LegId { get; set; }
        public int OrderIndex { get; set; }
        public Guid PlayerId { get; set; }
        public bool IsBust { get; set; }
    }

    private class GameMetaRow
    {
        public string KeyName { get; set; }
        public string ValueText { get; set; }
    }

    private class SettingsRow
    {
        public string KeyName { get; set; }
        public string ValueText { get; set; }
    }

    private class DatabaseSetupSnapshot
    {
        public string LastGameMode { get; set; }
        public X01GameSettings X01Settings { get; set; }
        public CricketGameSettings CricketSettings { get; set; }
        public ATCGameSettings ATCSettings { get; set; }
        public string SetsAndLegsMode { get; set; }
        public int SetCount { get; set; }
        public int LegCount { get; set; }
    }

    private class ThrowRow
    {
        public string Id { get; set; }
        public string GameId { get; set; }
        public string TurnId { get; set; }
        public int OrderIndex { get; set; }
        public string HitType { get; set; }
        public int MultiplierValue { get; set; }
        public int Score { get; set; }
        public int Value { get; set; }
        public int TargetValue { get; set; }
        public bool IsTargetHit { get; set; }
        public int Segment { get; set; }
    }

    private static bool IsSimpleValue(object value)
    {
        var type = value.GetType();

        if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type.IsEnum)
            return true;

        return false;
    }

    private static string ConvertToString(object value)
    {
        if (value is Guid guid)
            return guid.ToString();

        if (value is DateTime date)
            return date.ToString("o");

        if (value is Enum enumValue)
            return enumValue.ToString();

        return Convert.ToString(value);
    }
}
