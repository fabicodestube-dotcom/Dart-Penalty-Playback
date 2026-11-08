using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using SQLite;
using UnityEngine;

public static class SqliteDatabaseRepository
{
    public static bool SaveDatabase(Database database)
    {
        if (database == null)
            return false;

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
                return true;
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.SaveDatabase failed: " + e.Message);
                return false;
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
                    settings_json AS SettingsJson
                FROM games
                WHERE finished_at IS NOT NULL
                ORDER BY last_activity_at DESC;");

            PopulateGamePlayerIds(connection, finishedGames);

            foreach (var gameRow in finishedGames)
            {
                var game = CreateGameFromRow(gameRow);
                if (game == null)
                    continue;

                RestoreGameStructure(connection, game);
                RestoreRuntimeGameState(connection, gameRow.Id, game);
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

            // Custom-Penalty-Snapshots einmalig für alle Spiele laden
            // (eine Query statt N) und an die Summary-Erstellung reichen.
            var penaltySnapshots = ReadCustomPenaltySnapshots(connection);

            foreach (var gameRow in ReadGames(connection))
            {
                var summary = CreateGameSummaryFromRow(gameRow, connection, penaltySnapshots);
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
            RestoreRuntimeGameState(connection, gameRow.Id, game);
            game.InitializeAfterLoad();

            // playerStats werden nicht persistiert (nur Struktur + Runtime-Meta),
            // daher aus der Struktur recomputen (idempotent). Verhindert, dass ein
            // Reload+Save die player_game_stats-Zeile mit Nullen überschreibt.
            game.CalculatePlayerStatsOnSave();

            // Turn-Records (pro Buchung) werden nicht persistiert — nach Reload
            // ist die Struktur ohne Records. Custom-Summen/Counts aus der
            // gespeicherten Zeile übernehmen, sonst gingen sie bei
            // Reload+Save verloren (leere Live-Werte überschreiben nichts).
            RestorePersistedCustomPenalties(connection, gameRow.Id, game);

            // Legacy-Counter (alte Spiele) dito: kein Rebuild mehr, daher aus
            // der Zeile übernehmen, sonst gingen sie bei Reload+Save verloren.
            RestorePersistedLegacyPenalties(connection, gameRow.Id, game);

            return game;
        }
    }

    public static bool SaveGame(Game game)
    {
        if (game == null)
            return false;

        return SaveGames(new List<Game> { game });
    }

    public static bool SaveGames(IReadOnlyList<Game> games)
    {
        if (games == null || games.Count == 0)
            return false;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            try
            {
                connection.BeginTransaction();

                foreach (Game game in games)
                {
                    if (game != null)
                        SaveGame(connection, game);
                }

                connection.Commit();
                return true;
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.SaveGames failed: " + e.Message);
                return false;
            }
        }
    }

