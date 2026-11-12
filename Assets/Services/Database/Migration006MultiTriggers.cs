using SQLite;

public sealed class Migration006MultiTriggers : IDatabaseMigration
{
    public int Version => 6;
    public string Name => "multi_triggers";
    public string Checksum => "v6-multi-triggers";

    public void Apply(SQLiteConnection connection)
    {
        // condition_json and params_json will contain JSON arrays in the future.
        // Defensively convert existing single objects (old DBs) into arrays.
        connection.Execute("UPDATE custom_penalties SET condition_json = '[' || condition_json || ']' WHERE condition_json NOT LIKE '[%';");
        connection.Execute("UPDATE audio_events SET params_json = '[' || params_json || ']' WHERE params_json NOT LIKE '[%';");
    }
}
