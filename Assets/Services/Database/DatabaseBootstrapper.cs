using System;
using System.IO;
using SQLite;
using UnityEngine;

public static class DatabaseBootstrapper
{
    private static readonly object Gate = new object();
    private static DatabaseBootstrapResult result;

    public static DatabaseBootstrapStatus Status { get; private set; } = DatabaseBootstrapStatus.NotStarted;
    public static bool IsReady => Status == DatabaseBootstrapStatus.Ready;
    public static bool HasFailed => Status == DatabaseBootstrapStatus.Failed;
    public static DatabaseBootstrapResult Result => result;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetBeforeSceneLoad()
    {
        lock (Gate)
        {
            if (Status == DatabaseBootstrapStatus.Running)
                return;

            result = null;
            Status = DatabaseBootstrapStatus.NotStarted;
        }

        SqliteDatabaseService.ResetSchemaEnsured();
        DatabaseRecoveryService.ResetForTests();
        SaveSystem.ResetLoadState();
    }

#if UNITY_EDITOR
    public static void ResetForTests()
    {
        lock (Gate)
        {
            result = null;
            Status = DatabaseBootstrapStatus.NotStarted;
            SqliteDatabaseService.ResetSchemaEnsured();
        }
    }
#endif

    internal static void ResetForRecovery()
    {
        lock (Gate)
        {
            result = null;
            Status = DatabaseBootstrapStatus.NotStarted;
        }

        SqliteDatabaseService.ResetSchemaEnsured();
        SaveSystem.ResetLoadState();
    }

    public static DatabaseBootstrapResult Prepare()
    {
        lock (Gate)
        {
            if (Status == DatabaseBootstrapStatus.Ready || Status == DatabaseBootstrapStatus.Failed)
                return result;

            if (Status == DatabaseBootstrapStatus.Running)
                throw new DatabaseBootstrapException("Database bootstrap is already running.");

            Status = DatabaseBootstrapStatus.Running;

            try
            {
                result = PrepareInternal();
                Status = result.Success ? DatabaseBootstrapStatus.Ready : DatabaseBootstrapStatus.Failed;
            }
            catch (Exception e)
            {
                result = DatabaseBootstrapResult.Failed(e.Message, e);
                Status = DatabaseBootstrapStatus.Failed;
                Debug.LogError("[DatabaseBootstrap] " + e);
            }

            if (result.Success)
                SqliteDatabaseService.MarkSchemaEnsured();

            return result;
        }
    }

    public static void EnsureReady()
    {
        var bootstrapResult = Prepare();
        if (bootstrapResult == null || !bootstrapResult.Success)
        {
            string error = bootstrapResult != null ? bootstrapResult.Error : "Unbekannter Datenbankfehler.";
            throw new DatabaseBootstrapException(error, bootstrapResult != null ? bootstrapResult.Exception : null);
        }
    }

    private static DatabaseBootstrapResult PrepareInternal()
    {
        Directory.CreateDirectory(DatabasePaths.Root);
        Directory.CreateDirectory(DatabasePaths.BackupsRoot);
        Directory.CreateDirectory(DatabasePaths.KnownGoodRoot);
        Directory.CreateDirectory(DatabasePaths.CorruptRoot);

        string databasePath = DatabasePaths.DatabasePath;
        string restoredBackupPath = null;

        if (!File.Exists(databasePath))
        {
            if (DatabaseBackupService.TryRestoreLatestDatabaseBackup(
                    DatabaseSchema.CurrentVersion,
                    out restoredBackupPath,
                    out _))
            {
                Debug.Log("[DatabaseBootstrap] Restored missing database from backup: " + restoredBackupPath);
            }
            else
            {
                return CreateFreshDatabase();
            }
        }
        else if (new FileInfo(databasePath).Length == 0)
        {
            string quarantineDirectory = DatabasePaths.CreateQuarantineDirectory("empty-database");
            DatabaseFileSet.MoveTo(databasePath, Path.Combine(quarantineDirectory, DatabasePaths.DatabaseFileName));
            Debug.LogWarning("[DatabaseBootstrap] Empty database file was quarantined: " + quarantineDirectory);

            if (DatabaseBackupService.TryRestoreLatestDatabaseBackup(
                    DatabaseSchema.CurrentVersion,
                    out restoredBackupPath,
                    out _))
            {
                Debug.Log("[DatabaseBootstrap] Restored empty database from backup: " + restoredBackupPath);
            }
            else
            {
                return CreateFreshDatabase();
            }
        }

        bool restoredFromBackup = !string.IsNullOrEmpty(restoredBackupPath);
        DatabaseInfo info = null;
        string preflightError = null;
        string restoreError = null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (TryReadDatabaseInfo(databasePath, out info, out preflightError))
                break;

            if (!restoredFromBackup &&
                DatabaseBackupService.TryRestoreLatestDatabaseBackup(
                    DatabaseSchema.CurrentVersion,
                    out restoredBackupPath,
                    out restoreError))
            {
                restoredFromBackup = true;
                SqliteDatabaseService.ResetSchemaEnsured();
                Debug.Log("[DatabaseBootstrap] Restored database from backup: " + restoredBackupPath);
                continue;
            }

            string message = "Die Datenbank ist beschädigt und konnte nicht wiederhergestellt werden: " + preflightError;
            if (!string.IsNullOrEmpty(restoreError))
                message += " (" + restoreError + ")";

            return DatabaseBootstrapResult.Failed(message);
        }

        if (info == null)
            return DatabaseBootstrapResult.Failed("Die Datenbank konnte nicht gelesen werden.");

        if (info.ApplicationId != 0 && info.ApplicationId != DatabaseSchema.ApplicationId)
        {
            return DatabaseBootstrapResult.Failed(
                "Die Datenbank gehört zu einer anderen Anwendung (application_id=" + info.ApplicationId + ")."
            );
        }

