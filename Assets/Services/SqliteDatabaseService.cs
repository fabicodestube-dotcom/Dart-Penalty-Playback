using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SQLite;
using UnityEngine;

public static class SqliteDatabaseService
{
    private static bool schemaEnsured = false;
    private static readonly object schemaLock = new object();

    public static string GetDatabasePath()
    {
        return DatabasePaths.DatabasePath;
    }

    public static string GetConnectionString()
    {
        return GetDatabasePath();
    }

    public static SQLiteConnection Open()
    {
        if (!File.Exists(GetDatabasePath()))
            throw new DatabaseBootstrapException("Die Datenbankdatei fehlt. Der Datenbank-Bootstrapper wurde nicht ausgeführt.");

        var connection = new SQLiteConnection(GetDatabasePath());
        connection.Execute("PRAGMA foreign_keys = ON;");
        connection.Execute("PRAGMA busy_timeout = 5000;");
        return connection;
    }

    public static SQLiteConnection OpenReadOnly()
    {
        if (!File.Exists(GetDatabasePath()))
            throw new DatabaseBootstrapException("Die Datenbankdatei fehlt.");

        return new SQLiteConnection(GetDatabasePath(), SQLiteOpenFlags.ReadOnly);
    }

    public static SQLiteConnection OpenForBootstrap()
    {
        return new SQLiteConnection(GetDatabasePath(), SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
    }

    public static void EnsureSchema()
    {
        // Einmal pro Prozess: Schema steht nach dem Boot-Gate. Alle
        // Schema-Änderungen laufen ausschließlich über den MigrationRunner.
        lock (schemaLock)
        {
            if (schemaEnsured)
                return;

            DatabaseBootstrapper.EnsureReady();
            schemaEnsured = true;
        }
    }

    internal static void MarkSchemaEnsured()
    {
        lock (schemaLock)
        {
            schemaEnsured = true;
        }
    }

    internal static void ResetSchemaEnsured()
    {
        lock (schemaLock)
        {
            schemaEnsured = false;
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
                return new AppSettings();
            }

            try
            {
                var settings = JsonConvert.DeserializeObject<AppSettings>(row.ValueText);
                return settings ?? new AppSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SQLiteSettings] Failed to parse saved settings: " + e.Message);
                return new AppSettings();
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

    internal static void CreateSchemaBaseline(SQLiteConnection connection)
    {
        connection.Execute("CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, name TEXT NOT NULL, applied_at_utc TEXT NOT NULL, app_version TEXT NOT NULL, checksum TEXT NOT NULL);");
        connection.Execute("CREATE TABLE IF NOT EXISTS app_meta (key_name TEXT PRIMARY KEY, value_text TEXT NOT NULL);");
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
        connection.Execute("CREATE TABLE IF NOT EXISTS throws (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, turn_id TEXT NOT NULL, order_index INTEGER NOT NULL, hit_type TEXT NOT NULL, multiplier INTEGER NOT NULL, score INTEGER NOT NULL, value INTEGER NOT NULL DEFAULT 0, target_value INTEGER, is_target_hit INTEGER NOT NULL DEFAULT 0, segment INTEGER, custom_hit_id TEXT, FOREIGN KEY(game_id) REFERENCES games(id), FOREIGN KEY(turn_id) REFERENCES turns(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS throw_meta (throw_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (throw_id, key_name), FOREIGN KEY(throw_id) REFERENCES throws(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS settings (key_name TEXT PRIMARY KEY, value_text TEXT NOT NULL);");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_game_stats (game_id TEXT NOT NULL, player_id TEXT NOT NULL, stat_type TEXT NOT NULL, stat_data TEXT NOT NULL, stats_schema_version INTEGER NOT NULL DEFAULT 1, PRIMARY KEY (game_id, player_id), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_all (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_x01 (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_cricket (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_atc (player_id TEXT PRIMARY KEY, stat_data TEXT NOT NULL, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS custom_penalties (id TEXT PRIMARY KEY, name TEXT NOT NULL, short_name TEXT NOT NULL DEFAULT '', info TEXT NOT NULL DEFAULT '', enabled INTEGER NOT NULL DEFAULT 1, cost INTEGER NOT NULL DEFAULT 0, builtin INTEGER NOT NULL DEFAULT 0, condition_kind INTEGER NOT NULL DEFAULT 0, condition_json TEXT NOT NULL DEFAULT '{}');");
        connection.Execute("CREATE TABLE IF NOT EXISTS custom_hit_sectors (id TEXT PRIMARY KEY, name TEXT NOT NULL, info TEXT NOT NULL DEFAULT '', enabled INTEGER NOT NULL DEFAULT 1, builtin INTEGER NOT NULL DEFAULT 0, value INTEGER NOT NULL DEFAULT 0, multiplier INTEGER NOT NULL DEFAULT 0);");
        connection.Execute("CREATE TABLE IF NOT EXISTS audio_events (id TEXT PRIMARY KEY, name TEXT NOT NULL, info TEXT NOT NULL DEFAULT '', builtin INTEGER NOT NULL DEFAULT 0, enabled INTEGER NOT NULL DEFAULT 1, category INTEGER NOT NULL DEFAULT 0, kind INTEGER NOT NULL DEFAULT 0, params_json TEXT NOT NULL DEFAULT '{}');");
        connection.Execute("CREATE TABLE IF NOT EXISTS recorded_clips (id INTEGER PRIMARY KEY AUTOINCREMENT, category TEXT NOT NULL, score INTEGER NOT NULL DEFAULT 0, penalty_type TEXT, special_type TEXT, custom_key TEXT, file_name TEXT NOT NULL, slot_index INTEGER NOT NULL DEFAULT 0);");
        EnsureThrowCompatibility(connection);
        EnsurePenaltyShortName(connection);
        EnsureAudioEventEnabled(connection);
        EnsureCustomizationInfo(connection);
        EnsureCustomHitSectorValueMultiplier(connection);
        EnsureStatsSchemaVersion(connection);

        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_last_activity ON games(last_activity_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_finished_at ON games(finished_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_sets_game_id ON sets(game_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_legs_set_id ON legs(set_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_turns_leg_id ON turns(leg_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_throws_turn_id ON throws(turn_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_recorded_clips_lookup ON recorded_clips(category, custom_key, score);");
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

        if (!columns.Any(column => string.Equals(column.Name, "custom_hit_id", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE throws ADD COLUMN custom_hit_id TEXT;");

        connection.Execute("UPDATE throws SET value = COALESCE(value, segment, score) WHERE value IS NULL OR value = 0;");
        connection.Execute("UPDATE throws SET target_value = CASE WHEN segment IS NOT NULL AND segment > 0 THEN segment ELSE target_value END WHERE target_value IS NULL;");
        connection.Execute("UPDATE throws SET is_target_hit = CASE WHEN COALESCE(target_value, 0) > 0 OR COALESCE(segment, 0) > 0 THEN 1 ELSE 0 END WHERE is_target_hit IS NULL OR is_target_hit = 0;");
    }

    /// <summary>short_name-Spalte für Bestands-DBs nachrüsten.</summary>
    private static void EnsurePenaltyShortName(SQLiteConnection connection)
    {
        var columns = connection.Query<ColumnInfo>("PRAGMA table_info(custom_penalties);");
        if (columns == null || columns.Count == 0)
            return;

        if (!columns.Any(column => string.Equals(column.Name, "short_name", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE custom_penalties ADD COLUMN short_name TEXT NOT NULL DEFAULT '';");
    }

    /// <summary>enabled-Spalte für Bestands-DBs nachrüsten (Default an).</summary>
    private static void EnsureAudioEventEnabled(SQLiteConnection connection)
    {
        var columns = connection.Query<ColumnInfo>("PRAGMA table_info(audio_events);");
        if (columns == null || columns.Count == 0)
            return;

        if (!columns.Any(column => string.Equals(column.Name, "enabled", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE audio_events ADD COLUMN enabled INTEGER NOT NULL DEFAULT 1;");
    }

    /// <summary>info-Spalten für Bestands-DBs nachrüsten (Customization-Rows).</summary>
    private static void EnsureCustomizationInfo(SQLiteConnection connection)
    {
        EnsureInfoColumn(connection, "custom_penalties");
        EnsureInfoColumn(connection, "custom_hit_sectors");
        EnsureInfoColumn(connection, "audio_events");
    }

    private static void EnsureInfoColumn(SQLiteConnection connection, string table)
    {
        var columns = connection.Query<ColumnInfo>($"PRAGMA table_info({table});");
        if (columns == null || columns.Count == 0)
            return;

        if (!columns.Any(column => string.Equals(column.Name, "info", StringComparison.OrdinalIgnoreCase)))
            connection.Execute($"ALTER TABLE {table} ADD COLUMN info TEXT NOT NULL DEFAULT '';");
    }

    /// <summary>value/multiplier-Spalten für Bestands-DBs nachrüsten.</summary>
    private static void EnsureCustomHitSectorValueMultiplier(SQLiteConnection connection)
    {
        var columns = connection.Query<ColumnInfo>("PRAGMA table_info(custom_hit_sectors);");
        if (columns == null || columns.Count == 0)
            return;

        if (!columns.Any(column => string.Equals(column.Name, "value", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE custom_hit_sectors ADD COLUMN value INTEGER NOT NULL DEFAULT 0;");

        if (!columns.Any(column => string.Equals(column.Name, "multiplier", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE custom_hit_sectors ADD COLUMN multiplier INTEGER NOT NULL DEFAULT 0;");
    }

    /// <summary>stats_schema_version-Spalte für Bestands-DBs nachrüsten.</summary>
    private static void EnsureStatsSchemaVersion(SQLiteConnection connection)
    {
        var columns = connection.Query<ColumnInfo>("PRAGMA table_info(player_game_stats);");
        if (columns == null || columns.Count == 0)
            return;

        if (!columns.Any(column => string.Equals(column.Name, "stats_schema_version", StringComparison.OrdinalIgnoreCase)))
            connection.Execute("ALTER TABLE player_game_stats ADD COLUMN stats_schema_version INTEGER NOT NULL DEFAULT 1;");
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