    private static void SaveGame(SQLiteConnection connection, Game game)
    {
        var currentPlayerIndex = game.GetPlayerIDs().IndexOf(game.GetCurrentPlayerId());
        if (currentPlayerIndex < 0)
            currentPlayerIndex = 0;

        var startingPlayerIndex = GetPrivateField(game, "startingPlayerIndex", 0);
        var winnerPlayerId = GetPrivateField(game, "matchWinnerId", (Guid?)null);

        var settingsJson = game.GetSettings() != null ? JsonConvert.SerializeObject(game.GetSettings()) : null;

        connection.Execute(
            @"INSERT INTO games (id, game_mode, created_at, last_activity_at, finished_at, current_player_index, starting_player_index, winner_player_id, settings_json)
              VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
              ON CONFLICT(id) DO UPDATE SET
                game_mode = excluded.game_mode,
                created_at = excluded.created_at,
                last_activity_at = excluded.last_activity_at,
                finished_at = excluded.finished_at,
                current_player_index = excluded.current_player_index,
                starting_player_index = excluded.starting_player_index,
                winner_player_id = excluded.winner_player_id,
                settings_json = excluded.settings_json;",
            game.GetID().ToString(),
            game.GetGameMode().ToString(),
            DatabaseTimestamp.ToUtcIso(game.GetCreatedAt()),
            DatabaseTimestamp.ToUtcIso(game.GetLastActivityAt()),
            DatabaseTimestamp.ToUtcIso(game.GetFinishedAt()),
            currentPlayerIndex,
            startingPlayerIndex,
            winnerPlayerId.HasValue ? winnerPlayerId.Value.ToString() : null,
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

        SaveRuntimeGameState(connection, game);

        // Alte Struktur-Zeilen löschen, sonst bleiben nach erneutem
        // Speichern verwaiste Legs/Turns/Throws übrig (neue IDs pro
        // Save) und Restore hängt sie fälschlich ans aktuelle Set.
        DeleteGameStructure(connection, game.GetID().ToString());
        for (int i = 0; i < game.GetSets().Count; i++)
        {
            SaveSet(connection, game.GetID(), game.GetSets()[i], i);
        }

        SavePlayerGameStats(connection, game);
    }

    private static void DeleteGameStructure(SQLiteConnection connection, string gameId)
    {
        connection.Execute("DELETE FROM throws WHERE game_id = ?;", gameId);
        connection.Execute("DELETE FROM turns WHERE game_id = ?;", gameId);
        connection.Execute("DELETE FROM legs WHERE game_id = ?;", gameId);
        connection.Execute("DELETE FROM sets WHERE game_id = ?;", gameId);
    }

    public static bool DeleteGame(Guid id)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            try
            {
                connection.BeginTransaction();

                DeleteGameStructure(connection, id.ToString());
                connection.Execute("DELETE FROM game_players WHERE game_id = ?;", id.ToString());
                connection.Execute("DELETE FROM player_game_stats WHERE game_id = ?;", id.ToString());
                connection.Execute("DELETE FROM games WHERE id = ?;", id.ToString());

                connection.Commit();
                return true;
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.DeleteGame failed: " + e.Message);
                return false;
            }
        }
    }

    public static bool DeleteGamesByMode(GameMode mode)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            try
            {
                connection.BeginTransaction();

                var rows = connection.Query<GameRow>("SELECT id AS Id FROM games WHERE game_mode = ?;", mode.ToString());
                foreach (var row in rows)
                {
                    DeleteGameStructure(connection, row.Id);
                    connection.Execute("DELETE FROM game_players WHERE game_id = ?;", row.Id);
                    connection.Execute("DELETE FROM player_game_stats WHERE game_id = ?;", row.Id);
                }
                connection.Execute("DELETE FROM games WHERE game_mode = ?;", mode.ToString());

                connection.Commit();
                return true;
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.DeleteGamesByMode failed: " + e.Message);
                return false;
            }
        }
    }

    public static bool DeleteAllGames()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            try
            {
                connection.BeginTransaction();

                connection.Execute("DELETE FROM throws;");
                connection.Execute("DELETE FROM turns;");
                connection.Execute("DELETE FROM legs;");
                connection.Execute("DELETE FROM sets;");
                connection.Execute("DELETE FROM game_players;");
                connection.Execute("DELETE FROM player_game_stats;");
                connection.Execute("DELETE FROM games;");

                connection.Commit();
                return true;
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.DeleteAllGames failed: " + e.Message);
                return false;
            }
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

    /// <summary>
    /// Lädt die Pro-Spiel-Stats EINes Spiels (eine Connection, eine Query).
    /// Für Delete-Pfade: VOR dem Löschen aufrufen, da die Zeilen danach weg sind.
    /// </summary>
    public static Dictionary<Guid, GameStats> LoadPlayerGameStatsForGame(Guid gameId)
    {
        var result = new Dictionary<Guid, GameStats>();

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<PlayerGameStatRow>(
                "SELECT game_id AS GameId, player_id AS PlayerId, stat_type AS StatType, stat_data AS StatData FROM player_game_stats WHERE game_id = ?;",
                gameId.ToString());

            foreach (var row in rows)
            {
                if (!Guid.TryParse(row.PlayerId, out var playerId))
                    continue;

                var stats = DeserializeStats(row.StatType, row.StatData);
                if (stats != null)
                    result[playerId] = stats;
            }
        }

        return result;
    }

    public static void SavePlayerGameStatsForGame(Game game)
    {
        if (game == null)
            return;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.BeginTransaction();

            try
            {
                SavePlayerGameStats(connection, game);
                connection.Commit();
            }
            catch
            {
                connection.Rollback();
                throw;
            }
        }
    }

    public static List<Guid> LoadGameIdsWithOutdatedPlayerGameStats(int currentStatsVersion)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<OutdatedStatsRow>(
                "SELECT DISTINCT game_id AS GameId FROM player_game_stats WHERE stats_schema_version < ?;",
                currentStatsVersion
            );

            var result = new List<Guid>(rows.Count);
            foreach (var row in rows)
            {
                if (Guid.TryParse(row.GameId, out var gameId))
                    result.Add(gameId);
            }

            return result;
        }
    }

    public static string LoadPlayerStatsVersion()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return DatabaseMigrationRunner.GetMeta(connection, DatabaseSchema.StatsVersionMetaKey);
        }
    }

    public static void MarkPlayerStatsVersion(int version)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            DatabaseMigrationRunner.SetMeta(
                connection,
                DatabaseSchema.StatsVersionMetaKey,
                version.ToString()
            );
        }
    }

    public static string GetAppMeta(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return DatabaseMigrationRunner.GetMeta(connection, key);
        }
    }

    public static void SetAppMeta(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            return;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            DatabaseMigrationRunner.SetMeta(connection, key, value);
        }
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
                "INSERT OR REPLACE INTO player_game_stats (game_id, player_id, stat_type, stat_data, stats_schema_version) VALUES (?, ?, ?, ?, ?);",
                game.GetID().ToString(),
                kvp.Key.ToString(),
                statType,
                statData,
                DatabaseSchema.CurrentStatsVersion
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

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.BeginTransaction();

            try
            {
                foreach (var kvp in stats)
                {
                    Guid playerId = kvp.Key;
                    GameStats gameStats = kvp.Value;

                    var allStats = LoadPlayerStatsAll(connection, playerId) ?? new GameStats(playerId);
                    allStats.AddGameStat(gameStats, game.GetID(), gameStats.totalPenaltyCost);
                    SavePlayerStatsAll(connection, playerId, allStats);

                    GameStats modeStats = LoadPlayerStatsForMode(connection, playerId, game.GetGameMode())
                        ?? CreateStatsForMode(playerId, game.GetGameMode());
                    modeStats.AddGameStat(gameStats, game.GetID(), gameStats.totalPenaltyCost);
                    SavePlayerStatsForMode(connection, playerId, modeStats, game.GetGameMode());
                }

                connection.Commit();
            }
            catch (Exception e)
            {
                connection.Rollback();
                Debug.LogError("SqliteDatabaseRepository.SaveAggregatedPlayerStats failed: " + e.Message);
                throw;
            }
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
            SavePlayerStatsAll(connection, playerId, stats);
        }
    }

    private static void SavePlayerStatsAll(SQLiteConnection connection, Guid playerId, GameStats stats)
    {
        if (connection == null || stats == null)
            return;

        var json = JsonConvert.SerializeObject(stats, Formatting.None);
        connection.Execute(
            "INSERT OR REPLACE INTO player_stats_all (player_id, stat_data) VALUES (?, ?);",
            playerId.ToString(),
            json
        );
    }

    public static void SavePlayerStatsForMode(Guid playerId, GameStats stats, GameMode mode)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            SavePlayerStatsForMode(connection, playerId, stats, mode);
        }
    }

    private static void SavePlayerStatsForMode(SQLiteConnection connection, Guid playerId, GameStats stats, GameMode mode)
    {
        if (connection == null || stats == null)
            return;

        string tableName = mode switch
        {
            GameMode.X01 => "player_stats_x01",
            GameMode.Cricket => "player_stats_cricket",
            GameMode.ATC => "player_stats_atc",
            _ => null
        };

        if (tableName == null)
            return;

        var json = JsonConvert.SerializeObject(stats, Formatting.None);
        connection.Execute(
            $"INSERT OR REPLACE INTO {tableName} (player_id, stat_data) VALUES (?, ?);",
            playerId.ToString(),
            json
        );
    }

    public static GameStats LoadPlayerStatsAll(Guid playerId)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
            return LoadPlayerStatsAll(connection, playerId);
    }

    private static GameStats LoadPlayerStatsAll(SQLiteConnection connection, Guid playerId)
    {
        var row = connection.Query<PlayerStatsRow>(
            "SELECT stat_data AS StatData FROM player_stats_all WHERE player_id = ?;",
            playerId.ToString()
        ).FirstOrDefault();

        if (row == null || string.IsNullOrWhiteSpace(row.StatData))
            return null;

        return JsonConvert.DeserializeObject<GameStats>(row.StatData);
    }

    public static GameStats LoadPlayerStatsForMode(Guid playerId, GameMode mode)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
            return LoadPlayerStatsForMode(connection, playerId, mode);
    }

    private static GameStats LoadPlayerStatsForMode(SQLiteConnection connection, Guid playerId, GameMode mode)
    {
        string tableName = mode switch
        {
            GameMode.X01 => "player_stats_x01",
            GameMode.Cricket => "player_stats_cricket",
            GameMode.ATC => "player_stats_atc",
            _ => null
        };

        if (tableName == null)
            return null;

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

        return statType switch
        {
            "x01" => JsonConvert.DeserializeObject<GameStatsX01>(row.StatData),
            "cricket" => JsonConvert.DeserializeObject<GameStatsCricket>(row.StatData),
            "atc" => JsonConvert.DeserializeObject<GameStatsATC>(row.StatData),
            _ => null
        };
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
            connection.BeginTransaction();

            try
            {
                connection.Execute("DELETE FROM player_stats_all;");
                connection.Execute("DELETE FROM player_stats_x01;");
                connection.Execute("DELETE FROM player_stats_cricket;");
                connection.Execute("DELETE FROM player_stats_atc;");

                var finishedGames = new Dictionary<Guid, GameMode>();
                var finishedRows = connection.Query<FinishedGameRow>(
                    "SELECT id AS Id, game_mode AS GameMode FROM games WHERE finished_at IS NOT NULL;");

                foreach (var row in finishedRows)
                {
                    if (Guid.TryParse(row.Id, out var gameId))
                        finishedGames[gameId] = row.GameMode;
                }

                var aggregatedAll = new Dictionary<Guid, GameStats>();
                var aggregatedByMode = new Dictionary<Guid, Dictionary<GameMode, GameStats>>();

                var rows = connection.Query<PlayerGameStatRow>(
                    "SELECT game_id AS GameId, player_id AS PlayerId, stat_type AS StatType, stat_data AS StatData FROM player_game_stats;");

                foreach (var row in rows)
                {
                    if (!Guid.TryParse(row.GameId, out var gameId)) continue;
                    if (!Guid.TryParse(row.PlayerId, out var playerId)) continue;
                    if (!finishedGames.TryGetValue(gameId, out var mode)) continue;

                    GameStats gameStats = DeserializeStats(row.StatType, row.StatData);
                    if (gameStats == null) continue;

                    if (!aggregatedAll.TryGetValue(playerId, out var allStats))
                    {
                        allStats = new GameStats(playerId);
                        aggregatedAll[playerId] = allStats;
                    }

                    allStats.AddGameStat(gameStats, gameId, gameStats.totalPenaltyCost);

                    if (!aggregatedByMode.TryGetValue(playerId, out var modes))
                    {
                        modes = new Dictionary<GameMode, GameStats>();
                        aggregatedByMode[playerId] = modes;
                    }

                    if (!modes.TryGetValue(mode, out var modeStats))
                    {
                        modeStats = CreateStatsForMode(playerId, mode);
                        modes[mode] = modeStats;
                    }

                    modeStats.AddGameStat(gameStats, gameId, gameStats.totalPenaltyCost);
                }

                foreach (var kvp in aggregatedAll)
                    SavePlayerStatsAll(connection, kvp.Key, kvp.Value);

                foreach (var playerKvp in aggregatedByMode)
                {
                    foreach (var modeKvp in playerKvp.Value)
                        SavePlayerStatsForMode(connection, playerKvp.Key, modeKvp.Value, modeKvp.Key);
                }

                DatabaseMigrationRunner.SetMeta(
                    connection,
                    DatabaseSchema.StatsVersionMetaKey,
                    DatabaseSchema.CurrentStatsVersion.ToString()
                );

                connection.Commit();
            }
            catch
            {
                connection.Rollback();
                throw;
            }
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

    public static string GetSetting(string key)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var row = connection.Query<SettingsRow>(
                "SELECT value_text AS ValueText FROM settings WHERE key_name = ?;",
                key).FirstOrDefault();
            return row?.ValueText;
        }
    }

    public static void SetSetting(string key, string value)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.Execute(
                "INSERT OR REPLACE INTO settings (key_name, value_text) VALUES (?, ?);",
                key, value);
        }
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

    /// <summary>
    /// Lädt alle Custom-Penalty-Snapshots (pro Spiel die beim Speichern
    /// aktiven Defs) in einer Query. Alt-Saves ohne Snapshot → kein Eintrag.
    /// </summary>
    private static Dictionary<string, List<CustomPenaltyDef>> ReadCustomPenaltySnapshots(SQLiteConnection connection)
    {
        var result = new Dictionary<string, List<CustomPenaltyDef>>();

        var fullRows = connection.Query<SnapshotMetaRow>(
            "SELECT game_id AS GameId, custom_penalties_snapshot_json AS ValueText FROM game_runtime_state WHERE custom_penalties_snapshot_json IS NOT NULL;");

        foreach (var row in fullRows)
        {
            if (string.IsNullOrEmpty(row.GameId) || string.IsNullOrWhiteSpace(row.ValueText))
                continue;

            try
            {
                var defs = JsonConvert.DeserializeObject<List<CustomPenaltyDef>>(row.ValueText);
                if (defs != null)
                    result[row.GameId] = defs;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SQLite] Custom-Penalty-Snapshot unlesbar: " + e.Message);
            }
        }

        return result;
    }

    private class SnapshotMetaRow
    {
        public string GameId { get; set; }
        public string ValueText { get; set; }
    }

    private static GameSummary CreateGameSummaryFromRow(GameRow row, SQLiteConnection connection, Dictionary<string, List<CustomPenaltyDef>> penaltySnapshots = null)
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
        var setsWonByPlayer = ReadWonSetsCounts(connection, row.Id);
        var legsWonByPlayer = ReadWonLegsCounts(connection, row.Id);

        if (penaltySnapshots != null && penaltySnapshots.TryGetValue(row.Id, out var snapshot) && snapshot != null)
            summary.CustomPenaltySnapshot = snapshot;

        foreach (var pid in playerIds)
        {
            playerStats.TryGetValue(pid, out var gs);
            setsWonByPlayer.TryGetValue(pid, out var setsWon);
            legsWonByPlayer.TryGetValue(pid, out var legsWon);
            var entry = new PlayerSummaryEntry
            {
                PlayerId = pid,
                Score = GetScoreFromStats(playerStats, pid, row.GameMode),
                SetsWon = setsWon,
                LegsWon = legsWon,
                TargetsHit = GetTargetsHitFromStats(playerStats, pid),
                TotalTargets = GetTotalTargetsFromStats(playerStats, pid),
                WallCount = gs?.wallCount ?? 0,
                CeilingCount = gs?.ceilingCount ?? 0,
                AllMissCount = gs?.allMissCount ?? 0,
                ThreeOnesCount = gs?.tripleOnesCount ?? 0,
                TripleDigitCount = gs?.tripleDigitCount ?? 0,
                LostGameCount = gs?.lostGame ?? 0,
                CustomPenaltyCounts = gs?.customPenaltyCounts != null
                    ? new Dictionary<string, int>(gs.customPenaltyCounts)
                    : new Dictionary<string, int>()
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

    /// <summary>
    /// Übernimmt persistierte Custom-Summen/Counts in frisch recomputete
    /// Live-Stats (nur wenn live leer — Live-Buchungen gewinnen immer).
    /// </summary>
    private static void RestorePersistedCustomPenalties(SQLiteConnection connection, string gameId, Game game)
    {
        var persisted = ReadPlayerGameStatsForGame(connection, gameId);
        if (persisted == null || persisted.Count == 0)
            return;

        var live = game.GetPlayerStats();
        if (live == null)
            return;

        foreach (var kvp in persisted)
        {
            if (!live.TryGetValue(kvp.Key, out var liveStats) || liveStats == null)
                continue;
            if (kvp.Value == null)
                continue;

            bool liveEmpty = liveStats.customPenaltyCounts == null || liveStats.customPenaltyCounts.Count == 0;
            if (!liveEmpty)
                continue;

            liveStats.customPenaltyCost = kvp.Value.customPenaltyCost;
            liveStats.customPenaltyCounts = kvp.Value.customPenaltyCounts != null
                ? new Dictionary<string, int>(kvp.Value.customPenaltyCounts)
                : new Dictionary<string, int>();
        }
    }

    private static void RestorePersistedLegacyPenalties(SQLiteConnection connection, string gameId, Game game)
    {
        var persisted = ReadPlayerGameStatsForGame(connection, gameId);
        if (persisted == null || persisted.Count == 0)
            return;

        var live = game.GetPlayerStats();
        if (live == null)
            return;

        foreach (var kvp in persisted)
        {
            if (!live.TryGetValue(kvp.Key, out var liveStats) || liveStats == null)
                continue;
            if (kvp.Value == null)
                continue;

            // Nur übernehmen, wenn live noch nichts gebucht wurde (neue Spiele
            // buchen Legacy nie; alte Spiele behalten ihre Zeilen-Werte).
            bool liveEmpty = liveStats.wallCount == 0 && liveStats.ceilingCount == 0
                && liveStats.allMissCount == 0 && liveStats.tripleOnesCount == 0
                && liveStats.tripleDigitCount == 0 && liveStats.lostGame == 0;
            if (!liveEmpty)
                continue;

            liveStats.wallCount = kvp.Value.wallCount;
            liveStats.ceilingCount = kvp.Value.ceilingCount;
            liveStats.allMissCount = kvp.Value.allMissCount;
            liveStats.tripleOnesCount = kvp.Value.tripleOnesCount;
            liveStats.tripleDigitCount = kvp.Value.tripleDigitCount;
            liveStats.lostGame = kvp.Value.lostGame;
        }
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

    private static int GetScoreFromStats(Dictionary<Guid, GameStats> stats, Guid playerId, GameMode mode)
    {
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

    private static Dictionary<Guid, int> ReadWonSetsCounts(SQLiteConnection connection, string gameId)
    {
        var counts = new Dictionary<Guid, int>();
        var setRows = connection.Query<SetRow>(
            "SELECT winner_player_id AS WinnerPlayerId FROM sets WHERE game_id = ?;", gameId);

        foreach (var setRow in setRows)
        {
            if (!setRow.WinnerPlayerId.HasValue)
                continue;
            var winner = setRow.WinnerPlayerId.Value;
            counts.TryGetValue(winner, out var current);
            counts[winner] = current + 1;
        }

        return counts;
    }

    private static Dictionary<Guid, int> ReadWonLegsCounts(SQLiteConnection connection, string gameId)
    {
        var counts = new Dictionary<Guid, int>();

        var legRows = connection.Query<LegRow>(
            "SELECT set_id AS SetId, winner_player_id AS WinnerPlayerId FROM legs WHERE game_id = ?;", gameId);

        foreach (var legRow in legRows)
        {
            if (legRow.WinnerPlayerId == Guid.Empty)
                continue;
            counts.TryGetValue(legRow.WinnerPlayerId, out var current);
            counts[legRow.WinnerPlayerId] = current + 1;
        }

        return counts;
    }

    private static int GetTargetsHitFromStats(Dictionary<Guid, GameStats> stats, Guid playerId)
    {
        if (!stats.TryGetValue(playerId, out var gameStats))
            return 0;

        return (gameStats as GameStatsATC)?.targetsHit ?? 0;
    }

    private static int GetTotalTargetsFromStats(Dictionary<Guid, GameStats> stats, Guid playerId)
    {
        if (!stats.TryGetValue(playerId, out var gameStats))
            return 0;

        return (gameStats as GameStatsATC)?.totalTargets ?? 0;
    }

    private static GameRow ReadGameById(SQLiteConnection connection, string gameId)
    {
        var row = connection.Query<GameRow>(@"
            SELECT
                id AS Id,
                game_mode AS GameMode,
                created_at AS CreatedAt,
                last_activity_at AS LastActivityAt,
                finished_at AS FinishedAt,
                current_player_index AS CurrentPlayerIndex,
                starting_player_index AS StartingPlayerIndex,
                winner_player_id AS WinnerPlayerId,
                settings_json AS SettingsJson
            FROM games
            WHERE id = ?;
        ", gameId).FirstOrDefault();

        if (row != null)
            PopulateGamePlayerIds(connection, new List<GameRow> { row });
        return row;
    }

    private class PlayerGameStatRow
    {
        public string GameId { get; set; }
        public string PlayerId { get; set; }
        public string StatType { get; set; }
        public string StatData { get; set; }
    }

    private class OutdatedStatsRow
    {
        public string GameId { get; set; }
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
            legCount = database.GetLegCount(),
            playerOrder = database.GetPlayerOrder()
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

            if (payload.PlayerOrder != null && payload.PlayerOrder.Count > 0)
                database.SetPlayerOrder(payload.PlayerOrder);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SQLite] Failed to restore setup snapshot: " + e.Message);
        }
    }

    private static void RestoreRuntimeGameState(SQLiteConnection connection, string gameId, Game game)
    {
        var row = connection.Query<GameRuntimeStateRow>(
            @"SELECT
                x01_player_scores_json AS X01PlayerScoresJson,
                x01_player_checked_in_json AS X01PlayerCheckedInJson,
                x01_sudden_death_triggered AS X01SuddenDeathTriggered,
                x01_player_stats_json AS X01PlayerStatsJson,
                atc_targets_json AS AtcTargetsJson,
                atc_target_multipliers_json AS AtcTargetMultipliersJson,
                atc_progress_json AS AtcProgressJson,
                custom_penalties_snapshot_json AS CustomPenaltiesSnapshotJson,
                audio_events_snapshot_json AS AudioEventsSnapshotJson,
                custom_hit_sectors_snapshot_json AS CustomHitSectorsSnapshotJson
            FROM game_runtime_state
            WHERE game_id = ?;", gameId).FirstOrDefault();

        List<int> atcTargets = null;
        List<DartMultiplier> atcMultipliers = null;
        Dictionary<Guid, int> atcProgress = null;

        if (row != null)
        {
            if (game is X01Game x01 && !string.IsNullOrWhiteSpace(row.X01PlayerScoresJson))
            {
                var scoreMap = JsonConvert.DeserializeObject<Dictionary<string, int>>(row.X01PlayerScoresJson) ?? new Dictionary<string, int>();
                var scores = scoreMap
                    .Where(kvp => Guid.TryParse(kvp.Key, out _))
                    .ToDictionary(kvp => Guid.Parse(kvp.Key), kvp => kvp.Value);
                SetPrivateField(x01, "playerScores", scores);
            }

            if (game is X01Game x01CheckedIn && !string.IsNullOrWhiteSpace(row.X01PlayerCheckedInJson))
            {
                var checkInMap = JsonConvert.DeserializeObject<Dictionary<string, bool>>(row.X01PlayerCheckedInJson) ?? new Dictionary<string, bool>();
                var checkedIn = checkInMap
                    .Where(kvp => Guid.TryParse(kvp.Key, out _))
                    .ToDictionary(kvp => Guid.Parse(kvp.Key), kvp => kvp.Value);
                SetPrivateField(x01CheckedIn, "playerCheckedIn", checkedIn);
            }

            if (game is X01Game x01SuddenDeath && row.X01SuddenDeathTriggered.HasValue)
                SetPrivateField(x01SuddenDeath, "suddenDeathTriggered", row.X01SuddenDeathTriggered.Value != 0);

            if (!string.IsNullOrWhiteSpace(row.X01PlayerStatsJson) && game is X01Game x01StatsGame)
            {
                var x01Stats = JsonConvert.DeserializeObject<Dictionary<string, GameStatsX01>>(row.X01PlayerStatsJson) ?? new Dictionary<string, GameStatsX01>();
                var playerStatsField = typeof(Game).GetField("playerStats", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var playerStatsMap = playerStatsField?.GetValue(game) as Dictionary<Guid, GameStats>;

                foreach (var kv in x01Stats)
                {
                    if (Guid.TryParse(kv.Key, out var playerId) && x01StatsGame.GetPlayerIDs().Contains(playerId) && playerStatsMap != null)
                        playerStatsMap[playerId] = kv.Value;
                }
            }

            if (!string.IsNullOrWhiteSpace(row.AtcTargetsJson))
                atcTargets = SafeParseIntList(row.AtcTargetsJson);

            if (!string.IsNullOrWhiteSpace(row.AtcTargetMultipliersJson))
            {
                var multInts = SafeParseIntList(row.AtcTargetMultipliersJson);
                if (multInts != null)
                {
                    atcMultipliers = multInts.ConvertAll(i =>
                        i >= 0 && i <= 2 ? (DartMultiplier)i : DartMultiplier.Single);
                }
            }

            if (!string.IsNullOrWhiteSpace(row.AtcProgressJson))
                atcProgress = SafeParseGuidIntMap(row.AtcProgressJson);

            if (!string.IsNullOrWhiteSpace(row.CustomPenaltiesSnapshotJson))
            {
                try
                {
                    var snap = JsonConvert.DeserializeObject<List<CustomPenaltyDef>>(row.CustomPenaltiesSnapshotJson);
                    game.RestorePenaltySnapshot(snap);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[SQLite] Penalty-Snapshot unlesbar: " + e.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(row.AudioEventsSnapshotJson))
            {
                try
                {
                    var eventSnap = JsonConvert.DeserializeObject<List<AudioEventDef>>(row.AudioEventsSnapshotJson);
                    game.RestoreAudioEventSnapshot(eventSnap);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[SQLite] AudioEvent-Snapshot unlesbar: " + e.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(row.CustomHitSectorsSnapshotJson))
            {
                try
                {
                    var sectorSnap = JsonConvert.DeserializeObject<List<CustomHitDef>>(row.CustomHitSectorsSnapshotJson);
                    game.RestoreHitSectorSnapshot(sectorSnap);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[SQLite] HitSector-Snapshot unlesbar: " + e.Message);
                }
            }
        }

        if (game is ATCGame atcGame)
            atcGame.RestoreProgressAfterLoad(atcTargets, atcMultipliers, atcProgress);
    }

    private static List<int> SafeParseIntList(string json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<List<int>>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Dictionary<Guid, int> SafeParseGuidIntMap(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var raw = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
            if (raw == null)
                return null;

            var map = new Dictionary<Guid, int>();
            foreach (var kvp in raw)
            {
                if (Guid.TryParse(kvp.Key, out var id))
                    map[id] = kvp.Value;
            }

            return map;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void ClearTables(SQLiteConnection connection)
    {
        connection.Execute("DELETE FROM throws;");
        connection.Execute("DELETE FROM turns;");
        connection.Execute("DELETE FROM legs;");
        connection.Execute("DELETE FROM sets;");
        connection.Execute("DELETE FROM game_players;");
        connection.Execute("DELETE FROM game_runtime_state;");
        connection.Execute("DELETE FROM games;");
        connection.Execute("DELETE FROM bot_state;");
        connection.Execute("DELETE FROM players;");
    }

    private static void SavePlayer(SQLiteConnection connection, BasePlayer player)
    {
        connection.Execute(
            @"INSERT INTO players (id, name, type_name, deleted, show_in_statistics, setup_start_position, created_at)
              VALUES (?, ?, ?, ?, ?, ?, ?)
              ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                type_name = excluded.type_name,
                deleted = excluded.deleted,
                show_in_statistics = excluded.show_in_statistics,
                setup_start_position = excluded.setup_start_position;",
            player.GetID().ToString(),
            player.GetName(),
            player.GetType().Name,
            player.GotDeleted() ? 1 : 0,
            player.IsVisibleInStatistics() ? 1 : 0,
            player.GetSetupStartPosition(),
            DatabaseTimestamp.ToUtcIso(DateTime.UtcNow)
        );

        if (player is DartBot bot)
        {
            connection.Execute(
                "INSERT OR REPLACE INTO bot_state (player_id, skill, difficulty, number, confidence, darts_thrown, streak) VALUES (?, ?, ?, ?, ?, ?, ?);",
                player.GetID().ToString(),
                bot.GetSkillRating(),
                BotSkillScale.GetTierKey(bot.GetSkillRating()),
                bot.GetNumber(),
                GetPrivateField(bot, "confidence", 0f),
                GetPrivateField(bot, "dartsThrown", 0),
                GetPrivateField(bot, "streak", 0)
            );
        }
        else
        {
            connection.Execute("DELETE FROM bot_state WHERE player_id = ?;", player.GetID().ToString());
        }
    }

    private static void SaveRuntimeGameState(SQLiteConnection connection, Game game)
    {
        string x01Scores = null;
        string x01CheckedIn = null;
        int? x01SuddenDeath = null;
        string x01Stats = null;
        string atcTargets = null;
        string atcMultipliers = null;
        string atcProgress = null;
        string penalties = null;
        string audio = null;
        string hitSectors = null;

        if (game is X01Game x01)
        {
            var scoreField = typeof(X01Game).GetField("playerScores", BindingFlags.Instance | BindingFlags.NonPublic);
            var checkedInField = typeof(X01Game).GetField("playerCheckedIn", BindingFlags.Instance | BindingFlags.NonPublic);
            var suddenDeathField = typeof(X01Game).GetField("suddenDeathTriggered", BindingFlags.Instance | BindingFlags.NonPublic);

            if (scoreField != null && scoreField.GetValue(x01) is Dictionary<Guid, int> scores)
                x01Scores = JsonConvert.SerializeObject(scores.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));

            if (checkedInField != null && checkedInField.GetValue(x01) is Dictionary<Guid, bool> checkedIn)
                x01CheckedIn = JsonConvert.SerializeObject(checkedIn.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));

            if (suddenDeathField != null)
                x01SuddenDeath = (bool)suddenDeathField.GetValue(x01) ? 1 : 0;
        }

        if (game.GetPlayerStats() != null)
        {
            var stats = game.GetPlayerStats()
                .Where(kvp => kvp.Value is GameStatsX01)
                .ToDictionary(kvp => kvp.Key.ToString(), kvp => (GameStatsX01)kvp.Value);

            if (stats.Count > 0)
                x01Stats = JsonConvert.SerializeObject(stats);
        }

        try
        {
            penalties = JsonConvert.SerializeObject(game.GetPenaltySnapshot(), Formatting.None);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SQLite] Custom-Penalty-Snapshot fehlgeschlagen: " + e.Message);
        }

        try
        {
            audio = JsonConvert.SerializeObject(game.GetAudioEventSnapshot(), Formatting.None);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SQLite] AudioEvent-Snapshot fehlgeschlagen: " + e.Message);
        }

        try
        {
            hitSectors = JsonConvert.SerializeObject(game.GetHitSectorSnapshot(), Formatting.None);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SQLite] HitSector-Snapshot fehlgeschlagen: " + e.Message);
        }

        if (game is ATCGame atc)
        {
            atcTargets = JsonConvert.SerializeObject(atc.GetTargetNumbers());
            atcMultipliers = JsonConvert.SerializeObject(atc.GetTargetMultipliers().ConvertAll(m => (int)m));
            atcProgress = JsonConvert.SerializeObject(atc.GetTargetIndices().ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));
        }

        connection.Execute(
            "INSERT OR REPLACE INTO game_runtime_state (game_id, x01_player_scores_json, x01_player_checked_in_json, x01_sudden_death_triggered, x01_player_stats_json, atc_targets_json, atc_target_multipliers_json, atc_progress_json, custom_penalties_snapshot_json, audio_events_snapshot_json, custom_hit_sectors_snapshot_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
            game.GetID().ToString(),
            x01Scores,
            x01CheckedIn,
            x01SuddenDeath,
            x01Stats,
            atcTargets,
            atcMultipliers,
            atcProgress,
            penalties,
            audio,
            hitSectors);
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
            "INSERT INTO turns (id, game_id, leg_id, order_index, player_id, is_bust, score_before_turn, checkin_throw_index) VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
            turnId.ToString(),
            gameId.ToString(),
            legId.ToString(),
            order,
            turn.PlayerId.ToString(),
            turn.IsBust ? 1 : 0,
            turn.ScoreBeforeTurn,
            turn.CheckinThrowIndex
        );

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
            "INSERT INTO throws (id, game_id, turn_id, order_index, hit_type, multiplier, score, value, target_value, is_target_hit, custom_hit_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
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
            dart.HitType == HitType.Custom ? dart.CustomHitId : null
        );
    }

    private static List<BasePlayer> ReadPlayers(SQLiteConnection connection)
    {
        var players = new List<BasePlayer>();
        var rows = connection.Query<PlayerRow>(@"
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.type_name AS TypeName,
                p.deleted AS Deleted,
                p.show_in_statistics AS ShowInStatistics,
                p.setup_start_position AS SetupStartPosition,
                COALESCE(b.skill,
                    CASE UPPER(COALESCE(b.difficulty, 'Easy'))
                        WHEN 'MEDIUM' THEN 4.0
                        WHEN 'HARD' THEN 7.0
                        WHEN 'PRO' THEN 10.0
                        ELSE 1.0
                    END) AS Skill,
                COALESCE(b.number, 1) AS BotNumber,
                COALESCE(b.confidence, 0) AS Confidence,
                COALESCE(b.darts_thrown, 0) AS DartsThrown,
                COALESCE(b.streak, 0) AS Streak
            FROM players p
            LEFT JOIN bot_state b ON b.player_id = p.id;");

        foreach (var row in rows)
        {
            BasePlayer player = row.TypeName == "DartBot"
                ? new DartBot(Guid.Parse(row.Id), row.Name, (float)row.Skill, row.BotNumber)
                : new Player(Guid.Parse(row.Id), row.Name);

            player.ShowInStatistics(row.ShowInStatistics == 1);
            player.SetSetupStartPosition(row.SetupStartPosition);
            if (row.Deleted == 1)
                player.Delete();

            if (player is DartBot bot)
            {
                SetPrivateField(bot, "confidence", (float)row.Confidence);
                SetPrivateField(bot, "dartsThrown", row.DartsThrown);
                SetPrivateField(bot, "streak", row.Streak);
            }

            players.Add(player);
        }

        return players;
    }

    private static List<GameRow> ReadGames(SQLiteConnection connection)
    {
        var rows = connection.Query<GameRow>(@"
            SELECT
                id AS Id,
                game_mode AS GameMode,
                created_at AS CreatedAt,
                last_activity_at AS LastActivityAt,
                finished_at AS FinishedAt,
                current_player_index AS CurrentPlayerIndex,
                starting_player_index AS StartingPlayerIndex,
                winner_player_id AS WinnerPlayerId,
                settings_json AS SettingsJson
            FROM games
            ORDER BY last_activity_at DESC;
        ");

        PopulateGamePlayerIds(connection, rows);
        return rows;
    }

    private static void PopulateGamePlayerIds(SQLiteConnection connection, List<GameRow> games)
    {
        if (games == null || games.Count == 0)
            return;

        var byId = games.ToDictionary(game => game.Id, game => game, StringComparer.Ordinal);
        var rows = connection.Query<GamePlayerRow>(
            "SELECT game_id AS GameId, player_id AS PlayerId, player_order AS PlayerOrder FROM game_players ORDER BY game_id, player_order;");

        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.GameId, out var game))
                continue;

            if (Guid.TryParse(row.PlayerId, out var playerId))
                game.PlayerIds.Add(playerId);
        }
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

        if (!string.IsNullOrWhiteSpace(row.SettingsJson))
        {
            switch (row.GameMode)
            {
                case GameMode.X01:
                    SetPrivateField(game, "settings", JsonConvert.DeserializeObject<X01GameSettings>(row.SettingsJson));
                    break;
                case GameMode.Cricket:
                    SetPrivateField(game, "settings", JsonConvert.DeserializeObject<CricketGameSettings>(row.SettingsJson));
                    break;
                case GameMode.ATC:
                    SetPrivateField(game, "settings", JsonConvert.DeserializeObject<ATCGameSettings>(row.SettingsJson));
                    break;
            }
        }

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
            turn.ScoreBeforeTurn = turnRow.ScoreBeforeTurn;
            turn.CheckinThrowIndex = turnRow.CheckinThrowIndex;
            if (turnRow.IsBust)
                turn.MarkAsBust();

            turnById[turnRow.Id] = turn;

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
            var targetValue = throwRow.IsTargetHit || throwRow.TargetValue > 0 ? throwRow.TargetValue : -1;
            var customHitId = hitType == HitType.Custom ? throwRow.CustomHitId : null;
            var dart = new Throw(multiplier, value, hitType, targetValue > 0, targetValue, customHitId);
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
            SELECT id AS Id, game_id AS GameId, leg_id AS LegId, order_index AS OrderIndex, player_id AS PlayerId, is_bust AS IsBust, score_before_turn AS ScoreBeforeTurn, checkin_throw_index AS CheckinThrowIndex
            FROM turns
            WHERE game_id = ?
            ORDER BY leg_id, order_index, id;
        ", gameId);
    }

    private static List<ThrowRow> ReadThrowsForGame(SQLiteConnection connection, string gameId)
    {
        return connection.Query<ThrowRow>(@"
            SELECT id AS Id, game_id AS GameId, turn_id AS TurnId, order_index AS OrderIndex, hit_type AS HitType, multiplier AS MultiplierValue, score AS Score, value AS Value, target_value AS TargetValue, is_target_hit AS IsTargetHit, custom_hit_id AS CustomHitId
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
        public int SetupStartPosition { get; set; }
        public double Skill { get; set; }
        public int BotNumber { get; set; }
        public double Confidence { get; set; }
        public int DartsThrown { get; set; }
        public int Streak { get; set; }
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
        public string SettingsJson { get; set; }
        public List<Guid> PlayerIds { get; } = new List<Guid>();

        public List<Guid> GetPlayerIds()
        {
            return PlayerIds;
        }
    }

    private class GamePlayerRow
    {
        public string GameId { get; set; }
        public string PlayerId { get; set; }
        public int PlayerOrder { get; set; }
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
        public int ScoreBeforeTurn { get; set; }
        public int CheckinThrowIndex { get; set; }
    }

    private class GameRuntimeStateRow
    {
        public string X01PlayerScoresJson { get; set; }
        public string X01PlayerCheckedInJson { get; set; }
        public int? X01SuddenDeathTriggered { get; set; }
        public string X01PlayerStatsJson { get; set; }
        public string AtcTargetsJson { get; set; }
        public string AtcTargetMultipliersJson { get; set; }
        public string AtcProgressJson { get; set; }
        public string CustomPenaltiesSnapshotJson { get; set; }
        public string AudioEventsSnapshotJson { get; set; }
        public string CustomHitSectorsSnapshotJson { get; set; }
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
        public List<Guid> PlayerOrder { get; set; }
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
        public string CustomHitId { get; set; }
    }

    private class CustomPenaltyRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public int Enabled { get; set; }
        public int Cost { get; set; }
        public int Builtin { get; set; }
        public int ConditionKind { get; set; }
        public string ConditionJson { get; set; }
        public string Info { get; set; }
    }

    private class AudioEventRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public int Builtin { get; set; }
        public int Enabled { get; set; }
        public int Category { get; set; }
        public int Kind { get; set; }
        public string ParamsJson { get; set; }
    }

    // =========================================================
    // CUSTOM PENALTIES + AUDIO EVENTS (Customization-Screens)
    // =========================================================

    public static List<CustomPenaltyDef> LoadCustomPenalties()
    {
        var result = new List<CustomPenaltyDef>();

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<CustomPenaltyRow>(
                "SELECT id AS Id, name AS Name, short_name AS ShortName, info AS Info, enabled AS Enabled, cost AS Cost, builtin AS Builtin, condition_kind AS ConditionKind, condition_json AS ConditionJson FROM custom_penalties;");

            foreach (var row in rows)
            {
                List<PenaltyCondition> conditions = new List<PenaltyCondition>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(row.ConditionJson))
                    {
                        var deserialized = JsonConvert.DeserializeObject<List<PenaltyCondition>>(row.ConditionJson);
                        if (deserialized != null)
                            conditions = deserialized;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Customization] Penalty-Conditions unlesbar ({row.Id}): {e.Message}");
                    conditions = new List<PenaltyCondition>();
                }

                if (conditions.Count == 0)
                    conditions.Add(new PenaltyCondition());

                result.Add(new CustomPenaltyDef
                {
                    id = row.Id,
                    name = row.Name ?? row.Id,
                    shortName = row.ShortName,
                    info = row.Info,
                    enabled = row.Enabled != 0,
                    cost = row.Cost,
                    builtIn = row.Builtin != 0,
                    conditions = conditions
                });
            }
        }

        return result;
    }

    public static void SaveCustomPenalty(CustomPenaltyDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            string conditionJson = JsonConvert.SerializeObject(def.conditions ?? new List<PenaltyCondition> { new PenaltyCondition() }, Formatting.None);
            connection.Execute(
                "INSERT OR REPLACE INTO custom_penalties (id, name, short_name, info, enabled, cost, builtin, condition_kind, condition_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);",
                def.id,
                def.name ?? def.id,
                def.shortName ?? "",
                def.info ?? "",
                def.enabled ? 1 : 0,
                def.cost,
                def.builtIn ? 1 : 0,
                (int)(def.conditions?.FirstOrDefault()?.kind ?? PenaltyConditionKind.HitTypeMiss),
                conditionJson);
        }
    }

    public static bool DeleteCustomPenalty(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            int affected = connection.Execute("DELETE FROM custom_penalties WHERE id = ?;", id);
            return affected > 0;
        }
    }

    // =========================================================
    // CUSTOM HIT SECTORS (Tab 1 „Bereiche“)
    // =========================================================

    private class CustomHitSectorRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public int Enabled { get; set; }
        public int Builtin { get; set; }
        public int Value { get; set; }
        public int Multiplier { get; set; }
    }

    public static List<CustomHitDef> LoadCustomHitSectors()
    {
        var result = new List<CustomHitDef>();

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<CustomHitSectorRow>(
                "SELECT id AS Id, name AS Name, info AS Info, enabled AS Enabled, builtin AS Builtin, value AS Value, multiplier AS Multiplier FROM custom_hit_sectors;");

            foreach (var row in rows)
            {
                result.Add(new CustomHitDef
                {
                    id = row.Id,
                    name = row.Name ?? row.Id,
                    info = row.Info,
                    enabled = row.Enabled != 0,
                    builtIn = row.Builtin != 0,
                    value = row.Value,
                    multiplier = (DartMultiplier)row.Multiplier
                });
            }
        }

        return result;
    }

    public static void SaveCustomHitSector(CustomHitDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.Execute(
                "INSERT OR REPLACE INTO custom_hit_sectors (id, name, info, enabled, builtin, value, multiplier) VALUES (?, ?, ?, ?, ?, ?, ?);",
                def.id,
                def.name ?? def.id,
                def.info ?? "",
                def.enabled ? 1 : 0,
                def.builtIn ? 1 : 0,
                def.value,
                (int)def.multiplier);
        }
    }

    public static bool DeleteCustomHitSector(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            int affected = connection.Execute("DELETE FROM custom_hit_sectors WHERE id = ?;", id);
            return affected > 0;
        }
    }

    public static List<AudioEventDef> LoadAudioEvents()
    {
        var result = new List<AudioEventDef>();

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var rows = connection.Query<AudioEventRow>(
                "SELECT id AS Id, name AS Name, info AS Info, builtin AS Builtin, enabled AS Enabled, category AS Category, kind AS Kind, params_json AS ParamsJson FROM audio_events;");

            foreach (var row in rows)
            {
                List<AudioEventTrigger> triggers = new List<AudioEventTrigger>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(row.ParamsJson))
                    {
                        var deserialized = JsonConvert.DeserializeObject<List<AudioEventTrigger>>(row.ParamsJson);
                        if (deserialized != null)
                            triggers = deserialized;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Customization] Event-Triggers unlesbar ({row.Id}): {e.Message}");
                    triggers = new List<AudioEventTrigger>();
                }

                if (triggers.Count == 0)
                    triggers.Add(new AudioEventTrigger());

                result.Add(new AudioEventDef
                {
                    id = row.Id,
                    name = row.Name ?? row.Id,
                    info = row.Info,
                    builtIn = row.Builtin != 0,
                    enabled = row.Enabled != 0,
                    category = (AudioEventCategory)row.Category,
                    triggers = triggers
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Liest den Trigger eines Audio-Events aus params_json.
    ///
    /// Legacy: Das Feld hieß früher "matchExact" und steuerte faktisch nur die
    /// Anzahl der Würfe — der UI-Toggle war aber als "Match order"
    /// beschriftet, die Absicht war also die Reihenfolge. Deshalb wird der
    /// alte Wert als "matchOrder" übernommen. Sobald das Event gespeichert
    /// wird, verschwindet "matchExact" aus dem JSON und die Zuordnung greift
    /// nicht mehr (die neue Wahl des Users bleibt dann erhalten).
    /// </summary>
    private static AudioEventTrigger DeserializeAudioEventTrigger(string paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson))
            return new AudioEventTrigger();

        var trigger = JsonConvert.DeserializeObject<AudioEventTrigger>(paramsJson)
                      ?? new AudioEventTrigger();

        if (paramsJson.Contains("\"matchExact\""))
        {
            try
            {
                var legacy = Newtonsoft.Json.Linq.JObject.Parse(paramsJson)["matchExact"];
                if (legacy != null && legacy.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)
                    trigger.matchOrder = (bool)legacy;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Customization] Legacy-Trigger (matchExact) unlesbar: " + e.Message);
            }
        }

        return trigger;
    }

    public static void SaveAudioEvent(AudioEventDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
            return;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            string paramsJson = JsonConvert.SerializeObject(def.triggers ?? new List<AudioEventTrigger> { new AudioEventTrigger() }, Formatting.None);
            connection.Execute(
                "INSERT OR REPLACE INTO audio_events (id, name, info, builtin, enabled, category, kind, params_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
                def.id,
                def.name ?? def.id,
                def.info ?? "",
                def.builtIn ? 1 : 0,
                def.enabled ? 1 : 0,
                (int)def.category,
                (int)(def.triggers?.FirstOrDefault()?.kind ?? AudioEventTriggerKind.Manual),
                paramsJson);
        }
    }

    public static bool DeleteAudioEvent(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            int affected = connection.Execute("DELETE FROM audio_events WHERE id = ?;", id);
            return affected > 0;
        }
    }

    private static bool IsSimpleValue(object value)
    {
        var type = value.GetType();

        if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type.IsEnum)
            return true;

        return false;
    }

    // =========================================================
    // RECORDED CLIPS (AudioLibrary-Index, ersetzt audio_library.json)
    // =========================================================

    public class RecordedClipRow
    {
        public long Id { get; set; }
        public string Category { get; set; }
        public int Score { get; set; }
        public string PenaltyType { get; set; }
        public string SpecialType { get; set; }
        public string CustomKey { get; set; }
        public string FileName { get; set; }
        public int SlotIndex { get; set; }
        public long? FileSize { get; set; }
        public string Sha256 { get; set; }
        public string CreatedAtUtc { get; set; }
    }

    public sealed class RecordedClipSlotRepairResult
    {
        public int DeletedDuplicates;
        public int RenumberedSlots;
    }

    public static List<RecordedClipRow> LoadAllRecordedClips()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return LoadAllRecordedClips(connection);
        }
    }

    internal static List<RecordedClipRow> LoadAllRecordedClips(SQLiteConnection connection)
    {
        return connection.Query<RecordedClipRow>(
            "SELECT id AS Id, category AS Category, score AS Score, penalty_type AS PenaltyType, special_type AS SpecialType, custom_key AS CustomKey, file_name AS FileName, slot_index AS SlotIndex, file_size AS FileSize, sha256 AS Sha256, created_at_utc AS CreatedAtUtc FROM recorded_clips ORDER BY id;");
    }

    public static int CountRecordedClips()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recorded_clips;");
        }
    }

    public static long InsertRecordedClip(string category, int score, string penaltyType, string specialType, string customKey, string fileName, int slotIndex)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            RecordedClipFileStore.TryResolvePath(category, fileName, out string fullPath);
            RecordedClipFileStore.TryReadMetadata(
                fullPath,
                out long fileSize,
                out string sha256,
                out string createdAtUtc);

            connection.Execute(
                "INSERT INTO recorded_clips (category, score, penalty_type, special_type, custom_key, file_name, slot_index, file_size, sha256, created_at_utc) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                category, score, penaltyType, specialType, customKey, fileName, slotIndex,
                fullPath != null ? fileSize : null, sha256, createdAtUtc);
            return connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
        }
    }

    public static bool RefreshRecordedClipMetadata(long id)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return RefreshRecordedClipMetadata(connection, id);
        }
    }

    internal static bool RefreshRecordedClipMetadata(SQLiteConnection connection, long id)
    {
        var row = connection.Query<RecordedClipRow>(
            "SELECT id AS Id, category AS Category, file_name AS FileName FROM recorded_clips WHERE id = ?;",
            id).FirstOrDefault();

        if (row == null ||
            !RecordedClipFileStore.TryResolvePath(row.Category, row.FileName, out string fullPath) ||
            !RecordedClipFileStore.TryReadMetadata(fullPath, out long fileSize, out string sha256, out string createdAtUtc))
            return false;

        return connection.Execute(
            "UPDATE recorded_clips SET file_size = ?, sha256 = ?, created_at_utc = ? WHERE id = ?;",
            fileSize,
            sha256,
            createdAtUtc,
            id) > 0;
    }

    public static RecordedClipSlotRepairResult RepairRecordedClipSlots()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            connection.BeginTransaction();
            try
            {
                var result = RepairRecordedClipSlots(connection);
                connection.Commit();
                return result;
            }
            catch
            {
                connection.Rollback();
                throw;
            }
        }
    }

    internal static RecordedClipSlotRepairResult RepairRecordedClipSlots(SQLiteConnection connection)
    {
        var result = new RecordedClipSlotRepairResult();
        var rows = LoadAllRecordedClips(connection);
        var groups = rows.GroupBy(BuildClipSlotGroupKey, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var usedSlots = new Dictionary<int, RecordedClipRow>();
            int nextSlot = group.Max(row => row.SlotIndex) + 1;

            foreach (var row in group.OrderBy(row => row.Id))
            {
                if (!usedSlots.TryGetValue(row.SlotIndex, out var existing))
                {
                    usedSlots[row.SlotIndex] = row;
                    continue;
                }

                if (string.Equals(existing.FileName, row.FileName, StringComparison.Ordinal))
                {
                    result.DeletedDuplicates += connection.Execute("DELETE FROM recorded_clips WHERE id = ?;", row.Id);
                    continue;
                }

                while (usedSlots.ContainsKey(nextSlot))
                    nextSlot++;

                result.RenumberedSlots += connection.Execute(
                    "UPDATE recorded_clips SET slot_index = ? WHERE id = ?;",
                    nextSlot,
                    row.Id);

                usedSlots[nextSlot] = row;
                nextSlot++;
            }
        }

        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_recorded_clips_slot ON recorded_clips(category, COALESCE(score, 0), COALESCE(penalty_type, ''), COALESCE(special_type, ''), COALESCE(custom_key, ''), slot_index);");
        return result;
    }

    private static string BuildClipSlotGroupKey(RecordedClipRow row)
    {
        return (row.Category ?? string.Empty) + "\u001f" +
               row.Score + "\u001f" +
               (row.PenaltyType ?? string.Empty) + "\u001f" +
               (row.SpecialType ?? string.Empty) + "\u001f" +
               (row.CustomKey ?? string.Empty);
    }

    public static bool DeleteRecordedClipById(long id)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return connection.Execute("DELETE FROM recorded_clips WHERE id = ?;", id) > 0;
        }
    }

    public static int DeleteRecordedClipsByIds(IEnumerable<long> ids)
    {
        if (ids == null)
            return 0;

        long[] distinctIds = ids.Distinct().ToArray();
        if (distinctIds.Length == 0)
            return 0;

        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            try
            {
                connection.BeginTransaction();

                int deleted = 0;
                foreach (long id in distinctIds)
                    deleted += connection.Execute("DELETE FROM recorded_clips WHERE id = ?;", id);

                connection.Commit();
                return deleted;
            }
            catch
            {
                connection.Rollback();
                throw;
            }
        }
    }

    public static int MaxRecordedClipSlot(string category, int score, string penaltyType, string specialType, string customKey)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return connection.ExecuteScalar<int>(
                @"SELECT COALESCE(MAX(slot_index), 0) FROM recorded_clips
                  WHERE category = ?
                    AND COALESCE(score, 0) = COALESCE(?, 0)
                    AND COALESCE(penalty_type, '') = COALESCE(?, '')
                    AND COALESCE(special_type, '') = COALESCE(?, '')
                    AND COALESCE(custom_key, '') = COALESCE(?, '');",
                category, score, penaltyType ?? "", specialType ?? "", customKey ?? "");
        }
    }

}
