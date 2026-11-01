using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SQLite;
using UnityEngine;

public sealed class Migration004NormalizeSchema : IDatabaseMigration
{
    public int Version => 4;
    public string Name => "normalize_schema";
    public string Checksum => "v4-normalize-schema";

    public void Apply(SQLiteConnection connection)
    {
        ValidateOldStructure(connection);
        CreateNewTables(connection);

        var players = CopyPlayers(connection);
        CopyBotState(connection, players);
        CopyGames(connection, players);
        CopySets(connection);
        CopyLegs(connection);
        CopyTurns(connection);
        CopyThrows(connection);
        CopyGameRuntimeState(connection);
        CopyStats(connection);
        RepairDuplicateClipSlots(connection);

        DropOldTables(connection);
        RenameNewTables(connection);
        CreateIndexes(connection);

        Debug.Log("[DatabaseMigration] Normalized players, games, sets, legs, turns, throws and stats.");
    }

    private static void ValidateOldStructure(SQLiteConnection connection)
    {
        int duplicateSets = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM (SELECT game_id, order_index FROM sets GROUP BY game_id, order_index HAVING COUNT(*) > 1);");
        int duplicateLegs = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM (SELECT set_id, order_index FROM legs GROUP BY set_id, order_index HAVING COUNT(*) > 1);");
        int duplicateTurns = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM (SELECT leg_id, order_index FROM turns GROUP BY leg_id, order_index HAVING COUNT(*) > 1);");
        int duplicateThrows = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM (SELECT turn_id, order_index FROM throws GROUP BY turn_id, order_index HAVING COUNT(*) > 1);");
        int duplicateGamePlayers = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM (SELECT game_id, player_order FROM game_players GROUP BY game_id, player_order HAVING COUNT(*) > 1);");

        if (duplicateSets + duplicateLegs + duplicateTurns + duplicateThrows + duplicateGamePlayers > 0)
            throw new InvalidOperationException("Schema migration blocked by duplicate order indexes.");

        int orphans = connection.ExecuteScalar<int>(@"
            SELECT
                (SELECT COUNT(*) FROM sets WHERE game_id NOT IN (SELECT id FROM games)) +
                (SELECT COUNT(*) FROM legs WHERE game_id NOT IN (SELECT id FROM games) OR set_id NOT IN (SELECT id FROM sets)) +
                (SELECT COUNT(*) FROM turns WHERE game_id NOT IN (SELECT id FROM games) OR leg_id NOT IN (SELECT id FROM legs)) +
                (SELECT COUNT(*) FROM throws WHERE game_id NOT IN (SELECT id FROM games) OR turn_id NOT IN (SELECT id FROM turns)) +
                (SELECT COUNT(*) FROM game_players WHERE game_id NOT IN (SELECT id FROM games) OR player_id NOT IN (SELECT id FROM players)) +
                (SELECT COUNT(*) FROM player_game_stats WHERE game_id NOT IN (SELECT id FROM games) OR player_id NOT IN (SELECT id FROM players));");

        if (orphans > 0)
            throw new InvalidOperationException("Schema migration blocked by " + orphans + " orphaned structure rows.");
    }

    private static void CreateNewTables(SQLiteConnection connection)
    {
        connection.Execute(@"
            CREATE TABLE players_new (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                type_name TEXT NOT NULL,
                deleted INTEGER NOT NULL DEFAULT 0,
                show_in_statistics INTEGER NOT NULL DEFAULT 1,
                setup_start_position INTEGER NOT NULL DEFAULT -1,
                created_at TEXT NOT NULL
            );");

        connection.Execute(@"
            CREATE TABLE bot_state (
                player_id TEXT PRIMARY KEY,
                difficulty TEXT NOT NULL DEFAULT 'Easy',
                number INTEGER NOT NULL DEFAULT 1,
                confidence REAL NOT NULL DEFAULT 0,
                darts_thrown INTEGER NOT NULL DEFAULT 0,
                streak INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(player_id) REFERENCES players(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE games_new (
                id TEXT PRIMARY KEY,
                game_mode TEXT NOT NULL,
                created_at TEXT NOT NULL,
                last_activity_at TEXT NOT NULL,
                finished_at TEXT,
                current_player_index INTEGER NOT NULL DEFAULT 0,
                starting_player_index INTEGER NOT NULL DEFAULT 0,
                winner_player_id TEXT,
                settings_json TEXT
            );");

        connection.Execute(@"
            CREATE TABLE game_players_new (
                game_id TEXT NOT NULL,
                player_id TEXT NOT NULL,
                player_order INTEGER NOT NULL,
                PRIMARY KEY (game_id, player_id),
                UNIQUE (game_id, player_order),
                FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE,
                FOREIGN KEY(player_id) REFERENCES players(id)
            );");

        connection.Execute(@"
            CREATE TABLE game_runtime_state (
                game_id TEXT PRIMARY KEY,
                x01_player_scores_json TEXT,
                x01_player_checked_in_json TEXT,
                x01_sudden_death_triggered INTEGER,
                x01_player_stats_json TEXT,
                atc_targets_json TEXT,
                atc_target_multipliers_json TEXT,
                atc_progress_json TEXT,
                custom_penalties_snapshot_json TEXT,
                audio_events_snapshot_json TEXT,
                FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE sets_new (
                id TEXT PRIMARY KEY,
                game_id TEXT NOT NULL,
                order_index INTEGER NOT NULL,
                winner_player_id TEXT,
                UNIQUE (game_id, order_index),
                FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE legs_new (
                id TEXT PRIMARY KEY,
                game_id TEXT NOT NULL,
                set_id TEXT NOT NULL,
                order_index INTEGER NOT NULL,
                winner_player_id TEXT,
                UNIQUE (set_id, order_index),
                FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE,
                FOREIGN KEY(set_id) REFERENCES sets(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE turns_new (
                id TEXT PRIMARY KEY,
                game_id TEXT NOT NULL,
                leg_id TEXT NOT NULL,
                order_index INTEGER NOT NULL,
                player_id TEXT NOT NULL,
                is_bust INTEGER NOT NULL DEFAULT 0,
                score_before_turn INTEGER NOT NULL DEFAULT 0,
                checkin_throw_index INTEGER NOT NULL DEFAULT -1,
                UNIQUE (leg_id, order_index),
                FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE,
                FOREIGN KEY(leg_id) REFERENCES legs(id) ON DELETE CASCADE,
                FOREIGN KEY(player_id) REFERENCES players(id)
            );");

        connection.Execute(@"
            CREATE TABLE throws_new (
                id TEXT PRIMARY KEY,
                game_id TEXT NOT NULL,
                turn_id TEXT NOT NULL,
                order_index INTEGER NOT NULL,
                hit_type TEXT NOT NULL,
                multiplier INTEGER NOT NULL,
                score INTEGER NOT NULL,
                value INTEGER NOT NULL DEFAULT 0,
                target_value INTEGER,
                is_target_hit INTEGER NOT NULL DEFAULT 0,
                custom_hit_id TEXT,
                UNIQUE (turn_id, order_index),
                FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE,
                FOREIGN KEY(turn_id) REFERENCES turns(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE player_game_stats_new (
                game_id TEXT NOT NULL,
                player_id TEXT NOT NULL,
                stat_type TEXT NOT NULL,
                stat_data TEXT NOT NULL,
                stats_schema_version INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (game_id, player_id),
                FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE,
                FOREIGN KEY(player_id) REFERENCES players(id)
            );");

        connection.Execute(@"
            CREATE TABLE player_stats_all_new (
                player_id TEXT PRIMARY KEY,
                stat_data TEXT NOT NULL,
                FOREIGN KEY(player_id) REFERENCES players(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE player_stats_x01_new (
                player_id TEXT PRIMARY KEY,
                stat_data TEXT NOT NULL,
                FOREIGN KEY(player_id) REFERENCES players(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE player_stats_cricket_new (
                player_id TEXT PRIMARY KEY,
                stat_data TEXT NOT NULL,
                FOREIGN KEY(player_id) REFERENCES players(id) ON DELETE CASCADE
            );");

        connection.Execute(@"
            CREATE TABLE player_stats_atc_new (
                player_id TEXT PRIMARY KEY,
                stat_data TEXT NOT NULL,
                FOREIGN KEY(player_id) REFERENCES players(id) ON DELETE CASCADE
            );");
    }

    private static HashSet<string> CopyPlayers(SQLiteConnection connection)
    {
        var meta = LoadMeta(connection, "player_meta", "player_id");
        var rows = connection.Query<PlayerRow>(
            "SELECT id AS Id, name AS Name, type_name AS TypeName, deleted AS Deleted, show_in_statistics AS ShowInStatistics, created_at AS CreatedAt FROM players;");

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            string createdAt = string.IsNullOrWhiteSpace(row.CreatedAt)
                ? DatabaseTimestamp.ToUtcIso(DateTime.UtcNow)
                : row.CreatedAt;

            int setupStartPosition = GetInt(meta, row.Id, "setupStartPosition", -1);

            connection.Execute(
                "INSERT INTO players_new (id, name, type_name, deleted, show_in_statistics, setup_start_position, created_at) VALUES (?, ?, ?, ?, ?, ?, ?);",
                row.Id,
                row.Name,
                row.TypeName,
                row.Deleted,
                row.ShowInStatistics,
                setupStartPosition,
                createdAt);

            ids.Add(row.Id);
        }

        return ids;
    }

    private static void CopyBotState(SQLiteConnection connection, HashSet<string> players)
    {
        var meta = LoadMeta(connection, "player_meta", "player_id");
        var rows = connection.Query<PlayerRow>(
            "SELECT id AS Id, type_name AS TypeName FROM players;");

        foreach (var row in rows)
        {
            if (!players.Contains(row.Id) || row.TypeName != "DartBot")
                continue;

            string difficulty = GetString(meta, row.Id, "difficulty", "Easy");
            int number = GetInt(meta, row.Id, "number", 1);
            float confidence = GetFloat(meta, row.Id, "confidence", 0f);
            int dartsThrown = GetInt(meta, row.Id, "dartsThrown", 0);
            int streak = GetInt(meta, row.Id, "streak", 0);

            connection.Execute(
                "INSERT INTO bot_state (player_id, difficulty, number, confidence, darts_thrown, streak) VALUES (?, ?, ?, ?, ?, ?);",
                row.Id,
                difficulty,
                number,
                confidence,
                dartsThrown,
                streak);
        }
    }

    private static void CopyGames(SQLiteConnection connection, HashSet<string> players)
    {
        var meta = LoadMeta(connection, "game_meta", "game_id");
        var oldPlayers = connection.Query<GamePlayerRow>(
            "SELECT game_id AS GameId, player_id AS PlayerId, player_order AS PlayerOrder FROM game_players ORDER BY game_id, player_order;");

        var oldPlayersByGame = oldPlayers
            .GroupBy(row => row.GameId)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.PlayerOrder).ToList(), StringComparer.Ordinal);

        var rows = connection.Query<GameSourceRow>(
            "SELECT id AS Id, game_mode AS GameMode, created_at AS CreatedAt, last_activity_at AS LastActivityAt, finished_at AS FinishedAt, current_player_index AS CurrentPlayerIndex, starting_player_index AS StartingPlayerIndex, winner_player_id AS WinnerPlayerId, player_ids AS PlayerIdsRaw, settings_json AS SettingsJson FROM games;");

        foreach (var row in rows)
        {
            string settingsJson = row.SettingsJson;
            if (string.IsNullOrWhiteSpace(settingsJson))
                settingsJson = GetString(meta, row.Id, "game_settings", null);

            connection.Execute(
                "INSERT INTO games_new (id, game_mode, created_at, last_activity_at, finished_at, current_player_index, starting_player_index, winner_player_id, settings_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);",
                row.Id,
                row.GameMode,
                row.CreatedAt,
                row.LastActivityAt,
                row.FinishedAt,
                row.CurrentPlayerIndex,
                row.StartingPlayerIndex,
                row.WinnerPlayerId,
                settingsJson);

            List<string> playerIds = ParsePlayerIds(row.PlayerIdsRaw);
            if (playerIds.Count == 0 && oldPlayersByGame.TryGetValue(row.Id, out var gamePlayers))
                playerIds = gamePlayers.Select(player => player.PlayerId).ToList();

            playerIds = playerIds
                .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < playerIds.Count; i++)
            {
                if (!players.Contains(playerIds[i]))
                    throw new InvalidOperationException("Game " + row.Id + " references missing player " + playerIds[i] + ".");

                connection.Execute(
                    "INSERT INTO game_players_new (game_id, player_id, player_order) VALUES (?, ?, ?);",
                    row.Id,
                    playerIds[i],
                    i);
            }
        }
    }

    private static void CopySets(SQLiteConnection connection)
    {
        var rows = connection.Query<SetRow>(
            "SELECT id AS Id, game_id AS GameId, order_index AS OrderIndex, winner_player_id AS WinnerPlayerId FROM sets;");

        foreach (var row in rows)
        {
            connection.Execute(
                "INSERT INTO sets_new (id, game_id, order_index, winner_player_id) VALUES (?, ?, ?, ?);",
                row.Id,
                row.GameId,
                row.OrderIndex,
                row.WinnerPlayerId);
        }
    }

    private static void CopyLegs(SQLiteConnection connection)
    {
        var rows = connection.Query<LegRow>(
            "SELECT id AS Id, game_id AS GameId, set_id AS SetId, order_index AS OrderIndex, winner_player_id AS WinnerPlayerId FROM legs;");

        foreach (var row in rows)
        {
            connection.Execute(
                "INSERT INTO legs_new (id, game_id, set_id, order_index, winner_player_id) VALUES (?, ?, ?, ?, ?);",
                row.Id,
                row.GameId,
                row.SetId,
                row.OrderIndex,
                row.WinnerPlayerId);
        }
    }

    private static void CopyTurns(SQLiteConnection connection)
    {
        var meta = LoadMeta(connection, "turn_meta", "turn_id");
        var rows = connection.Query<TurnRow>(
            "SELECT id AS Id, game_id AS GameId, leg_id AS LegId, order_index AS OrderIndex, player_id AS PlayerId, is_bust AS IsBust FROM turns;");

        foreach (var row in rows)
        {
            int scoreBeforeTurn = GetTurnInt(meta, row.Id, "scorebeforeturn", 0);
            int checkinThrowIndex = GetTurnInt(meta, row.Id, "checkinthrowindex", -1);

            connection.Execute(
                "INSERT INTO turns_new (id, game_id, leg_id, order_index, player_id, is_bust, score_before_turn, checkin_throw_index) VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
                row.Id,
                row.GameId,
                row.LegId,
                row.OrderIndex,
                row.PlayerId,
                row.IsBust,
                scoreBeforeTurn,
                checkinThrowIndex);
        }
    }

    private static void CopyThrows(SQLiteConnection connection)
    {
        connection.Execute(@"
            INSERT INTO throws_new (id, game_id, turn_id, order_index, hit_type, multiplier, score, value, target_value, is_target_hit, custom_hit_id)
            SELECT id, game_id, turn_id, order_index, hit_type, multiplier, score, value, target_value, is_target_hit, custom_hit_id
            FROM throws;");
    }

    private static void CopyGameRuntimeState(SQLiteConnection connection)
    {
        var meta = LoadMeta(connection, "game_meta", "game_id");

        foreach (var gameKvp in meta)
        {
            var values = gameKvp.Value;
            string scores = GetValue(values, "player_scores");
            string checkedIn = GetValue(values, "player_checked_in");
            string suddenDeath = GetValue(values, "sudden_death_triggered");
            if (suddenDeath == null)
                suddenDeath = GetValue(values, "suddenDeathTriggered");
            string x01Stats = GetValue(values, "player_x01_stats");
            string atcTargets = GetValue(values, "atc_targets");
            string atcMultipliers = GetValue(values, "atc_target_multipliers");
            string atcProgress = GetValue(values, "atc_progress");
            string penalties = GetValue(values, "custom_penalties_snapshot");
            string audio = GetValue(values, "audio_events_snapshot");

            if (scores == null && checkedIn == null && suddenDeath == null && x01Stats == null &&
                atcTargets == null && atcMultipliers == null && atcProgress == null &&
                penalties == null && audio == null)
                continue;

            int? suddenDeathValue = bool.TryParse(suddenDeath, out bool parsedSuddenDeath)
                ? (parsedSuddenDeath ? 1 : 0)
                : (int?)null;

            connection.Execute(
                "INSERT OR REPLACE INTO game_runtime_state (game_id, x01_player_scores_json, x01_player_checked_in_json, x01_sudden_death_triggered, x01_player_stats_json, atc_targets_json, atc_target_multipliers_json, atc_progress_json, custom_penalties_snapshot_json, audio_events_snapshot_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                gameKvp.Key,
                scores,
                checkedIn,
                suddenDeathValue,
                x01Stats,
                atcTargets,
                atcMultipliers,
                atcProgress,
                penalties,
                audio);
        }
    }

    private static void CopyStats(SQLiteConnection connection)
    {
        connection.Execute(@"
            INSERT INTO player_game_stats_new (game_id, player_id, stat_type, stat_data, stats_schema_version)
            SELECT game_id, player_id, stat_type, stat_data, stats_schema_version
            FROM player_game_stats;");

        connection.Execute("INSERT INTO player_stats_all_new (player_id, stat_data) SELECT player_id, stat_data FROM player_stats_all;");
        connection.Execute("INSERT INTO player_stats_x01_new (player_id, stat_data) SELECT player_id, stat_data FROM player_stats_x01;");
        connection.Execute("INSERT INTO player_stats_cricket_new (player_id, stat_data) SELECT player_id, stat_data FROM player_stats_cricket;");
        connection.Execute("INSERT INTO player_stats_atc_new (player_id, stat_data) SELECT player_id, stat_data FROM player_stats_atc;");
    }

    private static void RepairDuplicateClipSlots(SQLiteConnection connection)
    {
        var rows = connection.Query<ClipRow>(
            "SELECT id AS Id, category AS Category, score AS Score, penalty_type AS PenaltyType, special_type AS SpecialType, custom_key AS CustomKey, file_name AS FileName, slot_index AS SlotIndex FROM recorded_clips ORDER BY id;");

        var groups = rows.GroupBy(BuildClipGroupKey, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var used = new Dictionary<int, ClipRow>();
            int nextSlot = group.Max(row => row.SlotIndex) + 1;

            foreach (var row in group.OrderBy(row => row.Id))
            {
                if (!used.TryGetValue(row.SlotIndex, out var existing))
                {
                    used[row.SlotIndex] = row;
                    continue;
                }

                if (string.Equals(existing.FileName, row.FileName, StringComparison.Ordinal))
                {
                    connection.Execute("DELETE FROM recorded_clips WHERE id = ?;", row.Id);
                    continue;
                }

                while (used.ContainsKey(nextSlot))
                    nextSlot++;

                connection.Execute(
                    "UPDATE recorded_clips SET slot_index = ? WHERE id = ?;",
                    nextSlot,
                    row.Id);

                used[nextSlot] = row;
                nextSlot++;
            }
        }
    }

    private static void DropOldTables(SQLiteConnection connection)
    {
        string[] tables =
        {
            "throw_meta", "throws", "turn_meta", "turns", "leg_meta", "legs",
            "set_meta", "sets", "game_meta", "game_players", "player_game_stats",
            "player_stats_all", "player_stats_x01", "player_stats_cricket", "player_stats_atc",
            "games", "player_meta", "players"
        };

        foreach (string table in tables)
            connection.Execute("DROP TABLE IF EXISTS " + table + ";");
    }

    private static void RenameNewTables(SQLiteConnection connection)
    {
        connection.Execute("ALTER TABLE players_new RENAME TO players;");
        connection.Execute("ALTER TABLE games_new RENAME TO games;");
        connection.Execute("ALTER TABLE game_players_new RENAME TO game_players;");
        connection.Execute("ALTER TABLE sets_new RENAME TO sets;");
        connection.Execute("ALTER TABLE legs_new RENAME TO legs;");
        connection.Execute("ALTER TABLE turns_new RENAME TO turns;");
        connection.Execute("ALTER TABLE throws_new RENAME TO throws;");
        connection.Execute("ALTER TABLE player_game_stats_new RENAME TO player_game_stats;");
        connection.Execute("ALTER TABLE player_stats_all_new RENAME TO player_stats_all;");
        connection.Execute("ALTER TABLE player_stats_x01_new RENAME TO player_stats_x01;");
        connection.Execute("ALTER TABLE player_stats_cricket_new RENAME TO player_stats_cricket;");
        connection.Execute("ALTER TABLE player_stats_atc_new RENAME TO player_stats_atc;");
    }

    private static void CreateIndexes(SQLiteConnection connection)
    {
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_last_activity ON games(last_activity_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_finished_at ON games(finished_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_sets_game_id ON sets(game_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_legs_set_id ON legs(set_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_turns_leg_id ON turns(leg_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_throws_turn_id ON throws(turn_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_game_players_player ON game_players(player_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_player_game_stats_player ON player_game_stats(player_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_recorded_clips_lookup ON recorded_clips(category, custom_key, score);");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_recorded_clips_slot ON recorded_clips(category, COALESCE(score, 0), COALESCE(penalty_type, ''), COALESCE(special_type, ''), COALESCE(custom_key, ''), slot_index);");
    }

    private static Dictionary<string, Dictionary<string, string>> LoadMeta(SQLiteConnection connection, string table, string ownerColumn)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var rows = connection.Query<MetaRow>(
            "SELECT " + ownerColumn + " AS OwnerId, key_name AS KeyName, value_text AS ValueText FROM " + table + ";");

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.OwnerId))
                continue;

            if (!result.TryGetValue(row.OwnerId, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                result[row.OwnerId] = values;
            }

            values[row.KeyName ?? string.Empty] = row.ValueText;
        }

        return result;
    }

    private static string GetValue(Dictionary<string, string> values, string key)
    {
        return values != null && values.TryGetValue(key, out string value) ? value : null;
    }

    private static string GetString(Dictionary<string, Dictionary<string, string>> meta, string ownerId, string key, string fallback)
    {
        if (meta.TryGetValue(ownerId, out var values) && values.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
            return value;

        return fallback;
    }

    private static int GetInt(Dictionary<string, Dictionary<string, string>> meta, string ownerId, string key, int fallback)
    {
        return int.TryParse(GetString(meta, ownerId, key, null), out int value) ? value : fallback;
    }

    private static float GetFloat(Dictionary<string, Dictionary<string, string>> meta, string ownerId, string key, float fallback)
    {
        return float.TryParse(GetString(meta, ownerId, key, null), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    private static int GetTurnInt(Dictionary<string, Dictionary<string, string>> meta, string ownerId, string normalizedKey, int fallback)
    {
        if (!meta.TryGetValue(ownerId, out var values))
            return fallback;

        foreach (var kvp in values)
        {
            if (NormalizeKey(kvp.Key) == normalizedKey && int.TryParse(kvp.Value, out int value))
                return value;
        }

        return fallback;
    }

    private static string NormalizeKey(string key)
    {
        return (key ?? string.Empty)
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace("k__BackingField", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    private static List<string> ParsePlayerIds(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
    }

    private static string BuildClipGroupKey(ClipRow row)
    {
        return (row.Category ?? string.Empty) + "\u001f" +
               row.Score + "\u001f" +
               (row.PenaltyType ?? string.Empty) + "\u001f" +
               (row.SpecialType ?? string.Empty) + "\u001f" +
               (row.CustomKey ?? string.Empty);
    }

    private sealed class MetaRow
    {
        public string OwnerId { get; set; }
        public string KeyName { get; set; }
        public string ValueText { get; set; }
    }

    private sealed class PlayerRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int Deleted { get; set; }
        public int ShowInStatistics { get; set; }
        public string CreatedAt { get; set; }
    }

    private sealed class GameSourceRow
    {
        public string Id { get; set; }
        public string GameMode { get; set; }
        public string CreatedAt { get; set; }
        public string LastActivityAt { get; set; }
        public string FinishedAt { get; set; }
        public int CurrentPlayerIndex { get; set; }
        public int StartingPlayerIndex { get; set; }
        public string WinnerPlayerId { get; set; }
        public string PlayerIdsRaw { get; set; }
        public string SettingsJson { get; set; }
    }

    private sealed class GamePlayerRow
    {
        public string GameId { get; set; }
        public string PlayerId { get; set; }
        public int PlayerOrder { get; set; }
    }

    private sealed class SetRow
    {
        public string Id { get; set; }
        public string GameId { get; set; }
        public int OrderIndex { get; set; }
        public string WinnerPlayerId { get; set; }
    }

    private sealed class LegRow
    {
        public string Id { get; set; }
        public string GameId { get; set; }
        public string SetId { get; set; }
        public int OrderIndex { get; set; }
        public string WinnerPlayerId { get; set; }
    }

    private sealed class TurnRow
    {
        public string Id { get; set; }
        public string GameId { get; set; }
        public string LegId { get; set; }
        public int OrderIndex { get; set; }
        public string PlayerId { get; set; }
        public int IsBust { get; set; }
    }

    private sealed class ClipRow
    {
        public long Id { get; set; }
        public string Category { get; set; }
        public int Score { get; set; }
        public string PenaltyType { get; set; }
        public string SpecialType { get; set; }
        public string CustomKey { get; set; }
        public string FileName { get; set; }
        public int SlotIndex { get; set; }
    }
}
