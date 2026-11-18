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

public sealed class Migration001InitialSchema : IDatabaseMigration
{
    public int Version => 1;
    public string Name => "initial_schema";
    public string Checksum => "v1-initial-schema";

    public void Apply(SQLiteConnection connection)
    {
        SqliteDatabaseService.CreateSchemaBaseline(connection);
        DatabaseMigrationRunner.SetMeta(connection, DatabaseSchema.StatsVersionMetaKey, DatabaseSchema.CurrentStatsVersion.ToString());
    }
}

public static class DatabaseMigrations
{
    public static readonly IDatabaseMigration[] All =
    {
        new Migration001InitialSchema()
    };
}

public static class DatabaseMigrationRunner
{
    public static void Migrate(string databasePath, int fromVersion, int toVersion)
    {
        if (string.IsNullOrEmpty(databasePath))
            throw new ArgumentException("databasePath is required", nameof(databasePath));

        if (fromVersion == toVersion)
            return;

        if (fromVersion > toVersion)
            throw new InvalidOperationException("Downgrades are not supported.");

        var migrations = DatabaseMigrations.All
            .Where(migration => migration.Version > fromVersion && migration.Version <= toVersion)
            .OrderBy(migration => migration.Version)
            .ToArray();

        if (migrations.Length == 0)
            throw new InvalidOperationException("No migration found for " + fromVersion + " -> " + toVersion + ".");

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

                SetMeta(connection, DatabaseSchema.VersionMetaKey, toVersion.ToString());
                SetMeta(connection, DatabaseSchema.LastMigrationMetaKey, DateTime.UtcNow.ToString("o"));

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

    public static void SetMeta(SQLiteConnection connection, string key, string value)
    {
        if (connection == null || string.IsNullOrEmpty(key))
            return;

        connection.Execute(
            "INSERT OR REPLACE INTO app_meta (key_name, value_text) VALUES (?, ?);",
            key,
            value ?? string.Empty
        );
    }

    public static string GetMeta(SQLiteConnection connection, string key)
    {
        if (connection == null || string.IsNullOrEmpty(key))
            return null;

        var rows = connection.Query<MetaRow>(
            "SELECT value_text AS ValueText FROM app_meta WHERE key_name = ?;",
            key
        );

        return rows.Count > 0 ? rows[0].ValueText : null;
    }

    private sealed class MetaRow
    {
        public string ValueText { get; set; }
    }
}
