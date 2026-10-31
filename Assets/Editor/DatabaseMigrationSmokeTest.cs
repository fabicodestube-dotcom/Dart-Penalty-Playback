using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SQLite;
using UnityEditor;
using UnityEngine;

public static class DatabaseMigrationSmokeTest
{
    private const string LegacyPlayerCreatedAt = "2026-01-02T02:00:00.0000000-05:00";
    private const string LegacyGameCreatedAt = "2026-01-02T03:04:05.1234567+01:00";
    private const string LegacyGameLastActivityAt = "2026-01-02T03:05:06.7654321+01:00";
    private const string LegacyGameFinishedAt = "2026-01-02T03:06:07.0000000+01:00";

    [MenuItem("Tools/Database/Run Migration Smoke Test")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "darts-db-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        string previousRoot = DatabasePaths.RootOverride;

        try
        {
            DatabasePaths.RootOverride = tempRoot;

            string actualRoot = Application.persistentDataPath;

            TestFreshDatabase(tempRoot);
            TestLegacyMigration();
            TestBackupAndRestore();
            TestClipIntegrityRepair();
            TestCustomHitSectorRoundtrip();
            TestActualDatabaseCopyIfAvailable(actualRoot);

            Debug.Log("[DB-SMOKE] PASS");
        }
        finally
        {
            DatabasePaths.RootOverride = previousRoot;
            DatabaseBootstrapper.ResetForTests();
            SaveSystem.ResetLoadFailedForTests();

            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DB-SMOKE] Cleanup failed: " + e.Message);
            }
        }
    }

    private static void TestFreshDatabase(string tempRoot)
    {
        DatabaseBootstrapper.ResetForTests();
        SaveSystem.ResetLoadFailedForTests();
        ResetRoot(tempRoot);

        var result = DatabaseBootstrapper.Prepare();
        Check(result.Success, "Fresh bootstrap failed: " + result.Error);
        Check(result.CreatedNewDatabase, "Fresh bootstrap did not report a new database.");
        Check(File.Exists(DatabasePaths.DatabasePath), "Fresh database file is missing.");

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(DatabaseHealth.GetUserVersion(connection) == DatabaseSchema.CurrentVersion, "Fresh database has wrong user_version.");
            Check(DatabaseHealth.GetApplicationId(connection) == DatabaseSchema.ApplicationId, "Fresh database has wrong application_id.");
            Check(DatabaseHealth.TableExists(connection, "schema_migrations"), "schema_migrations is missing.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 2;") == 1, "Fresh database is missing migration v2.");
            Check(DatabaseHealth.ColumnExists(connection, "player_game_stats", "stats_schema_version"), "stats_schema_version is missing.");
        }

        Debug.Log("[DB-SMOKE] Fresh database OK");
    }

    private static void TestLegacyMigration()
    {
        string root = CreateRoot("legacy");
        DatabaseBootstrapper.ResetForTests();
        ResetRoot(root);

        CreateLegacyDatabase(DatabasePaths.DatabasePath);

        var result = DatabaseBootstrapper.Prepare();
        Check(result.Success, "Legacy migration failed: " + result.Error);
        Check(result.Migrated, "Legacy bootstrap did not report a migration.");
        Check(result.FromSchemaVersion == 0, "Legacy migration started from the wrong version.");
        Check(!string.IsNullOrEmpty(result.BackupPath) && File.Exists(result.BackupPath), "Pre-migration backup is missing.");

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(DatabaseHealth.GetUserVersion(connection) == DatabaseSchema.CurrentVersion, "Legacy database was not migrated.");
            Check(DatabaseHealth.GetApplicationId(connection) == DatabaseSchema.ApplicationId, "legacy application_id was not set.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM players;") == 1, "Legacy player row was lost.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM player_game_stats;") == 1, "Legacy stats row was lost.");
            Check(DatabaseHealth.ColumnExists(connection, "player_game_stats", "stats_schema_version"), "Legacy stats column was not added.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 1;") == 1, "Migration v1 row is missing.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 2;") == 1, "Migration v2 row is missing.");

            CheckUtcTimestamp(
                connection.ExecuteScalar<string>("SELECT created_at FROM players LIMIT 1;"),
                LegacyPlayerCreatedAt,
                "player.created_at"
            );

            CheckUtcTimestamp(
                connection.ExecuteScalar<string>("SELECT created_at FROM games LIMIT 1;"),
                LegacyGameCreatedAt,
                "game.created_at"
            );

            CheckUtcTimestamp(
                connection.ExecuteScalar<string>("SELECT last_activity_at FROM games LIMIT 1;"),
                LegacyGameLastActivityAt,
                "game.last_activity_at"
            );

            CheckUtcTimestamp(
                connection.ExecuteScalar<string>("SELECT finished_at FROM games LIMIT 1;"),
                LegacyGameFinishedAt,
                "game.finished_at"
            );
        }

        Debug.Log("[DB-SMOKE] Legacy migration OK");
    }

    private static void TestBackupAndRestore()
    {
        string root = Path.Combine(Path.GetTempPath(), "darts-db-smoke-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        DatabaseBootstrapper.ResetForTests();
        ResetRoot(root);

        var bootstrap = DatabaseBootstrapper.Prepare();
        Check(bootstrap.Success, "Backup test bootstrap failed: " + bootstrap.Error);

        string recording = Path.Combine(DatabasePaths.RecordingsRoot, "Scores", "smoke.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(recording));
        File.WriteAllBytes(recording, new byte[] { 1, 2, 3, 4 });

        var backup = DatabaseBackupService.CreateBackup("smoke", DatabaseSchema.CurrentVersion, true);
        Check(backup.Success && File.Exists(backup.Path), "Backup was not created.");

        File.Delete(recording);
        File.WriteAllText(DatabasePaths.DatabasePath, "not a sqlite database");

        bool restored = DatabaseBackupService.TryRestoreLatestDatabaseBackup(
            DatabaseSchema.CurrentVersion,
            out string restoredBackup,
            out string restoreError
        );

        Check(restored, "Restore failed: " + restoreError);
        Check(File.Exists(restoredBackup), "Restore did not report a valid backup.");
        Check(DatabaseHealth.TryQuickCheck(DatabasePaths.DatabasePath, out string healthError), "Restored database is invalid: " + healthError);
        Check(File.Exists(recording), "Recordings were not restored.");

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(DatabaseHealth.GetUserVersion(connection) == DatabaseSchema.CurrentVersion, "Restored database has wrong version.");
        }

        foreach (string otherBackup in Directory.GetFiles(DatabasePaths.BackupsRoot, "darts-backup-*.zip"))
        {
            if (!string.Equals(otherBackup, backup.Path, StringComparison.Ordinal))
                File.Delete(otherBackup);
        }

        TamperBackupFile(backup.Path, "recordings/Scores/smoke.wav");

        bool tamperedRestored = DatabaseBackupService.TryRestoreLatestDatabaseBackup(
            DatabaseSchema.CurrentVersion,
            out _,
            out _
        );

        Check(!tamperedRestored, "Tampered backup was accepted.");
        Check(DatabaseHealth.TryQuickCheck(DatabasePaths.DatabasePath, out string tamperHealthError), "Rejected tampered backup changed the database: " + tamperHealthError);

        try
        {
            Directory.Delete(root, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DB-SMOKE] Backup test cleanup failed: " + e.Message);
        }

        Debug.Log("[DB-SMOKE] Backup/restore OK");
    }

    private static void TestClipIntegrityRepair()
    {
        string root = CreateRoot("clip-integrity");
        DatabaseBootstrapper.ResetForTests();
        ResetRoot(root);

        var bootstrap = DatabaseBootstrapper.Prepare();
        Check(bootstrap.Success, "Clip integrity bootstrap failed: " + bootstrap.Error);

        string scoresFolder = Path.Combine(DatabasePaths.RecordingsRoot, "Scores");
        string specialFolder = Path.Combine(DatabasePaths.RecordingsRoot, "Special");
        Directory.CreateDirectory(scoresFolder);
        Directory.CreateDirectory(specialFolder);

        string validScoreFile = Path.Combine(scoresFolder, "valid_score.wav");
        string validCustomFile = Path.Combine(specialFolder, "valid_custom.wav");
        string duplicateFileA = Path.Combine(scoresFolder, "duplicate_a.wav");
        string duplicateFileB = Path.Combine(scoresFolder, "duplicate_b.wav");
        string orphanFile = Path.Combine(scoresFolder, "orphan.wav");

        File.WriteAllBytes(validScoreFile, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(validCustomFile, new byte[] { 4, 5, 6 });
        File.WriteAllBytes(duplicateFileA, new byte[] { 7, 8, 9 });
        File.WriteAllBytes(duplicateFileB, new byte[] { 10, 11, 12 });
        File.WriteAllBytes(orphanFile, new byte[] { 13, 14, 15 });

        SqliteDatabaseRepository.InsertRecordedClip("Score", 10, null, null, null, "valid_score.wav", 1);
        SqliteDatabaseRepository.InsertRecordedClip("Custom", 0, null, null, "event", "valid_custom.wav", 1);
        SqliteDatabaseRepository.InsertRecordedClip("Score", 11, null, null, null, "missing.wav", 1);
        SqliteDatabaseRepository.InsertRecordedClip("Score", 12, null, null, null, "../escape.wav", 1);
        SqliteDatabaseRepository.InsertRecordedClip("Bogus", 0, null, null, null, "unknown.wav", 1);
        SqliteDatabaseRepository.InsertRecordedClip("Score", 13, null, null, null, "duplicate_a.wav", 1);
        SqliteDatabaseRepository.InsertRecordedClip("Score", 13, null, null, null, "duplicate_b.wav", 1);

        var report = RecordedClipIntegrityService.Scan();
        Check(report.RowCount == 7, "Clip scan found the wrong row count: " + report.RowCount);
        Check(report.InvalidRows.Count == 3, "Clip scan found the wrong invalid row count: " + report.InvalidRows.Count);
        Check(report.DuplicateSlotRows.Count == 1, "Clip scan found the wrong duplicate slot count: " + report.DuplicateSlotRows.Count);
        Check(report.OrphanFiles.Count == 1, "Clip scan found the wrong orphan count: " + report.OrphanFiles.Count);

        var repair = RecordedClipIntegrityService.Repair(true);
        Check(repair.DeletedRows == 3, "Clip repair deleted the wrong row count: " + repair.DeletedRows);
        Check(repair.QuarantinedFiles == 1, "Clip repair quarantined the wrong file count: " + repair.QuarantinedFiles);
        Check(repair.After.InvalidRows.Count == 0, "Clip repair left invalid rows behind.");
        Check(repair.After.OrphanFiles.Count == 0, "Clip repair left orphan files behind.");
        Check(repair.After.DuplicateSlotRows.Count == 1, "Clip repair should not silently delete duplicate slots.");
        Check(repair.After.RowCount == 4, "Clip repair kept the wrong row count: " + repair.After.RowCount);

        Check(File.Exists(validScoreFile), "Clip repair deleted a valid score file.");
        Check(File.Exists(validCustomFile), "Clip repair deleted a valid custom file.");
        Check(File.Exists(duplicateFileA), "Clip repair deleted duplicate file A.");
        Check(File.Exists(duplicateFileB), "Clip repair deleted duplicate file B.");
        Check(!File.Exists(orphanFile), "Clip repair did not move the orphan file.");
        Check(!string.IsNullOrEmpty(repair.QuarantineRoot), "Clip repair did not report a quarantine folder.");
        Check(File.Exists(Path.Combine(repair.QuarantineRoot, "Scores", "orphan.wav")), "Quarantined orphan file is missing.");

        Directory.Delete(root, true);
        Debug.Log("[DB-SMOKE] Clip integrity OK");
    }

    private static void TestCustomHitSectorRoundtrip()
    {
        string root = CreateRoot("hit-sector");
        DatabaseBootstrapper.ResetForTests();
        ResetRoot(root);

        var bootstrap = DatabaseBootstrapper.Prepare();
        Check(bootstrap.Success, "Hit sector bootstrap failed: " + bootstrap.Error);

        var definition = new CustomHitDef
        {
            id = "smoke:triple20",
            name = "Triple 20",
            info = "smoke",
            enabled = true,
            builtIn = false,
            value = 20,
            multiplier = DartMultiplier.Triple
        };

        SqliteDatabaseRepository.SaveCustomHitSector(definition);

        var loaded = SqliteDatabaseRepository.LoadCustomHitSectors()
            .Find(item => item != null && item.id == definition.id);

        Check(loaded != null, "Custom hit sector was not loaded.");
        Check(loaded.value == 20, "Custom hit sector value was not loaded.");
        Check(loaded.multiplier == DartMultiplier.Triple, "Custom hit sector multiplier was not loaded.");
        Check(loaded.enabled, "Custom hit sector enabled flag was not loaded.");

        Check(SqliteDatabaseRepository.DeleteCustomHitSector(definition.id), "Custom hit sector was not deleted.");

        var deleted = SqliteDatabaseRepository.LoadCustomHitSectors()
            .Find(item => item != null && item.id == definition.id);
        Check(deleted == null, "Deleted custom hit sector is still present.");

        Directory.Delete(root, true);
        Debug.Log("[DB-SMOKE] Custom hit sector roundtrip OK");
    }

    private static void TestActualDatabaseCopyIfAvailable(string actualRoot)
    {
        string actualDatabase = Path.Combine(actualRoot, DatabasePaths.DatabaseFileName);
        if (!File.Exists(actualDatabase))
        {
            Debug.Log("[DB-SMOKE] No real database found, skipping copy test.");
            return;
        }

        string root = CreateRoot("actual-copy");
        string copiedDatabase = Path.Combine(root, DatabasePaths.DatabaseFileName);
        File.Copy(actualDatabase, copiedDatabase, true);

        int expectedPlayers;
        int expectedGames;
        int expectedThrows;
        int expectedStatRows;

        using (var connection = new SQLiteConnection(copiedDatabase, SQLiteOpenFlags.ReadOnly))
        {
            expectedPlayers = DatabaseHealth.TableExists(connection, "players")
                ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM players;")
                : 0;
            expectedGames = DatabaseHealth.TableExists(connection, "games")
                ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM games;")
                : 0;
            expectedThrows = DatabaseHealth.TableExists(connection, "throws")
                ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM throws;")
                : 0;
            expectedStatRows = DatabaseHealth.TableExists(connection, "player_game_stats")
                ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM player_game_stats;")
                : 0;
        }

        DatabaseBootstrapper.ResetForTests();
        ResetRoot(root);
        File.Copy(actualDatabase, DatabasePaths.DatabasePath, true);

        var result = DatabaseBootstrapper.Prepare();
        Check(result.Success, "Real database copy migration failed: " + result.Error);
        if (result.Migrated)
            Check(File.Exists(result.BackupPath), "Real database copy migration created no backup.");

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM players;") == expectedPlayers, "Player count changed after migration.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM games;") == expectedGames, "Game count changed after migration.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM throws;") == expectedThrows, "Throw count changed after migration.");
            Check(DatabaseHealth.GetUserVersion(connection) == DatabaseSchema.CurrentVersion, "Real database copy has wrong version.");

            int nonUtcTimestamps = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM games WHERE created_at NOT LIKE '%Z' OR last_activity_at NOT LIKE '%Z' OR (finished_at IS NOT NULL AND finished_at NOT LIKE '%Z');");
            Check(nonUtcTimestamps == 0, "Real database copy still has non-UTC game timestamps: " + nonUtcTimestamps);
        }

        if (expectedStatRows > 0)
        {
            using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadWrite))
            {
                connection.Execute("UPDATE player_game_stats SET stats_schema_version = 0;");
            }

            PlayerStatsBackfillService.EnsureCurrent();

            using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
            {
                Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM player_game_stats WHERE stats_schema_version < ?;", DatabaseSchema.CurrentStatsVersion) == 0, "Stats backfill left outdated rows.");
                Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM player_game_stats;") == expectedStatRows, "Stats backfill changed the row count.");
                Check(DatabaseMigrationRunner.GetMeta(connection, DatabaseSchema.StatsVersionMetaKey) == DatabaseSchema.CurrentStatsVersion.ToString(), "Stats version meta was not updated.");
            }
        }

        Directory.Delete(root, true);
        Debug.Log("[DB-SMOKE] Real database copy OK");
    }

    private static void TamperBackupFile(string backupPath, string entryName)
    {
        using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry(entryName);
            Check(entry != null, "Backup entry is missing: " + entryName);

            entry.Delete();
            var replacement = archive.CreateEntry(entryName);

            using (var stream = replacement.Open())
            {
                stream.WriteByte(9);
            }
        }
    }

    private static void CreateLegacyDatabase(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath));

        using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
        {
            connection.Execute("CREATE TABLE players (id TEXT PRIMARY KEY, name TEXT NOT NULL, type_name TEXT NOT NULL, deleted INTEGER NOT NULL DEFAULT 0, show_in_statistics INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')));");
            connection.Execute("CREATE TABLE games (id TEXT PRIMARY KEY, game_mode TEXT NOT NULL, created_at TEXT NOT NULL, last_activity_at TEXT NOT NULL, finished_at TEXT, current_player_index INTEGER NOT NULL DEFAULT 0, starting_player_index INTEGER NOT NULL DEFAULT 0, winner_player_id TEXT, player_ids TEXT NOT NULL DEFAULT '', settings_json TEXT);");
            connection.Execute("CREATE TABLE player_game_stats (game_id TEXT NOT NULL, player_id TEXT NOT NULL, stat_type TEXT NOT NULL, stat_data TEXT NOT NULL, PRIMARY KEY (game_id, player_id));");
            connection.Execute("CREATE TABLE settings (key_name TEXT PRIMARY KEY, value_text TEXT NOT NULL);");

            string playerId = Guid.NewGuid().ToString();
            string gameId = Guid.NewGuid().ToString();

            connection.Execute(
                "INSERT INTO players (id, name, type_name, created_at) VALUES (?, ?, ?, ?);",
                playerId,
                "Legacy Player",
                "Player",
                LegacyPlayerCreatedAt
            );

            connection.Execute(
                "INSERT INTO games (id, game_mode, created_at, last_activity_at, finished_at, player_ids) VALUES (?, ?, ?, ?, ?, ?);",
                gameId,
                "X01",
                LegacyGameCreatedAt,
                LegacyGameLastActivityAt,
                LegacyGameFinishedAt,
                playerId
            );

            connection.Execute(
                "INSERT INTO player_game_stats (game_id, player_id, stat_type, stat_data) VALUES (?, ?, ?, ?);",
                gameId,
                playerId,
                "x01",
                "{}"
            );

            connection.Execute("PRAGMA user_version = 0;");
        }
    }

    private static void ResetRoot(string root)
    {
        DatabasePaths.RootOverride = root;

        if (Directory.Exists(root))
            Directory.Delete(root, true);

        Directory.CreateDirectory(root);
    }

    private static string CreateRoot(string name)
    {
        string root = Path.Combine(Path.GetTempPath(), "darts-db-smoke-" + name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CheckUtcTimestamp(string actual, string expectedSource, string label)
    {
        Check(!string.IsNullOrEmpty(actual) && actual.EndsWith("Z", StringComparison.Ordinal),
            label + " is not stored as UTC: " + actual);

        DateTimeOffset actualValue = DateTimeOffset.Parse(actual);
        DateTimeOffset expectedValue = DateTimeOffset.Parse(expectedSource);

        Check(actualValue.ToUniversalTime() == expectedValue.ToUniversalTime(),
            label + " changed instant: " + actual);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[DB-SMOKE] " + message);
    }
}
