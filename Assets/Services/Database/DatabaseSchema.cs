public static class DatabaseSchema
{
    public const int ApplicationId = 0x44415254;
    public const int CurrentVersion = 2;
    public const int CurrentStatsVersion = 1;

    public const string VersionMetaKey = "schema_version";
    public const string StatsVersionMetaKey = "stats_schema_version";
    public const string LastMigrationMetaKey = "last_migration";
    public const string LastBackupMetaKey = "last_successful_backup";
}
