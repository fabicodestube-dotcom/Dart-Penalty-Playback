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

        string databasePath = DatabasePaths.DatabasePath;

        if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);

            return CreateFreshDatabase();
        }

        bool restoredFromBackup = false;
        DatabaseInfo info = null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (TryReadDatabaseInfo(databasePath, out info, out string preflightError))
                break;

            if (!restoredFromBackup &&
                DatabaseBackupService.TryRestoreLatestDatabaseBackup(
                    DatabaseSchema.CurrentVersion,
                    out string restoredBackup,
                    out string restoreError))
            {
                restoredFromBackup = true;
                SqliteDatabaseService.ResetSchemaEnsured();
                Debug.Log("[DatabaseBootstrap] Restored database from backup: " + restoredBackup);
                continue;
            }

            return DatabaseBootstrapResult.Failed(
                "Die Datenbank ist beschädigt und konnte nicht wiederhergestellt werden: " + preflightError
            );
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

        if (fromVersion < DatabaseSchema.CurrentVersion)
        {
            var backup = DatabaseBackupService.CreateBackup("pre-migration", fromVersion, true);

            try
            {
                DatabaseMigrationRunner.Migrate(databasePath, fromVersion, DatabaseSchema.CurrentVersion);
            }
            catch (Exception e)
            {
                return DatabaseBootstrapResult.Failed(
                    "Migration von Schema v" + fromVersion + " auf v" + DatabaseSchema.CurrentVersion +
                    " fehlgeschlagen. Die Datenbank wurde nicht verändert. Backup: " + backup.Path,
                    e
                );
            }

            DatabaseBackupService.CreateRollingBackupIfDue(DatabaseSchema.CurrentVersion);

            return new DatabaseBootstrapResult
            {
                Success = true,
                RestoredFromBackup = restoredFromBackup,
                Migrated = true,
                FromSchemaVersion = fromVersion,
                ToSchemaVersion = DatabaseSchema.CurrentVersion,
                BackupPath = backup.Path
            };
        }

        if (info.UserVersion == DatabaseSchema.CurrentVersion)
        {
            DatabaseBackupService.CreateRollingBackupIfDue(DatabaseSchema.CurrentVersion);

            return new DatabaseBootstrapResult
            {
                Success = true,
                RestoredFromBackup = restoredFromBackup,
                FromSchemaVersion = info.UserVersion,
                ToSchemaVersion = info.UserVersion
            };
        }

        return DatabaseBootstrapResult.Failed("Unbekannter Datenbankzustand.");
    }

    private static DatabaseBootstrapResult CreateFreshDatabase()
    {
        string stagingDirectory = null;

        try
        {
            stagingDirectory = DatabasePaths.CreateStagingDirectory("new-database");
            string stagedDatabase = Path.Combine(stagingDirectory, DatabasePaths.DatabaseFileName);

            File.WriteAllBytes(stagedDatabase, Array.Empty<byte>());
            DatabaseMigrationRunner.Migrate(stagedDatabase, 0, DatabaseSchema.CurrentVersion);

            if (!DatabaseHealth.TryQuickCheck(stagedDatabase, out string healthError))
                throw new InvalidOperationException("Neue Datenbank ist ungültig: " + healthError);

            string destination = DatabasePaths.DatabasePath;
            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(stagedDatabase, destination);
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

                info = new DatabaseInfo
                {
                    UserVersion = DatabaseHealth.GetUserVersion(connection),
                    ApplicationId = DatabaseHealth.GetApplicationId(connection)
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
