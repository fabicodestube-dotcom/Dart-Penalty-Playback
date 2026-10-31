using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerStatsBackfillService
{
    public static void EnsureCurrent()
    {
        if (!TryEnsureCurrent(out string error))
            throw new DatabaseBootstrapException(error);
    }

    public static bool TryEnsureCurrent(out string error)
    {
        error = null;

        try
        {
            int currentVersion = DatabaseSchema.CurrentStatsVersion;
            List<Guid> outdatedGameIds = SqliteDatabaseRepository.LoadGameIdsWithOutdatedPlayerGameStats(currentVersion);

            if (outdatedGameIds.Count == 0)
            {
                string storedVersion = SqliteDatabaseRepository.LoadPlayerStatsVersion();
                if (storedVersion != currentVersion.ToString())
                    SqliteDatabaseRepository.MarkPlayerStatsVersion(currentVersion);

                return true;
            }

            Debug.Log("[StatsBackfill] Updating " + outdatedGameIds.Count + " games to stats schema v" + currentVersion + ".");

            foreach (Guid gameId in outdatedGameIds)
            {
                Game game = SqliteDatabaseRepository.LoadGameById(gameId);
                if (game == null)
                    throw new InvalidOperationException("Spiel " + gameId + " konnte für den Statistik-Backfill nicht geladen werden.");

                SqliteDatabaseRepository.SavePlayerGameStatsForGame(game);
            }

            SqliteDatabaseRepository.RebuildAggregatedPlayerStats();
            SqliteDatabaseRepository.MarkPlayerStatsVersion(currentVersion);

            Debug.Log("[StatsBackfill] Stats backfill completed.");
            return true;
        }
        catch (Exception e)
        {
            error = "Statistik-Backfill fehlgeschlagen: " + e.Message;
            Debug.LogError("[StatsBackfill] " + e);
            return false;
        }
    }
}
