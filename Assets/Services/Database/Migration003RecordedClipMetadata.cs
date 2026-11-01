using SQLite;
using UnityEngine;

public sealed class Migration003RecordedClipMetadata : IDatabaseMigration
{
    public int Version => 3;
    public string Name => "recorded_clip_metadata";
    public string Checksum => "v3-recorded-clip-metadata";

    public void Apply(SQLiteConnection connection)
    {
        if (!DatabaseHealth.ColumnExists(connection, "recorded_clips", "file_size"))
            connection.Execute("ALTER TABLE recorded_clips ADD COLUMN file_size INTEGER;");

        if (!DatabaseHealth.ColumnExists(connection, "recorded_clips", "sha256"))
            connection.Execute("ALTER TABLE recorded_clips ADD COLUMN sha256 TEXT;");

        if (!DatabaseHealth.ColumnExists(connection, "recorded_clips", "created_at_utc"))
            connection.Execute("ALTER TABLE recorded_clips ADD COLUMN created_at_utc TEXT;");

        var rows = connection.Query<ClipRow>(
            "SELECT id AS Id, category AS Category, file_name AS FileName FROM recorded_clips;");

        int updated = 0;
        foreach (var row in rows)
        {
            if (!RecordedClipFileStore.TryResolvePath(row.Category, row.FileName, out string fullPath))
                continue;

            if (!RecordedClipFileStore.TryReadMetadata(fullPath, out long fileSize, out string sha256, out string createdAtUtc))
                continue;

            connection.Execute(
                "UPDATE recorded_clips SET file_size = ?, sha256 = ?, created_at_utc = ? WHERE id = ?;",
                fileSize,
                sha256,
                createdAtUtc,
                row.Id);

            updated++;
        }

        Debug.Log("[DatabaseMigration] Backfilled clip metadata for " + updated + " rows.");
    }

    private sealed class ClipRow
    {
        public long Id { get; set; }
        public string Category { get; set; }
        public string FileName { get; set; }
    }
}