        if (info.UserVersion > DatabaseSchema.CurrentVersion)
        {
            return DatabaseBootstrapResult.Failed(
                "Die Datenbank wurde mit einer neueren App-Version erstellt (Schema v" +
                info.UserVersion + ", App unterstützt v" + DatabaseSchema.CurrentVersion + ")."
            );
        }

        if (info.UserVersion < 0)
            return DatabaseBootstrapResult.Failed("Ungültige Datenbank-Schemaversion.");

        int fromVersion = info.UserVersion;

        if (fromVersion == DatabaseSchema.CurrentVersion)
        {
            DatabaseBackupService.CreateRollingBackupIfDue(DatabaseSchema.CurrentVersion);

            return new DatabaseBootstrapResult
            {
                Success = true,
                RestoredFromBackup = restoredFromBackup,
                BackupPath = restoredBackupPath,
                FromSchemaVersion = info.UserVersion,
                ToSchemaVersion = info.UserVersion
            };
        }

        if (!DatabaseMigrationRunner.CanUpgrade(fromVersion, DatabaseSchema.CurrentVersion))
        {
            return DatabaseBootstrapResult.Failed(
                "Die vorhandene Datenbank hat Schema v" + fromVersion +
                ". Diese Version unterstützt nur Schema v" + DatabaseSchema.CurrentVersion +
                " und es ist kein Migrationspfad vorhanden."
            );
        }

        DatabaseBackupResult backup;

        try
        {
            backup = DatabaseBackupService.CreateBackup(
                "pre-migration",
                fromVersion,
                true,
                true);
        }
        catch (Exception e)
        {
            return DatabaseBootstrapResult.Failed(
                "Migration abgebrochen: Vor der Schemaänderung konnte kein Backup erstellt werden: " + e.Message,
                e);
        }

        try
        {
            DatabaseMigrationRunner.Migrate(databasePath, fromVersion, DatabaseSchema.CurrentVersion);
        }
        catch (Exception e)
        {
            return DatabaseBootstrapResult.Failed(
                "Migration von Schema v" + fromVersion + " auf v" + DatabaseSchema.CurrentVersion +
                " fehlgeschlagen. Die Datenbank wurde nicht verändert. Backup: " + backup.Path,
                e);
        }

        DatabaseBackupService.CreateRollingBackupIfDue(DatabaseSchema.CurrentVersion);

        return new DatabaseBootstrapResult
        {
            Success = true,
            RestoredFromBackup = restoredFromBackup,
            Migrated = true,
            BackupPath = backup.Path,
            FromSchemaVersion = fromVersion,
            ToSchemaVersion = DatabaseSchema.CurrentVersion
        };
    }

    private static DatabaseBootstrapResult CreateFreshDatabase()
    {
        string stagingDirectory = null;

        try
        {
            stagingDirectory = DatabasePaths.CreateStagingDirectory("new-database");
            string stagedDatabase = Path.Combine(stagingDirectory, DatabasePaths.DatabaseFileName);

            using (var connection = new SQLiteConnection(
                       stagedDatabase,
                       SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
            {
                SqliteDatabaseService.InitializeFreshDatabase(connection);

                if (!DatabaseHealth.TryQuickCheck(connection, out string healthError))
                    throw new InvalidOperationException("Neue Datenbank ist ungültig: " + healthError);

                if (!DatabaseHealth.TryValidateSchema(connection, DatabaseSchema.CurrentVersion, out string schemaError))
                    throw new InvalidOperationException("Neues Schema ist ungültig: " + schemaError);
            }

            string destination = DatabasePaths.DatabasePath;
            if (DatabaseFileSet.Exists(destination))
            {
                string quarantineDirectory = DatabasePaths.CreateQuarantineDirectory("fresh-database");
                DatabaseFileSet.MoveTo(destination, Path.Combine(quarantineDirectory, DatabasePaths.DatabaseFileName));
            }

            DatabaseFileSet.Replace(stagedDatabase, destination);
            DatabaseBackupService.EnsureKnownGoodSnapshot(DatabaseSchema.CurrentVersion);
            DatabaseBackupService.CreateRollingBackupIfDue(DatabaseSchema.CurrentVersion);

            return new DatabaseBootstrapResult
            {
                Success = true,
                CreatedNewDatabase = true,
                FromSchemaVersion = 0,
                ToSchemaVersion = DatabaseSchema.CurrentVersion
            };
        }
        finally
        {
            DatabasePaths.TryDeleteDirectory(stagingDirectory);
        }
    }

    private static bool TryReadDatabaseInfo(
        string databasePath,
        out DatabaseInfo info,
        out string error)
    {
        info = null;
        error = null;

        try
        {
            using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly))
            {
                if (!DatabaseHealth.TryQuickCheck(connection, out error))
                    return false;

                int userVersion = DatabaseHealth.GetUserVersion(connection);
                int applicationId = DatabaseHealth.GetApplicationId(connection);

                if (applicationId != 0 && applicationId != DatabaseSchema.ApplicationId)
                {
                    info = new DatabaseInfo
                    {
                        UserVersion = userVersion,
                        ApplicationId = applicationId
                    };
                    return true;
                }

                if (userVersion == DatabaseSchema.CurrentVersion &&
                    !DatabaseHealth.TryValidateSchema(connection, DatabaseSchema.CurrentVersion, out string schemaError))
                {
                    error = "Schema ungültig: " + schemaError;
                    return false;
                }

                info = new DatabaseInfo
                {
                    UserVersion = userVersion,
                    ApplicationId = applicationId
                };

                return true;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private sealed class DatabaseInfo
    {
        public int UserVersion;
        public int ApplicationId;
    }
}
