public static class DatabaseSchema
{
    public const int ApplicationId = 0x44415254;
    public const int CurrentVersion = 1;
    public const int CurrentStatsVersion = 1;

    public const string VersionMetaKey = "schema_version";
    public const string StatsVersionMetaKey = "stats_schema_version";
    public const string LastMigrationMetaKey = "last_migration";
    public const string LastBackupMetaKey = "last_successful_backup";

    public static readonly string[] RequiredTables =
    {
        "schema_migrations",
        "app_meta",
        "players",
        "bot_state",
        "games",
        "game_players",
        "game_runtime_state",
        "sets",
        "legs",
        "turns",
        "throws",
        "settings",
        "player_game_stats",
        "player_stats_snapshots",
        "custom_penalties",
        "custom_hit_sectors",
        "audio_events",
        "recorded_clips"
    };

    public static readonly string[] RequiredColumns =
    {
        "players.id",
        "players.name",
        "players.type_name",
        "players.deleted",
        "players.show_in_statistics",
        "players.setup_start_position",
        "players.created_at",
        "bot_state.player_id",
        "bot_state.skill",
        "games.id",
        "games.game_mode",
        "games.created_at",
        "games.last_activity_at",
        "games.settings_json",
        "game_players.game_id",
        "game_players.player_id",
        "game_players.player_order",
        "game_runtime_state.game_id",
        "game_runtime_state.custom_hit_sectors_snapshot_json",
        "sets.id",
        "sets.game_id",
        "sets.order_index",
        "legs.id",
        "legs.game_id",
        "legs.set_id",
        "legs.order_index",
        "turns.id",
        "turns.game_id",
        "turns.leg_id",
        "turns.order_index",
        "turns.player_id",
        "throws.id",
        "throws.game_id",
        "throws.turn_id",
        "throws.order_index",
        "throws.hit_type",
        "player_game_stats.game_id",
        "player_game_stats.player_id",
        "player_game_stats.stat_type",
        "player_game_stats.stat_data",
        "player_game_stats.stats_schema_version",
        "player_stats_snapshots.player_id",
        "player_stats_snapshots.mode",
        "player_stats_snapshots.range_type",
        "player_stats_snapshots.period_key",
        "player_stats_snapshots.stat_data",
        "player_stats_snapshots.stats_schema_version",
        "recorded_clips.id",
        "recorded_clips.category",
        "recorded_clips.file_name",
        "recorded_clips.slot_index",
        "recorded_clips.file_size",
        "recorded_clips.sha256",
        "recorded_clips.created_at_utc"
    };
}
