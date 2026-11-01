using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

public sealed class DatabaseConsistencyReport
{
    public List<string> Issues = new List<string>();
    public List<string> Warnings = new List<string>();
    public RecordedClipIntegrityReport ClipReport;

    public bool IsHealthy => Issues.Count == 0;

    public string ToSummary()
    {
        return "Issues=" + Issues.Count +
               ", Warnings=" + Warnings.Count +
               ", Clips=[" + (ClipReport != null ? ClipReport.ToSummary() : "") + "]";
    }
}

public sealed class DatabaseConsistencyRepairResult
{
    public DatabaseConsistencyReport Before;
    public DatabaseConsistencyReport After;
    public RecordedClipRepairResult ClipRepair;
    public int RebuiltGamePlayers;
    public List<string> Failures = new List<string>();

    public string ToSummary()
    {
        return "RebuiltGamePlayers=" + RebuiltGamePlayers +
               ", Failures=" + Failures.Count +
               ", Before=[" + (Before != null ? Before.ToSummary() : "") + "]" +
               ", After=[" + (After != null ? After.ToSummary() : "") + "]";
    }
}

public static class DatabaseConsistencyService
{
    public static DatabaseConsistencyReport Scan(bool verifyClipHashes = false)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return Scan(connection, verifyClipHashes);
        }
    }

    internal static DatabaseConsistencyReport Scan(SQLiteConnection connection, bool verifyClipHashes)
    {
        var report = new DatabaseConsistencyReport
        {
            ClipReport = RecordedClipIntegrityService.Scan(connection, verifyClipHashes)
        };

        AddCountIssue(connection, report, "Sets ohne Spiel", "SELECT COUNT(*) FROM sets WHERE game_id NOT IN (SELECT id FROM games);");
        AddCountIssue(connection, report, "Legs ohne Spiel/Set", "SELECT COUNT(*) FROM legs WHERE game_id NOT IN (SELECT id FROM games) OR set_id NOT IN (SELECT id FROM sets);");
        AddCountIssue(connection, report, "Turns ohne Spiel/Leg", "SELECT COUNT(*) FROM turns WHERE game_id NOT IN (SELECT id FROM games) OR leg_id NOT IN (SELECT id FROM legs);");
        AddCountIssue(connection, report, "Throws ohne Spiel/Turn", "SELECT COUNT(*) FROM throws WHERE game_id NOT IN (SELECT id FROM games) OR turn_id NOT IN (SELECT id FROM turns);");
        AddCountIssue(connection, report, "GamePlayers ohne Spiel/Spieler", "SELECT COUNT(*) FROM game_players WHERE game_id NOT IN (SELECT id FROM games) OR player_id NOT IN (SELECT id FROM players);");
        AddCountIssue(connection, report, "Stats ohne Spiel/Spieler", "SELECT COUNT(*) FROM player_game_stats WHERE game_id NOT IN (SELECT id FROM games) OR player_id NOT IN (SELECT id FROM players);");
        AddCountIssue(connection, report, "Aggregatstats ohne Spieler", "SELECT (SELECT COUNT(*) FROM player_stats_all WHERE player_id NOT IN (SELECT id FROM players)) + (SELECT COUNT(*) FROM player_stats_x01 WHERE player_id NOT IN (SELECT id FROM players)) + (SELECT COUNT(*) FROM player_stats_cricket WHERE player_id NOT IN (SELECT id FROM players)) + (SELECT COUNT(*) FROM player_stats_atc WHERE player_id NOT IN (SELECT id FROM players));");
        AddCountIssue(connection, report, "Doppelte GamePlayer-Reihenfolge", "SELECT COUNT(*) FROM (SELECT game_id, player_order FROM game_players GROUP BY game_id, player_order HAVING COUNT(*) > 1);");
        AddCountIssue(connection, report, "Stats-Typ passt nicht zum Spielmodus", "SELECT COUNT(*) FROM player_game_stats s JOIN games g ON g.id = s.game_id WHERE lower(s.stat_type) <> lower(g.game_mode);");
        AddCountIssue(connection, report, "Doppelte Set-Reihenfolge", "SELECT COUNT(*) FROM (SELECT game_id, order_index FROM sets GROUP BY game_id, order_index HAVING COUNT(*) > 1);");
        AddCountIssue(connection, report, "Doppelte Leg-Reihenfolge", "SELECT COUNT(*) FROM (SELECT set_id, order_index FROM legs GROUP BY set_id, order_index HAVING COUNT(*) > 1);");
        AddCountIssue(connection, report, "Doppelte Turn-Reihenfolge", "SELECT COUNT(*) FROM (SELECT leg_id, order_index FROM turns GROUP BY leg_id, order_index HAVING COUNT(*) > 1);");
        AddCountIssue(connection, report, "Doppelte Throw-Reihenfolge", "SELECT COUNT(*) FROM (SELECT turn_id, order_index FROM throws GROUP BY turn_id, order_index HAVING COUNT(*) > 1);");

        AddClipIssues(report);

        if (DatabaseHealth.ColumnExists(connection, "games", "player_ids"))
            CheckLegacyPlayerIds(connection, report);

        string schemaVersion = DatabaseMigrationRunner.GetMeta(connection, DatabaseSchema.VersionMetaKey);
        if (schemaVersion != DatabaseSchema.CurrentVersion.ToString())
            report.Warnings.Add("app_meta schema_version=" + (schemaVersion ?? "<null>") + " erwartet " + DatabaseSchema.CurrentVersion + ".");

        string statsVersion = DatabaseMigrationRunner.GetMeta(connection, DatabaseSchema.StatsVersionMetaKey);
        if (statsVersion != DatabaseSchema.CurrentStatsVersion.ToString())
            report.Warnings.Add("app_meta stats_schema_version=" + (statsVersion ?? "<null>") + " erwartet " + DatabaseSchema.CurrentStatsVersion + ".");

        return report;
    }

    public static DatabaseConsistencyRepairResult Repair(bool quarantineOrphans = true, bool verifyClipHashes = true)
    {
        SqliteDatabaseService.EnsureSchema();

        var result = new DatabaseConsistencyRepairResult
        {
            Before = Scan(verifyClipHashes),
            ClipRepair = RecordedClipIntegrityService.Repair(quarantineOrphans, verifyClipHashes)
        };

        try
        {
            result.RebuiltGamePlayers = RebuildGamePlayersFromPlayerIds();
        }
        catch (Exception e)
        {
            result.Failures.Add("GamePlayers-Rebuild fehlgeschlagen: " + e.Message);
        }

        result.After = Scan(false);
        return result;
    }

    public static int RebuildGamePlayersFromPlayerIds()
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            if (!DatabaseHealth.ColumnExists(connection, "games", "player_ids"))
                return 0;

            var players = new HashSet<string>(
                connection.Query<IdRow>("SELECT id AS Id FROM players;").Select(row => row.Id),
                StringComparer.Ordinal);

            var games = connection.Query<GamePlayerIdsRow>("SELECT id AS Id, player_ids AS PlayerIds FROM games;");
            var gamePlayers = connection.Query<GamePlayerRow>("SELECT game_id AS GameId, player_id AS PlayerId, player_order AS PlayerOrder FROM game_players;")
                .GroupBy(row => row.GameId)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.PlayerOrder).ToList(), StringComparer.Ordinal);

            int inserted = 0;
            connection.BeginTransaction();

            try
            {
                connection.Execute("DELETE FROM game_players;");

                foreach (var game in games)
                {
                    var ids = ParsePlayerIds(game.PlayerIds);
                    if (ids.Count == 0 && gamePlayers.TryGetValue(game.Id, out var existing))
                        ids = existing.Select(row => row.PlayerId).ToList();

                    ids = ids.Distinct(StringComparer.Ordinal).ToList();

                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (!players.Contains(ids[i]))
                            throw new InvalidOperationException("Spiel " + game.Id + " verweist auf fehlenden Spieler " + ids[i] + ".");

                        connection.Execute(
                            "INSERT INTO game_players (game_id, player_id, player_order) VALUES (?, ?, ?);",
                            game.Id,
                            ids[i],
                            i);

                        inserted++;
                    }
                }

                connection.Commit();
                return inserted;
            }
            catch
            {
                connection.Rollback();
                throw;
            }
        }
    }

    private static void CheckLegacyPlayerIds(SQLiteConnection connection, DatabaseConsistencyReport report)
    {
        var games = connection.Query<GamePlayerIdsRow>("SELECT id AS Id, player_ids AS PlayerIds FROM games;");
        var gamePlayers = connection.Query<GamePlayerRow>("SELECT game_id AS GameId, player_id AS PlayerId, player_order AS PlayerOrder FROM game_players;")
            .GroupBy(row => row.GameId)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.PlayerOrder).Select(row => row.PlayerId).ToList(), StringComparer.Ordinal);

        foreach (var game in games)
        {
            var expected = ParsePlayerIds(game.PlayerIds);
            gamePlayers.TryGetValue(game.Id, out var actual);
            actual ??= new List<string>();

            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                report.Issues.Add("Spiel " + game.Id + ": player_ids passt nicht zu game_players.");
        }
    }

    private static void AddCountIssue(SQLiteConnection connection, DatabaseConsistencyReport report, string label, string sql)
    {
        int count = connection.ExecuteScalar<int>(sql);
        if (count > 0)
            report.Issues.Add(label + ": " + count);
    }

    private static void AddClipIssues(DatabaseConsistencyReport report)
    {
        var clips = report.ClipReport;
        if (clips == null)
            return;

        if (clips.InvalidRows.Count > 0)
            report.Issues.Add("Ungültige Clip-Zeilen: " + clips.InvalidRows.Count);
        if (clips.MetadataRows.Count > 0)
            report.Issues.Add("Clip-Metadaten veraltet: " + clips.MetadataRows.Count);
        if (clips.DuplicateSlotRows.Count > 0)
            report.Issues.Add("Doppelte Clip-Slots: " + clips.DuplicateSlotRows.Count);
        if (clips.OrphanFiles.Count > 0)
            report.Issues.Add("Verwaiste Clip-Dateien: " + clips.OrphanFiles.Count);
    }

    private static List<string> ParsePlayerIds(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
    }

    private sealed class IdRow
    {
        public string Id { get; set; }
    }

    private sealed class GamePlayerIdsRow
    {
        public string Id { get; set; }
        public string PlayerIds { get; set; }
    }

    private sealed class GamePlayerRow
    {
        public string GameId { get; set; }
        public string PlayerId { get; set; }
        public int PlayerOrder { get; set; }
    }
}
