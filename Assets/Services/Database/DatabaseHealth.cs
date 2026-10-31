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
        error = null;

        if (connection == null)
        {
            error = "Keine Datenbankverbindung.";
            return false;
        }

        try
        {
            string result = connection.ExecuteScalar<string>("PRAGMA quick_check;");
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
