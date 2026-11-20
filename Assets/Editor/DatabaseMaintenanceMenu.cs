using UnityEditor;
using UnityEngine;

public static class DatabaseMaintenanceMenu
{
    [MenuItem("Tools/Database/Scan Clip Integrity")]
    public static void ScanClipIntegrity()
    {
        ScanClips(false);
    }

    [MenuItem("Tools/Database/Scan Clip Integrity (Hashes)")]
    public static void ScanClipIntegrityWithHashes()
    {
        ScanClips(true);
    }

    private static void ScanClips(bool verifyHashes)
    {
        try
        {
            var report = RecordedClipIntegrityService.Scan(verifyHashes);
            Debug.Log("[DB-MAINTENANCE] Clip integrity: " + report.ToSummary());

            foreach (var issue in report.InvalidRows)
                Debug.LogWarning("[DB-MAINTENANCE] Invalid clip row: " + issue);

            foreach (var issue in report.MetadataRows)
                Debug.LogWarning("[DB-MAINTENANCE] Clip metadata: " + issue);

            foreach (var issue in report.DuplicateSlotRows)
                Debug.LogWarning("[DB-MAINTENANCE] Duplicate clip slot: " + issue);

            foreach (string orphan in report.OrphanFiles)
                Debug.LogWarning("[DB-MAINTENANCE] Orphan clip file: " + orphan);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Clip scan failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Repair Clip Index")]
    public static void RepairClipIndex()
    {
        try
        {
            var result = RecordedClipIntegrityService.Repair(true, true);
            Debug.Log("[DB-MAINTENANCE] Clip repair: " + result.ToSummary());

            if (!string.IsNullOrEmpty(result.QuarantineRoot))
                Debug.Log("[DB-MAINTENANCE] Orphans moved to: " + result.QuarantineRoot);

            foreach (string failure in result.Failures)
                Debug.LogWarning("[DB-MAINTENANCE] Repair failure: " + failure);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Clip repair failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Create Backup Now")]
    public static void CreateBackupNow()
    {
        try
        {
            var result = DatabaseBackupService.CreateBackup(
                "manual",
                DatabaseSchema.CurrentVersion,
                true,
                true);

            Debug.Log("[DB-MAINTENANCE] Backup created: " + result.Path);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Backup failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Create Known-Good Snapshot")]
    public static void CreateKnownGoodSnapshot()
    {
        try
        {
            if (DatabaseBackupService.TryCreateKnownGoodSnapshot(
                    DatabaseSchema.CurrentVersion,
                    out string path,
                    out string error))
            {
                Debug.Log("[DB-MAINTENANCE] Known-good snapshot created: " + path);
            }
            else
            {
                Debug.LogError("[DB-MAINTENANCE] Known-good snapshot failed: " + error);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Known-good snapshot failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Restore Latest Backup")]
    public static void RestoreLatestBackup()
    {
        if (!EditorUtility.DisplayDialog(
                "Backup wiederherstellen",
                "Die aktuelle Datenbank wird quarantänisiert und das neueste gültige Backup wiederhergestellt.\n\nFortfahren?",
                "Wiederherstellen",
                "Abbrechen"))
            return;

        try
        {
            if (!DatabaseBackupService.TryRestoreLatestDatabaseBackup(
                    DatabaseSchema.CurrentVersion,
                    out string restoredBackup,
                    out string error))
            {
                Debug.LogError("[DB-MAINTENANCE] Restore failed: " + error);
                return;
            }

            DatabaseBootstrapper.ResetForTests();
            SaveSystem.ResetLoadFailedForTests();

            Debug.Log("[DB-MAINTENANCE] Restored latest backup: " + restoredBackup);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Restore failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Validate Database and Backups")]
    public static void ValidateDatabaseAndBackups()
    {
        try
        {
            string databasePath = DatabasePaths.DatabasePath;

            if (!DatabaseHealth.TryFullCheck(databasePath, out string integrityError))
            {
                Debug.LogError("[DB-MAINTENANCE] Database integrity failed: " + integrityError);
                return;
            }

            using (var connection = new SQLite.SQLiteConnection(databasePath, SQLite.SQLiteOpenFlags.ReadOnly))
            {
                if (!DatabaseHealth.TryValidateSchema(connection, DatabaseSchema.CurrentVersion, out string schemaError))
                {
                    Debug.LogError("[DB-MAINTENANCE] Database schema failed: " + schemaError);
                    return;
                }
            }

            Debug.Log("[DB-MAINTENANCE] Database is healthy.");

            if (!System.IO.Directory.Exists(DatabasePaths.BackupsRoot))
                return;

            foreach (string backup in System.IO.Directory.EnumerateFiles(
                         DatabasePaths.BackupsRoot,
                         "darts-backup-*.zip",
                         System.IO.SearchOption.TopDirectoryOnly))
            {
                if (DatabaseBackupService.TryValidateBackup(backup, out string backupError))
                    Debug.Log("[DB-MAINTENANCE] Backup OK: " + backup);
                else
                    Debug.LogWarning("[DB-MAINTENANCE] Backup invalid: " + backup + " (" + backupError + ")");
            }

            if (System.IO.File.Exists(DatabasePaths.KnownGoodDatabasePath))
            {
                if (DatabaseBackupService.TryValidateBackup(DatabasePaths.KnownGoodDatabasePath, out string knownGoodError))
                    Debug.Log("[DB-MAINTENANCE] Known-good snapshot OK.");
                else
                    Debug.LogWarning("[DB-MAINTENANCE] Known-good snapshot invalid: " + knownGoodError);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Validation failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Scan Database")]
    public static void ScanDatabase()
    {
        try
        {
            var report = DatabaseConsistencyService.Scan(true);
            Debug.Log("[DB-MAINTENANCE] Database consistency: " + report.ToSummary());

            foreach (string issue in report.Issues)
                Debug.LogWarning("[DB-MAINTENANCE] Issue: " + issue);

            foreach (string warning in report.Warnings)
                Debug.LogWarning("[DB-MAINTENANCE] Warning: " + warning);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Database scan failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Repair Database")]
    public static void RepairDatabase()
    {
        try
        {
            var result = DatabaseConsistencyService.Repair(true, true);
            Debug.Log("[DB-MAINTENANCE] Database repair: " + result.ToSummary());

            if (result.ClipRepair != null && !string.IsNullOrEmpty(result.ClipRepair.QuarantineRoot))
                Debug.Log("[DB-MAINTENANCE] Clip orphans moved to: " + result.ClipRepair.QuarantineRoot);

            foreach (string failure in result.Failures)
                Debug.LogWarning("[DB-MAINTENANCE] Repair failure: " + failure);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Database repair failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Reset Local Database")]
    public static void ResetLocalDatabase()
    {
        string databasePath = DatabasePaths.DatabasePath;

        if (!EditorUtility.DisplayDialog(
                "Datenbank zurücksetzen",
                "Die lokale Datenbank inklusive Backups wird gelöscht. Danach wird beim nächsten Start eine neue V1-Datenbank angelegt.\n\nFortfahren?",
                "Löschen",
                "Abbrechen"))
            return;

        try
        {
            DatabaseFileSet.Delete(databasePath);

            if (System.IO.Directory.Exists(DatabasePaths.BackupsRoot))
                System.IO.Directory.Delete(DatabasePaths.BackupsRoot, true);

            DatabaseBootstrapper.ResetForTests();
            SaveSystem.ResetLoadFailedForTests();

            Debug.Log("[DB-MAINTENANCE] Local database reset: " + databasePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Database reset failed: " + e);
        }
    }
}
