using System;
using System.IO;
using SQLite;

public static class DatabaseHealth
{
    public static bool TryQuickCheck(string databasePath, out string error)
    {
        error = null;

        if (string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
        {
            error = "Die Datenbankdatei fehlt.";
            return false;
        }

        try
        {
            using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly))
            {
                return TryQuickCheck(connection, out error);
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    public static bool TryQuickCheck(SQLiteConnection connection, out string error)
    {
        return TryPragmaCheck(connection, "PRAGMA quick_check;", out error);
    }

    public static bool TryFullCheck(string databasePath, out string error)
    {
        error = null;

        if (string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
        {
            error = "Die Datenbankdatei fehlt.";
            return false;
        }

        try
        {
            using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly))
            {
                return TryPragmaCheck(connection, "PRAGMA integrity_check;", out error);
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    public static bool TryFullCheck(SQLiteConnection connection, out string error)
    {
        return TryPragmaCheck(connection, "PRAGMA integrity_check;", out error);
    }

    public static bool TryValidateSchema(SQLiteConnection connection, int expectedSchemaVersion, out string error)
    {
        error = null;

        if (connection == null)
        {
            error = "Keine Datenbankverbindung.";
            return false;
        }

        try
        {
            foreach (string table in DatabaseSchema.RequiredTables)
            {
                if (!TableExists(connection, table))
                {
                    error = "Pflicht-Tabelle fehlt: " + table;
                    return false;
                }
            }

            foreach (string entry in DatabaseSchema.RequiredColumns)
            {
                int separator = entry.IndexOf('.');
                if (separator <= 0 || separator >= entry.Length - 1)
                    continue;

                string table = entry.Substring(0, separator);
                string column = entry.Substring(separator + 1);

                if (!ColumnExists(connection, table, column))
                {
                    error = "Pflicht-Spalte fehlt: " + entry;
                    return false;
                }
            }

            string schemaVersion = DatabaseMeta.Get(connection, DatabaseSchema.VersionMetaKey);
            if (!string.Equals(schemaVersion, expectedSchemaVersion.ToString(), StringComparison.Ordinal))
            {
                error = "app_meta schema_version=" + (schemaVersion ?? "<null>") +
                        ", erwartet " + expectedSchemaVersion + ".";
                return false;
            }

            string statsVersion = DatabaseMeta.Get(connection, DatabaseSchema.StatsVersionMetaKey);
            if (!string.Equals(statsVersion, DatabaseSchema.CurrentStatsVersion.ToString(), StringComparison.Ordinal))
            {
                error = "app_meta stats_schema_version=" + (statsVersion ?? "<null>") +
                        ", erwartet " + DatabaseSchema.CurrentStatsVersion + ".";
                return false;
            }

            int foreignKeyViolations = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_foreign_key_check;");
            if (foreignKeyViolations > 0)
            {
                error = "Foreign-Key-Verletzungen: " + foreignKeyViolations;
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private static bool TryPragmaCheck(SQLiteConnection connection, string pragma, out string error)
    {
        error = null;

        if (connection == null)
        {
            error = "Keine Datenbankverbindung.";
            return false;
        }

        try
        {
            string result = connection.ExecuteScalar<string>(pragma);
            if (string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                return true;

            error = string.IsNullOrWhiteSpace(result) ? "Unbekannter Integritätsfehler." : result;
            return false;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    public static int GetUserVersion(SQLiteConnection connection)
    {
        return connection.ExecuteScalar<int>("PRAGMA user_version;");
    }

    public static int GetApplicationId(SQLiteConnection connection)
    {
        return connection.ExecuteScalar<int>("PRAGMA application_id;");
    }

    public static bool TableExists(SQLiteConnection connection, string tableName)
    {
        if (connection == null || string.IsNullOrEmpty(tableName))
            return false;

        int count = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?;",
            tableName
        );

        return count > 0;
    }

    public static bool ColumnExists(SQLiteConnection connection, string tableName, string columnName)
    {
        if (connection == null || string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(columnName))
            return false;

        var columns = connection.Query<ColumnInfo>("PRAGMA table_info(" + tableName + ");");
        if (columns == null)
            return false;

        foreach (var column in columns)
        {
            if (string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private sealed class ColumnInfo
    {
        public string Name { get; set; }
    }
}
