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
        // Once per process: scheme is after the boot gate.
        // Scheme changes are made exclusively via MigrationRunner.
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

                // Quarantine defective raw value instead of it on the next save
                // To be silently overwritten.
                try
                {
                    string quarantineKey = "app_settings_corrupt_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    connection.Execute(
                        "INSERT OR REPLACE INTO settings (key_name, value_text) VALUES (?, ?);",
                        quarantineKey,
                        row.ValueText);
                    connection.Execute("DELETE FROM settings WHERE key_name = ?;", "app_settings");
                }
                catch (Exception quarantineError)
                {
                    Debug.LogWarning("[SQLiteSettings] Failed to quarantine corrupt settings: " + quarantineError.Message);
                }

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

        connection.Execute("CREATE TABLE IF NOT EXISTS players (id TEXT PRIMARY KEY, name TEXT NOT NULL, type_name TEXT NOT NULL, deleted INTEGER NOT NULL DEFAULT 0, show_in_statistics INTEGER NOT NULL DEFAULT 1, setup_start_position INTEGER NOT NULL DEFAULT -1, created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')));");
        connection.Execute("CREATE TABLE IF NOT EXISTS bot_state (player_id TEXT PRIMARY KEY, difficulty TEXT NOT NULL DEFAULT 'Easy', number INTEGER NOT NULL DEFAULT 1, confidence REAL NOT NULL DEFAULT 0, darts_thrown INTEGER NOT NULL DEFAULT 0, streak INTEGER NOT NULL DEFAULT 0, skill REAL, FOREIGN KEY(player_id) REFERENCES players(id) ON DELETE CASCADE);");

        connection.Execute("CREATE TABLE IF NOT EXISTS games (id TEXT PRIMARY KEY, game_mode TEXT NOT NULL, created_at TEXT NOT NULL, last_activity_at TEXT NOT NULL, finished_at TEXT, current_player_index INTEGER NOT NULL DEFAULT 0, starting_player_index INTEGER NOT NULL DEFAULT 0, winner_player_id TEXT, settings_json TEXT);");
        connection.Execute("CREATE TABLE IF NOT EXISTS game_players (game_id TEXT NOT NULL, player_id TEXT NOT NULL, player_order INTEGER NOT NULL, PRIMARY KEY (game_id, player_id), UNIQUE (game_id, player_order), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS game_runtime_state (game_id TEXT PRIMARY KEY, x01_player_scores_json TEXT, x01_player_checked_in_json TEXT, x01_sudden_death_triggered INTEGER, x01_player_stats_json TEXT, atc_targets_json TEXT, atc_target_multipliers_json TEXT, atc_progress_json TEXT, custom_penalties_snapshot_json TEXT, audio_events_snapshot_json TEXT, custom_hit_sectors_snapshot_json TEXT, FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE);");

        connection.Execute("CREATE TABLE IF NOT EXISTS sets (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, order_index INTEGER NOT NULL, winner_player_id TEXT, UNIQUE (game_id, order_index), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE);");
        connection.Execute("CREATE TABLE IF NOT EXISTS legs (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, set_id TEXT NOT NULL, order_index INTEGER NOT NULL, winner_player_id TEXT, UNIQUE (set_id, order_index), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE, FOREIGN KEY(set_id) REFERENCES sets(id) ON DELETE CASCADE);");
        connection.Execute("CREATE TABLE IF NOT EXISTS turns (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, leg_id TEXT NOT NULL, order_index INTEGER NOT NULL, player_id TEXT NOT NULL, is_bust INTEGER NOT NULL DEFAULT 0, score_before_turn INTEGER NOT NULL DEFAULT 0, checkin_throw_index INTEGER NOT NULL DEFAULT -1, UNIQUE (leg_id, order_index), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE, FOREIGN KEY(leg_id) REFERENCES legs(id) ON DELETE CASCADE, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS throws (id TEXT PRIMARY KEY, game_id TEXT NOT NULL, turn_id TEXT NOT NULL, order_index INTEGER NOT NULL, hit_type TEXT NOT NULL, multiplier INTEGER NOT NULL, score INTEGER NOT NULL, value INTEGER NOT NULL DEFAULT 0, target_value INTEGER, is_target_hit INTEGER NOT NULL DEFAULT 0, segment INTEGER, custom_hit_id TEXT, UNIQUE (turn_id, order_index), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE, FOREIGN KEY(turn_id) REFERENCES turns(id) ON DELETE CASCADE);");

        connection.Execute("CREATE TABLE IF NOT EXISTS settings (key_name TEXT PRIMARY KEY, value_text TEXT NOT NULL);");

        connection.Execute("CREATE TABLE IF NOT EXISTS player_game_stats (game_id TEXT NOT NULL, player_id TEXT NOT NULL, stat_type TEXT NOT NULL, stat_data TEXT NOT NULL, stats_schema_version INTEGER NOT NULL DEFAULT 1, PRIMARY KEY (game_id, player_id), FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE, FOREIGN KEY(player_id) REFERENCES players(id));");
        connection.Execute("CREATE TABLE IF NOT EXISTS player_stats_snapshots (player_id TEXT NOT NULL, mode TEXT NOT NULL, range_type TEXT NOT NULL, period_key TEXT NOT NULL, stat_data TEXT NOT NULL, stats_schema_version INTEGER NOT NULL DEFAULT 1, updated_at_utc TEXT NOT NULL, PRIMARY KEY (player_id, mode, range_type, period_key), FOREIGN KEY(player_id) REFERENCES players(id) ON DELETE CASCADE);");

        connection.Execute("CREATE TABLE IF NOT EXISTS custom_penalties (id TEXT PRIMARY KEY, name TEXT NOT NULL, short_name TEXT NOT NULL DEFAULT '', info TEXT NOT NULL DEFAULT '', enabled INTEGER NOT NULL DEFAULT 1, cost INTEGER NOT NULL DEFAULT 0, builtin INTEGER NOT NULL DEFAULT 0, condition_kind INTEGER NOT NULL DEFAULT 0, condition_json TEXT NOT NULL DEFAULT '[]');");
        connection.Execute("CREATE TABLE IF NOT EXISTS custom_hit_sectors (id TEXT PRIMARY KEY, name TEXT NOT NULL, info TEXT NOT NULL DEFAULT '', enabled INTEGER NOT NULL DEFAULT 1, builtin INTEGER NOT NULL DEFAULT 0, value INTEGER NOT NULL DEFAULT 0, multiplier INTEGER NOT NULL DEFAULT 0);");
        connection.Execute("CREATE TABLE IF NOT EXISTS audio_events (id TEXT PRIMARY KEY, name TEXT NOT NULL, info TEXT NOT NULL DEFAULT '', builtin INTEGER NOT NULL DEFAULT 0, enabled INTEGER NOT NULL DEFAULT 1, category INTEGER NOT NULL DEFAULT 0, kind INTEGER NOT NULL DEFAULT 0, params_json TEXT NOT NULL DEFAULT '[]');");

        connection.Execute("CREATE TABLE IF NOT EXISTS recorded_clips (id INTEGER PRIMARY KEY AUTOINCREMENT, category TEXT NOT NULL, score INTEGER NOT NULL DEFAULT 0, penalty_type TEXT, special_type TEXT, custom_key TEXT, file_name TEXT NOT NULL, slot_index INTEGER NOT NULL DEFAULT 0, file_size INTEGER, sha256 TEXT, created_at_utc TEXT);");

        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_last_activity ON games(last_activity_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_games_finished_at ON games(finished_at);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_sets_game_id ON sets(game_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_legs_set_id ON legs(set_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_turns_leg_id ON turns(leg_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_throws_turn_id ON throws(turn_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_game_players_player ON game_players(player_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_player_game_stats_player ON player_game_stats(player_id);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_player_stats_snapshots_lookup ON player_stats_snapshots(mode, range_type, period_key);");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_recorded_clips_lookup ON recorded_clips(category, custom_key, score);");
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_recorded_clips_slot ON recorded_clips(category, COALESCE(score, 0), COALESCE(penalty_type, ''), COALESCE(special_type, ''), COALESCE(custom_key, ''), slot_index);");
    }

    private class SettingsRow
    {
        public string KeyName { get; set; }
        public string ValueText { get; set; }
    }

}
