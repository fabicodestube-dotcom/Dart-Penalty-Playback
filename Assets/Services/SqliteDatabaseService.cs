using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SQLite;
using UnityEngine;

public static class SqliteDatabaseService
{
    private const string DatabaseFileName = "darts.sqlite";

    public static string GetDatabasePath()
    {
        return Path.Combine(Application.persistentDataPath, DatabaseFileName);
    }

    public static string GetConnectionString()
    {
        return GetDatabasePath();
    }

    public static SQLiteConnection Open()
    {
        return new SQLiteConnection(GetDatabasePath());
    }

    public static void EnsureSchema()
    {
        using (var connection = new SQLiteConnection(GetDatabasePath()))
        {
            EnsureSchema(connection);
        }
    }

    public static AppSettings LoadSettings()
    {
        EnsureSchema();

        using (var connection = Open())
        {
            var rows = connection.Query<SettingsRow>("SELECT key_name AS KeyName, value_text AS ValueText FROM settings WHERE key_name = ?;", "app_settings");
            var row = rows.FirstOrDefault();
            if (row == null || string.IsNullOrWhiteSpace(row.ValueText))
            {
                var defaultSettings = new AppSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }

            try
            {
                var settings = JsonConvert.DeserializeObject<AppSettings>(row.ValueText);
                return settings ?? new AppSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SQLiteSettings] Failed to parse saved settings: " + e.Message);
                var fallback = new AppSettings();
                SaveSettings(fallback);
                return fallback;
            }
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        if (settings == null)
            return;

        EnsureSchema();

        using (var connection = Open())
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.None);
            connection.Execute("INSERT OR REPLACE INTO settings (key_name, value_text) VALUES (?, ?);", "app_settings", json);
        }
    }

    private static void EnsureSchema(SQLiteConnection connection)
    {
        connection.Execute("CREATE TABLE IF NOT EXISTS players (id TEXT PRIMARY KEY, name TEXT NOT NULL, type_name TEXT NOT NULL, deleted INTEGER NOT NULL DEFAULT 0, show_in_statistics INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_meta (player_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (player_id, key_name), FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS games (id TEXT PRIMARY KEY, game_mode TEXT NOT NULL, created_at TEXT NOT NULL, last_activity_at TEXT NOT NULL, finished_at TEXT, current_player_index INTEGER NOT NULL DEFAULT 0, starting_player_index INTEGER NOT NULL DEFAULT 0, winner_player_id TEXT, player_ids TEXT NOT NULL DEFAULT '', settings_json TEXT);");
        connection.Execute("CREATE TABLE IF NOT EXISTS game_players (game_id TEXT NOT NULL, player_id TEXT NOT NULL, player_order INTEGER NOT NULL, PRIMARY KEY (game_id, player_id), FOREIGN KEY(game_id) REFERENCES games(id), FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS game_meta (game_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (game_id, key_name), FOREIGN KEY(game_id) REFERENCES games(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS sets (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, order_index INTEGER NOT NULL, winner_player_id TEXT, FOREIGN KEY(game_id) REFERENCES games(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS set_meta (set_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (set_id, key_name), FOREIGN KEY(set_id) REFERENCES sets(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS legs (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, set_id TEXT NOT NULL, order_index INTEGER NOT NULL, winner_player_id TEXT, FOREIGN KEY(game_id) REFERENCES games(id), FOREIGN KEY(set_id) REFERENCES sets(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS leg_meta (leg_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (leg_id, key_name), FOREIGN KEY(leg_id) REFERENCES legs(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS turns (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, leg_id TEXT NOT NULL, order_index INTEGER NOT NULL, player_id TEXT NOT NULL, is_bust INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(game_id) REFERENCES games(id), FOREIGN KEY(leg_id) REFERENCES legs(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS turn_meta (turn_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (turn_id, key_name), FOREIGN KEY(turn_id) REFERENCES turns(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS throws (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, turn_id TEXT NOT NULL, order_index INTEGER NOT NULL, hit_type TEXT NOT NULL, multiplier INTEGER NOT NULL, score INTEGER NOT NULL, value INTEGER NOT NULL DEFAULT 0, target_value INTEGER, is_target_hit INTEGER NOT NULL DEFAULT 0, segment INTEGER, FOREIGN KEY(game_id) REFERENCES games(id), FOREIGN KEY(turn_id) REFERENCES turns(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS throw_meta (throw_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (throw_id, key_name), FOREIGN KEY(throw_id) REFERENCES throws(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS settings (key_name TEXT PRIMARY KEY, value_text TEXT NOT NULL);");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_game_stats (game_id TEXT NOT NULL, player_id TEXT NOT NULL, stat_type TEXT NOT NULL, stat_data TEXT NOT NULL, PRIMARY KEY (game_id, player_id), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_all (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_x01 (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_cricket (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_atc (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");

        EnsureThrowCompatibility(connection);

        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_last_activity ON games(last_activity_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_finished_at ON games(finished_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_sets_game_id ON sets(game_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_legs_set_id ON legs(set_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_turns_leg_id ON turns(leg_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_throws_turn_id ON throws(turn_id);");
    }

    private static void EnsureThrowCompatibility(SQLiteConnection connection)
    {
        var columns = connection.Query<ColumnInfo>("PRAGMA table_info(throws);");
        if (columns == null || columns.Count == 0)
            return;

        if (!columns.Any(column => string.Equals(column.Name, "value", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE throws ADD COLUMN value INTEGER NOT NULL DEFAULT 0;");

        if (!columns.Any(column => string.Equals(column.Name, "target_value", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE throws ADD COLUMN target_value INTEGER;");

        if (!columns.Any(column => string.Equals(column.Name, "is_target_hit", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE throws ADD COLUMN is_target_hit INTEGER NOT NULL DEFAULT 0;");

        if (!columns.Any(column => string.Equals(column.Name, "segment", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE throws ADD COLUMN segment INTEGER;");

        connection.Execute("UPDATE throws SET value = COALESCE(value, segment, score) WHERE value IS NULL OR value = 0;");
        connection.Execute("UPDATE throws SET target_value = CASE WHEN segment IS NOT NULL AND segment > 0 THEN segment ELSE target_value END WHERE target_value IS NULL;");
        connection.Execute("UPDATE throws SET is_target_hit = CASE WHEN COALESCE(target_value, 0) > 0 OR COALESCE(segment, 0) > 0 THEN 1 ELSE 0 END WHERE is_target_hit IS NULL OR is_target_hit = 0;");
    }

    private class SettingsRow
    {
        public string KeyName { get; set; }
        public string ValueText { get; set; }
    }

    private class ColumnInfo
    {
        public string Name { get; set; }
    }
}
