using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using UnityEngine;

public interface IDatabaseMigration
{
    int Version { get; }
    string Name { get; }
    string Checksum { get; }
    void Apply(SQLiteConnection connection);
}

/// <summary>
/// Registry for future schema changes. Schema 1 is the current baseline and
/// therefore has no registered patches. When the schema changes, increment
/// <see cref="DatabaseSchema.CurrentVersion"/> and add the next migration here.
/// </summary>
public static class DatabaseMigrations
{
    public static readonly IDatabaseMigration[] All = new IDatabaseMigration[0];
}

public static class DatabaseMigrationRunner
{
    public static bool CanUpgrade(int fromVersion, int toVersion)
    {
        if (fromVersion == toVersion)
            return true;

        if (fromVersion < 0 || fromVersion > toVersion)
            return false;

        int version = fromVersion;
        while (version < toVersion)
        {
            int nextVersion = version + 1;
            if (!DatabaseMigrations.All.Any(migration => migration != null && migration.Version == nextVersion))
                return false;

            version = nextVersion;
        }

        return true;
    }

    public static void Migrate(string databasePath, int fromVersion, int toVersion)
    {
        if (string.IsNullOrEmpty(databasePath))
            throw new ArgumentException("databasePath is required", nameof(databasePath));

        if (fromVersion == toVersion)
            return;

        if (fromVersion > toVersion)
            throw new InvalidOperationException("Downgrades are not supported.");

        if (!CanUpgrade(fromVersion, toVersion))
            throw new InvalidOperationException("No migration path found for " + fromVersion + " -> " + toVersion + ".");

        var migrations = DatabaseMigrations.All
            .Where(migration => migration != null && migration.Version > fromVersion && migration.Version <= toVersion)
            .OrderBy(migration => migration.Version)
            .ToArray();

        using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite))
        {
            bool transactionStarted = false;

            try
            {
                connection.Execute("PRAGMA foreign_keys = OFF;");
                connection.BeginTransaction();
                transactionStarted = true;

                foreach (var migration in migrations)
                {
                    migration.Apply(connection);

                    connection.Execute(
                        "INSERT OR REPLACE INTO schema_migrations (version, name, applied_at_utc, app_version, checksum) VALUES (?, ?, ?, ?, ?);",
                        migration.Version,
                        migration.Name,
                        DateTime.UtcNow.ToString("o"),
                        Application.version,
                        migration.Checksum
                    );
                }

                DatabaseMeta.Set(connection, DatabaseSchema.VersionMetaKey, toVersion.ToString());
                DatabaseMeta.Set(connection, DatabaseSchema.LastMigrationMetaKey, DateTime.UtcNow.ToString("o"));

                connection.Execute("PRAGMA application_id = " + DatabaseSchema.ApplicationId + ";");
                connection.Execute("PRAGMA user_version = " + toVersion + ";");

                int foreignKeyViolations = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_foreign_key_check;");
                if (foreignKeyViolations > 0)
                    throw new InvalidOperationException("Migration would leave " + foreignKeyViolations + " foreign key violations.");

                connection.Commit();
                transactionStarted = false;
                connection.Execute("PRAGMA foreign_keys = ON;");

                if (!DatabaseHealth.TryQuickCheck(connection, out string healthError))
                    throw new InvalidOperationException("Migration integrity check failed: " + healthError);

                if (!DatabaseHealth.TryValidateSchema(connection, toVersion, out string schemaError))
                    throw new InvalidOperationException("Migration schema check failed: " + schemaError);

                Debug.Log("[DatabaseMigration] Migrated database " + fromVersion + " -> " + toVersion + ".");
            }
            catch
            {
                if (transactionStarted)
                {
                    try
                    {
                        connection.Rollback();
                    }
                    catch (Exception rollbackException)
                    {
                        Debug.LogError("[DatabaseMigration] Rollback failed: " + rollbackException);
                    }
                }

                throw;
            }
        }
    }
}
