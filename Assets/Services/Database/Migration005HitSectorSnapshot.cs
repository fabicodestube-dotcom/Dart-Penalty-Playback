using SQLite;

public sealed class Migration005HitSectorSnapshot : IDatabaseMigration
{
    public int Version => 5;
    public string Name => "hit_sector_snapshot";
    public string Checksum => "v5-hit-sector-snapshot";

    public void Apply(SQLiteConnection connection)
    {
        if (!DatabaseHealth.ColumnExists(connection, "game_runtime_state", "custom_hit_sectors_snapshot_json"))
        {
            connection.Execute(
                "ALTER TABLE game_runtime_state ADD COLUMN custom_hit_sectors_snapshot_json TEXT;");
        }
    }
}
