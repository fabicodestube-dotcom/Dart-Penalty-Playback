using System;
using System.Collections.Generic;
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
            TestNewPlayerPersistence();
            TestLegacyMigration();
            TestBackupAndRestore();
            TestClipIntegrityRepair();
            TestCustomHitSectorRoundtrip();
            TestBuiltInHitSectors();
            TestHitSectorDeletionCascadesPenalties();
            TestHitSectorSnapshotSurvivesDeletion();
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
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 3;") == 1, "Fresh database is missing migration v3.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 4;") == 1, "Fresh database is missing migration v4.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 5;") == 1, "Fresh database is missing migration v5.");
            Check(DatabaseHealth.ColumnExists(connection, "game_runtime_state", "custom_hit_sectors_snapshot_json"), "custom_hit_sectors_snapshot_json is missing.");
            Check(DatabaseHealth.ColumnExists(connection, "player_game_stats", "stats_schema_version"), "stats_schema_version is missing.");
            Check(DatabaseHealth.ColumnExists(connection, "recorded_clips", "file_size"), "recorded_clips.file_size is missing.");
            Check(DatabaseHealth.ColumnExists(connection, "recorded_clips", "sha256"), "recorded_clips.sha256 is missing.");
            Check(DatabaseHealth.ColumnExists(connection, "recorded_clips", "created_at_utc"), "recorded_clips.created_at_utc is missing.");
            Check(DatabaseHealth.TableExists(connection, "bot_state"), "bot_state is missing.");
            Check(DatabaseHealth.TableExists(connection, "game_runtime_state"), "game_runtime_state is missing.");
            Check(!DatabaseHealth.TableExists(connection, "player_meta"), "player_meta was not removed.");
            Check(!DatabaseHealth.TableExists(connection, "game_meta"), "game_meta was not removed.");
            Check(!DatabaseHealth.TableExists(connection, "set_meta"), "set_meta was not removed.");
            Check(!DatabaseHealth.TableExists(connection, "leg_meta"), "leg_meta was not removed.");
            Check(!DatabaseHealth.TableExists(connection, "turn_meta"), "turn_meta was not removed.");
            Check(!DatabaseHealth.TableExists(connection, "throw_meta"), "throw_meta was not removed.");
            Check(!DatabaseHealth.ColumnExists(connection, "games", "player_ids"), "games.player_ids was not removed.");
        }

        Debug.Log("[DB-SMOKE] Fresh database OK");
    }

    private static void TestNewPlayerPersistence()
    {
        string root = CreateRoot("new-player");
        DatabaseBootstrapper.ResetForTests();
        SaveSystem.ResetLoadFailedForTests();
        ResetRoot(root);

        var bootstrap = DatabaseBootstrapper.Prepare();
        Check(bootstrap.Success, "New player bootstrap failed: " + bootstrap.Error);

        var database = new Database();
        Guid playerId = database.AddPlayer("Smoke New Player");
        SqliteDatabaseRepository.SaveDatabase(database);

        string createdAt;
        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            createdAt = connection.ExecuteScalar<string>("SELECT created_at FROM players WHERE id = ?;", playerId.ToString());
            Check(!string.IsNullOrWhiteSpace(createdAt), "New player created_at was not persisted.");
        }

        SqliteDatabaseRepository.SaveDatabase(database);

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(connection.ExecuteScalar<string>("SELECT created_at FROM players WHERE id = ?;", playerId.ToString()) == createdAt,
                "Existing player created_at was overwritten.");
        }

        var settings = new X01GameSettings
        {
            pointTarget = 501,
            checkinType = CheckinType.StraightIn,
            checkoutType = CheckoutType.Double,
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = 1,
            legCount = 1,
            soundEnabled = false,
            Penalties = new PenaltySettings()
        };

        var game = new X01Game(Guid.NewGuid(), settings, new List<Guid> { playerId });
        SqliteDatabaseRepository.SaveGame(game);

        var loaded = SqliteDatabaseRepository.LoadGameById(game.GetID());
        Check(loaded != null, "New player game could not be reloaded.");
        Check(loaded.GetPlayerIDs().Count == 1 && loaded.GetPlayerIDs()[0] == playerId, "New player game assignment was lost.");

        Debug.Log("[DB-SMOKE] New player persistence OK");
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
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 3;") == 1, "Migration v3 row is missing.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 4;") == 1, "Migration v4 row is missing.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE version = 5;") == 1, "Migration v5 row is missing.");
            Check(DatabaseHealth.ColumnExists(connection, "game_runtime_state", "custom_hit_sectors_snapshot_json"), "Legacy hit-sector snapshot column was not added.");
            Check(!DatabaseHealth.TableExists(connection, "player_meta"), "Legacy player_meta was not removed.");
            Check(!DatabaseHealth.TableExists(connection, "game_meta"), "Legacy game_meta was not removed.");
            Check(!DatabaseHealth.ColumnExists(connection, "games", "player_ids"), "Legacy games.player_ids was not removed.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM game_players;") == 1, "Legacy game player assignment was lost.");
            Check(connection.ExecuteScalar<int>("SELECT setup_start_position FROM players LIMIT 1;") == 2, "Legacy setup start position was not migrated.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM bot_state;") == 1, "Legacy bot state was not migrated.");
            Check(connection.ExecuteScalar<string>("SELECT difficulty FROM bot_state LIMIT 1;") == "Hard", "Legacy bot difficulty was not migrated.");
            Check(connection.ExecuteScalar<int>("SELECT number FROM bot_state LIMIT 1;") == 7, "Legacy bot number was not migrated.");
            Check(connection.ExecuteScalar<int>("SELECT x01_sudden_death_triggered FROM game_runtime_state LIMIT 1;") == 1, "Legacy runtime state was not migrated.");
            Check(connection.ExecuteScalar<string>("SELECT x01_player_scores_json FROM game_runtime_state LIMIT 1;") != null, "Legacy player scores were not migrated.");

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
        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath))
            connection.Execute("DROP INDEX IF EXISTS ux_recorded_clips_slot;");
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
        Check(repair.After.MetadataRows.Count == 0, "Clip repair left stale metadata behind.");
        Check(repair.After.OrphanFiles.Count == 0, "Clip repair left orphan files behind.");
        Check(repair.After.DuplicateSlotRows.Count == 0, "Clip repair left duplicate slots behind.");
        Check(repair.RenumberedSlots == 1, "Clip repair renumbered the wrong slot count: " + repair.RenumberedSlots);
        Check(repair.DeletedDuplicateRows == 0, "Clip repair deleted different files in duplicate slots.");
        Check(repair.After.RowCount == 4, "Clip repair kept the wrong row count: " + repair.After.RowCount);

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ux_recorded_clips_slot';") == 1,
                "Clip repair did not restore the unique slot index.");
        }

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

    private static void TestBuiltInHitSectors()
    {
        string root = CreateRoot("builtin-sectors");
        DatabaseBootstrapper.ResetForTests();
        SaveSystem.ResetLoadFailedForTests();
        ResetRoot(root);

        var bootstrap = DatabaseBootstrapper.Prepare();
        Check(bootstrap.Success, "Built-in sector bootstrap failed: " + bootstrap.Error);

        CustomizationStore.Reload();

        var sectors = CustomizationStore.GetHitSectors();
        var wall = sectors.Find(item => item != null && item.id == CustomHitDef.WallId);
        var ceiling = sectors.Find(item => item != null && item.id == CustomHitDef.CeilingId);

        Check(wall != null && wall.builtIn, "Wall seed is not built-in.");
        Check(ceiling != null && ceiling.builtIn, "Ceiling seed is not built-in.");

        wall.builtIn = false;
        ceiling.builtIn = false;
        SqliteDatabaseRepository.SaveCustomHitSector(wall);
        SqliteDatabaseRepository.SaveCustomHitSector(ceiling);

        CustomizationStore.Reload();

        sectors = CustomizationStore.GetHitSectors();
        wall = sectors.Find(item => item != null && item.id == CustomHitDef.WallId);
        ceiling = sectors.Find(item => item != null && item.id == CustomHitDef.CeilingId);

        Check(wall != null && wall.builtIn, "Legacy Wall seed was not migrated to built-in.");
        Check(ceiling != null && ceiling.builtIn, "Legacy Ceiling seed was not migrated to built-in.");
        Check(!CustomizationStore.DeleteHitSector(CustomHitDef.WallId), "Built-in Wall sector was deletable.");
        Check(!CustomizationStore.DeleteHitSector(CustomHitDef.CeilingId), "Built-in Ceiling sector was deletable.");
        Check(CustomizationStore.GetHitSectors().Exists(item => item != null && item.id == CustomHitDef.WallId),
            "Wall sector disappeared after delete attempt.");
        Check(CustomizationStore.GetHitSectors().Exists(item => item != null && item.id == CustomHitDef.CeilingId),
            "Ceiling sector disappeared after delete attempt.");

        var custom = CustomHitDef.CreateNew();
        custom.name = "Smoke deletable area";
        Check(CustomizationStore.SaveHitSector(custom), "Custom sector could not be saved.");
        Check(CustomizationStore.DeleteHitSector(custom.id), "Custom sector was not deletable.");

        Directory.Delete(root, true);
        Debug.Log("[DB-SMOKE] Built-in hit sectors OK");
    }

    private static void TestHitSectorDeletionCascadesPenalties()
    {
        string root = CreateRoot("hit-sector-delete");
        DatabaseBootstrapper.ResetForTests();
        SaveSystem.ResetLoadFailedForTests();
        ResetRoot(root);

        var bootstrap = DatabaseBootstrapper.Prepare();
        Check(bootstrap.Success, "Hit-sector delete bootstrap failed: " + bootstrap.Error);

        CustomizationStore.Reload();

        var area = new CustomHitDef
        {
            id = "smoke:delete-area",
            name = "Delete Area",
            info = "smoke",
            enabled = true,
            builtIn = false,
            value = 20,
            multiplier = DartMultiplier.Triple
        };
        Check(CustomizationStore.SaveHitSector(area), "Delete area could not be saved.");

        var instant = CustomPenaltyDef.CreateNew();
        instant.id = "smoke:instant-penalty";
        instant.name = "Instant Penalty";
        instant.conditions = new List<PenaltyCondition>
        {
            new PenaltyCondition
            {
                kind = PenaltyConditionKind.HitTypeMiss,
                hitType = HitType.Custom,
                customHitId = area.id
            }
        };
        Check(CustomizationStore.SavePenalty(instant), "Instant penalty could not be saved.");

        var pattern = CustomPenaltyDef.CreateNew();
        pattern.id = "smoke:pattern-penalty";
        pattern.name = "Pattern Penalty";
        pattern.conditions = new List<PenaltyCondition>
        {
            new PenaltyCondition
            {
                kind = PenaltyConditionKind.TurnPattern,
                pattern = new List<TurnPatternEntry>
                {
                    new TurnPatternEntry
                    {
                        anyHitType = false,
                        hitType = HitType.Custom,
                        customHitId = area.id
                    }
                }
            }
        };
        Check(CustomizationStore.SavePenalty(pattern), "Pattern penalty could not be saved.");

        var unrelated = CustomPenaltyDef.CreateNew();
        unrelated.id = "smoke:unrelated-penalty";
        unrelated.name = "Unrelated Penalty";
        unrelated.conditions = new List<PenaltyCondition>
        {
            new PenaltyCondition
            {
                kind = PenaltyConditionKind.ExactScore,
                score = 100
            }
        };
        Check(CustomizationStore.SavePenalty(unrelated), "Unrelated penalty could not be saved.");

        var usedPenalties = CustomizationStore.GetPenaltiesUsingSector(area.id);
        Check(usedPenalties.Count == 2, "Expected two penalties using the area, got " + usedPenalties.Count);
        var usedNames = CustomizationStore.GetPenaltyNamesUsingSector(area.id);
        Check(usedNames.Contains("Instant Penalty") && usedNames.Contains("Pattern Penalty"),
            "Penalty names using the area are wrong: " + string.Join(", ", usedNames));

        Check(CustomizationStore.DeleteHitSectorWithPenalties(area.id), "Area with penalties was not deletable.");
        Check(!CustomizationStore.GetHitSectors().Exists(item => item != null && item.id == area.id),
            "Deleted area is still in the live store.");
        Check(!CustomizationStore.GetPenalties().Exists(item => item != null && item.id == instant.id),
            "Instant penalty survived area deletion.");
        Check(!CustomizationStore.GetPenalties().Exists(item => item != null && item.id == pattern.id),
            "Pattern penalty survived area deletion.");
        Check(CustomizationStore.GetPenalties().Exists(item => item != null && item.id == unrelated.id),
            "Unrelated penalty was deleted.");
        Check(CustomizationStore.GetPenaltiesUsingSector(area.id).Count == 0,
            "Deleted area still has linked penalties.");

        var orphanArea = new CustomHitDef
        {
            id = "smoke:orphan-area",
            name = "Orphan Area",
            info = "smoke",
            enabled = true,
            builtIn = false,
            value = 5,
            multiplier = DartMultiplier.Single
        };
        Check(CustomizationStore.SaveHitSector(orphanArea), "Orphan area could not be saved.");

        var orphan = CustomPenaltyDef.CreateNew();
        orphan.id = "smoke:orphan-penalty";
        orphan.name = "Orphan Penalty";
        orphan.conditions = new List<PenaltyCondition>
        {
            new PenaltyCondition
            {
                kind = PenaltyConditionKind.HitTypeMiss,
                hitType = HitType.Custom,
                customHitId = orphanArea.id
            }
        };
        Check(CustomizationStore.SavePenalty(orphan), "Orphan penalty could not be saved.");

        Check(CustomizationStore.DeleteHitSector(orphanArea.id), "Area without cascade was not deletable.");
        Check(!CustomizationStore.IsKnownHitSector(orphanArea.id), "Deleted area is still known.");
        Check(CustomizationStore.GetPenalties().Exists(item => item != null && item.id == orphan.id),
            "Area-only delete cascaded into the penalty.");
        Check(CustomizationStore.GetPenaltiesUsingSector(orphanArea.id).Count == 1,
            "Orphan penalty is no longer linked to its deleted area.");
        Check(PenaltyDisplay.SectorName(orphanArea.id).Contains("smoke:"),
            "Deleted area fallback label is wrong: " + PenaltyDisplay.SectorName(orphanArea.id));
        Check(CustomizationStore.DeletePenalty(orphan.id), "Orphan penalty was not deletable.");

        Directory.Delete(root, true);
        Debug.Log("[DB-SMOKE] Hit-sector delete cascade OK");
    }

    private static void TestHitSectorSnapshotSurvivesDeletion()
    {
        string root = CreateRoot("hit-sector-snapshot");
        DatabaseBootstrapper.ResetForTests();
        SaveSystem.ResetLoadFailedForTests();
        ResetRoot(root);

        var bootstrap = DatabaseBootstrapper.Prepare();
        Check(bootstrap.Success, "Hit-sector snapshot bootstrap failed: " + bootstrap.Error);

        var database = new Database();
        Guid playerId = database.AddPlayer("Snapshot Player");
        SqliteDatabaseRepository.SaveDatabase(database);

        var definition = new CustomHitDef
        {
            id = "smoke:snapshot-area",
            name = "Snapshot Area",
            info = "smoke",
            enabled = true,
            builtIn = false,
            value = 20,
            multiplier = DartMultiplier.Triple
        };
        SqliteDatabaseRepository.SaveCustomHitSector(definition);
        CustomizationStore.Reload();

        var settings = new X01GameSettings
        {
            pointTarget = 501,
            checkinType = CheckinType.StraightIn,
            checkoutType = CheckoutType.Double,
            setsAndLegsMode = SetsAndLegs.FirstTo,
            setCount = 1,
            legCount = 1,
            soundEnabled = false,
            Penalties = new PenaltySettings()
        };

        var game = new X01Game(Guid.NewGuid(), settings, new List<Guid> { playerId });

        var frozen = game.GetHitSectorSnapshot()
            .Find(item => item != null && item.id == definition.id);
        Check(frozen != null, "Area was not captured in the game snapshot.");
        Check(frozen.value == 20, "Snapshot area value is wrong.");
        Check(frozen.multiplier == DartMultiplier.Triple, "Snapshot area multiplier is wrong.");

        var live = CustomizationStore.GetHitSectors()
            .Find(item => item != null && item.id == definition.id);
        Check(live != null, "Area was not saved to the live store.");
        live.value = 5;
        live.multiplier = DartMultiplier.Single;
        SqliteDatabaseRepository.SaveCustomHitSector(live);
        CustomizationStore.Reload();

        frozen = game.GetHitSectorSnapshot()
            .Find(item => item != null && item.id == definition.id);
        Check(frozen != null && frozen.value == 20 && frozen.multiplier == DartMultiplier.Triple,
            "Live edit changed the game snapshot.");

        SqliteDatabaseRepository.SaveGame(game);
        Check(CustomizationStore.DeleteHitSector(definition.id), "Snapshot area was not deletable.");
        Check(!CustomizationStore.GetHitSectors().Exists(item => item != null && item.id == definition.id),
            "Deleted area is still in the live store.");

        var loaded = SqliteDatabaseRepository.LoadGameById(game.GetID());
        Check(loaded != null, "Game with hit-sector snapshot could not be reloaded.");

        var restored = loaded.GetHitSectorSnapshot()
            .Find(item => item != null && item.id == definition.id);
        Check(restored != null, "Deleted area was not restored from the game snapshot.");
        Check(restored.value == 20, "Restored snapshot area value is wrong.");
        Check(restored.multiplier == DartMultiplier.Triple, "Restored snapshot area multiplier is wrong.");
        Check(restored.enabled, "Restored snapshot area is disabled.");
        Check(!CustomizationStore.GetHitSectors().Exists(item => item != null && item.id == definition.id),
            "Deleted area reappeared in the live store.");

        SqliteDatabaseRepository.SaveGame(loaded);
        var reloaded = SqliteDatabaseRepository.LoadGameById(game.GetID());
        Check(reloaded != null, "Resaved game with hit-sector snapshot could not be reloaded.");

        var persisted = reloaded.GetHitSectorSnapshot()
            .Find(item => item != null && item.id == definition.id);
        Check(persisted != null && persisted.value == 20 && persisted.multiplier == DartMultiplier.Triple,
            "Hit-sector snapshot was lost after resave.");
        Check(!CustomizationStore.GetHitSectors().Exists(item => item != null && item.id == definition.id),
            "Deleted area reappeared in the live store after resave.");

        Directory.Delete(root, true);
        Debug.Log("[DB-SMOKE] Hit-sector snapshot survives deletion OK");
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

        string actualRecordings = Path.Combine(actualRoot, DatabasePaths.RecordingsFolderName);
        if (Directory.Exists(actualRecordings))
            CopyDirectory(actualRecordings, Path.Combine(root, DatabasePaths.RecordingsFolderName));

        int expectedPlayers;
        int expectedGames;
        int expectedThrows;
        int expectedStatRows;
        int expectedClips;
        int expectedRuntimeRows;
        int expectedBotRows;

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
            expectedClips = DatabaseHealth.TableExists(connection, "recorded_clips")
                ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recorded_clips;")
                : 0;
            expectedRuntimeRows = DatabaseHealth.TableExists(connection, "game_runtime_state")
                ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM game_runtime_state;")
                : (DatabaseHealth.TableExists(connection, "game_meta")
                    ? connection.ExecuteScalar<int>("SELECT COUNT(DISTINCT game_id) FROM game_meta WHERE key_name IN ('player_scores','player_checked_in','sudden_death_triggered','suddenDeathTriggered','player_x01_stats','atc_targets','atc_target_multipliers','atc_progress','custom_penalties_snapshot','audio_events_snapshot');")
                    : 0);
            expectedBotRows = DatabaseHealth.TableExists(connection, "bot_state")
                ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM bot_state;")
                : (DatabaseHealth.TableExists(connection, "players")
                    ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM players WHERE type_name = 'DartBot';")
                    : 0);
        }

        DatabaseBootstrapper.ResetForTests();
        ResetRoot(root);
        if (Directory.Exists(actualRecordings))
            CopyDirectory(actualRecordings, Path.Combine(root, DatabasePaths.RecordingsFolderName));
        File.Copy(actualDatabase, DatabasePaths.DatabasePath, true);

        var result = DatabaseBootstrapper.Prepare();
        Check(result.Success, "Real database copy migration failed: " + result.Error);
        if (result.Migrated)
            Check(File.Exists(result.BackupPath), "Real database copy migration created no backup.");

        CustomizationStore.Reload();

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM players;") == expectedPlayers, "Player count changed after migration.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM games;") == expectedGames, "Game count changed after migration.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM throws;") == expectedThrows, "Throw count changed after migration.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recorded_clips;") == expectedClips, "Clip count changed after migration.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM game_runtime_state;") == expectedRuntimeRows, "Runtime state count changed after migration.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM bot_state;") == expectedBotRows, "Bot state count changed after migration.");
            Check(!DatabaseHealth.TableExists(connection, "player_meta"), "Real database copy still has player_meta.");
            Check(!DatabaseHealth.TableExists(connection, "game_meta"), "Real database copy still has game_meta.");
            Check(!DatabaseHealth.ColumnExists(connection, "games", "player_ids"), "Real database copy still has games.player_ids.");
            Check(DatabaseHealth.GetUserVersion(connection) == DatabaseSchema.CurrentVersion, "Real database copy has wrong version.");

            int nonUtcTimestamps = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM games WHERE created_at NOT LIKE '%Z' OR last_activity_at NOT LIKE '%Z' OR (finished_at IS NOT NULL AND finished_at NOT LIKE '%Z');");
            Check(nonUtcTimestamps == 0, "Real database copy still has non-UTC game timestamps: " + nonUtcTimestamps);
        }

        var clipReport = RecordedClipIntegrityService.Scan(true);
        Check(clipReport.IsHealthy, "Real database copy clip integrity failed: " + clipReport.ToSummary());

        var consistency = DatabaseConsistencyService.Scan(true);
        Check(consistency.IsHealthy, "Real database copy consistency failed: " + string.Join("; ", consistency.Issues));

        var gameIds = new List<Guid>();
        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            foreach (var row in connection.Query<GameIdRow>("SELECT id AS Id FROM games ORDER BY last_activity_at;"))
            {
                if (Guid.TryParse(row.Id, out var gameId))
                    gameIds.Add(gameId);
            }
        }

        Check(gameIds.Count == expectedGames, "Real database copy did not expose all games.");
        foreach (Guid gameId in gameIds)
        {
            var game = SqliteDatabaseRepository.LoadGameById(gameId);
            Check(game != null, "Real database copy game could not be loaded: " + gameId);
            Check(game.GetPlayerIDs().Count > 0, "Real database copy game has no players: " + gameId);
            Check(game.GetSettings() != null, "Real database copy game has no settings: " + gameId);

            var playerIds = game.GetPlayerIDs().ToList();
            int throwCount = CountThrows(game);
            SqliteDatabaseRepository.SaveGame(game);

            var reloaded = SqliteDatabaseRepository.LoadGameById(gameId);
            Check(reloaded != null, "Real database copy game could not be reloaded: " + gameId);
            Check(reloaded.GetPlayerIDs().SequenceEqual(playerIds), "Real database copy player order changed: " + gameId);
            Check(CountThrows(reloaded) == throwCount, "Real database copy throw count changed: " + gameId);
            Check(reloaded.GetSettings() != null, "Real database copy settings were lost: " + gameId);
        }

        var loadedDatabase = SqliteDatabaseRepository.LoadDatabase();
        Check(loadedDatabase.GetGameSummaries().Count == expectedGames, "Real database copy summary count changed.");
        Check(loadedDatabase.GetAllPlayers().Count == expectedPlayers, "Real database copy player summary count changed.");

        SqliteDatabaseRepository.SaveDatabase(loadedDatabase);

        using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
        {
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM players;") == expectedPlayers, "Real database copy player count changed after resave.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM bot_state;") == expectedBotRows, "Real database copy bot state changed after resave.");
            Check(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_foreign_key_check;") == 0, "Real database copy resave created foreign key violations.");
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
            connection.Execute("CREATE TABLE player_meta (player_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (player_id, key_name));");
            connection.Execute("CREATE TABLE games (id TEXT PRIMARY KEY, game_mode TEXT NOT NULL, created_at TEXT NOT NULL, last_activity_at TEXT NOT NULL, finished_at TEXT, current_player_index INTEGER NOT NULL DEFAULT 0, starting_player_index INTEGER NOT NULL DEFAULT 0, winner_player_id TEXT, player_ids TEXT NOT NULL DEFAULT '', settings_json TEXT);");
            connection.Execute("CREATE TABLE game_meta (game_id TEXT NOT NULL, key_name TEXT NOT NULL, value_text TEXT, PRIMARY KEY (game_id, key_name));");
            connection.Execute("CREATE TABLE game_players (game_id TEXT NOT NULL, player_id TEXT NOT NULL, player_order INTEGER NOT NULL, PRIMARY KEY (game_id, player_id));");
            connection.Execute("CREATE TABLE player_game_stats (game_id TEXT NOT NULL, player_id TEXT NOT NULL, stat_type TEXT NOT NULL, stat_data TEXT NOT NULL, PRIMARY KEY (game_id, player_id));");
            connection.Execute("CREATE TABLE settings (key_name TEXT PRIMARY KEY, value_text TEXT NOT NULL);");

            string playerId = Guid.NewGuid().ToString();
            string gameId = Guid.NewGuid().ToString();

            connection.Execute(
                "INSERT INTO players (id, name, type_name, created_at) VALUES (?, ?, ?, ?);",
                playerId,
                "Legacy Bot",
                "DartBot",
                LegacyPlayerCreatedAt
            );

            connection.Execute(
                "INSERT INTO player_meta (player_id, key_name, value_text) VALUES (?, ?, ?);",
                playerId, "setupStartPosition", "2");
            connection.Execute(
                "INSERT INTO player_meta (player_id, key_name, value_text) VALUES (?, ?, ?);",
                playerId, "difficulty", "Hard");
            connection.Execute(
                "INSERT INTO player_meta (player_id, key_name, value_text) VALUES (?, ?, ?);",
                playerId, "number", "7");
            connection.Execute(
                "INSERT INTO player_meta (player_id, key_name, value_text) VALUES (?, ?, ?);",
                playerId, "confidence", "1.5");
            connection.Execute(
                "INSERT INTO player_meta (player_id, key_name, value_text) VALUES (?, ?, ?);",
                playerId, "dartsThrown", "42");
            connection.Execute(
                "INSERT INTO player_meta (player_id, key_name, value_text) VALUES (?, ?, ?);",
                playerId, "streak", "3");

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
                "INSERT INTO game_players (game_id, player_id, player_order) VALUES (?, ?, ?);",
                gameId,
                playerId,
                0
            );

            connection.Execute(
                "INSERT INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);",
                gameId, "player_scores", "{\"" + playerId + "\":301}");
            connection.Execute(
                "INSERT INTO game_meta (game_id, key_name, value_text) VALUES (?, ?, ?);",
                gameId, "sudden_death_triggered", "True");

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

    private static int CountThrows(Game game)
    {
        if (game == null)
            return 0;

        return game.GetSets()
            .Sum(set => set.GetLegs()
                .Sum(leg => leg.GetTurns()
                    .Sum(turn => turn.GetThrows().Count)));
    }

    private sealed class GameIdRow
    {
        public string Id { get; set; }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
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
