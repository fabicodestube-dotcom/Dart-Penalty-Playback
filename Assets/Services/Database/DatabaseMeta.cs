using SQLite;

public static class DatabaseMeta
{
    public static void Set(SQLiteConnection connection, string key, string value)
    {
        if (connection == null || string.IsNullOrEmpty(key))
            return;

        connection.Execute(
            "INSERT OR REPLACE INTO app_meta (key_name, value_text) VALUES (?, ?);",
            key,
            value ?? string.Empty
        );
    }

    public static string Get(SQLiteConnection connection, string key)
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
