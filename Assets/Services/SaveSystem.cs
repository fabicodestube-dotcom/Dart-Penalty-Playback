using System;
using UnityEngine;

public static class SaveSystem
{
    private static bool loadFailed;

    public static bool HasLoadFailed => loadFailed;

    public static void MarkLoadFailed(string reason)
    {
        loadFailed = true;
        Debug.LogError("SQLite-Datenbank gesperrt: " + reason);
    }

    internal static void ResetLoadState()
    {
        loadFailed = false;
    }

#if UNITY_EDITOR
    public static void ResetLoadFailedForTests()
    {
        ResetLoadState();
    }
#endif

    public static bool SaveDatabase(Database db)
    {
        if (db == null || IsWriteBlocked()) return false;

        return SqliteDatabaseRepository.SaveDatabase(db);
    }

    public static bool SaveGame(Game game)
    {
        if (game == null || IsWriteBlocked()) return false;

        return SqliteDatabaseRepository.SaveGame(game);
    }

    public static bool SaveAggregatedPlayerStats(Game game)
    {
        if (game == null || IsWriteBlocked()) return false;

        try
        {
            SqliteDatabaseRepository.SaveAggregatedPlayerStats(game);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Speichern der Spieler-Aggregate fehlgeschlagen: " + e);
            return false;
        }
    }

    public static bool RebuildAggregatedPlayerStats()
    {
        if (IsWriteBlocked()) return false;

        try
        {
            SqliteDatabaseRepository.RebuildAggregatedPlayerStats();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Neuaufbau der Spieler-Aggregate fehlgeschlagen: " + e);
            return false;
        }
    }

    public static bool DeleteGame(Guid id)
    {
        if (IsWriteBlocked()) return false;

        return SqliteDatabaseRepository.DeleteGame(id);
    }

    public static bool DeleteGamesByMode(GameMode mode)
    {
        if (IsWriteBlocked()) return false;

        return SqliteDatabaseRepository.DeleteGamesByMode(mode);
    }

    public static bool DeleteAllGames()
    {
        if (IsWriteBlocked()) return false;

        return SqliteDatabaseRepository.DeleteAllGames();
    }

    public static Game LoadGameById(Guid id)
    {
        DatabaseBootstrapper.EnsureReady();
        return SqliteDatabaseRepository.LoadGameById(id);
    }

    public static Database LoadDatabase()
    {
        try
        {
            DatabaseBootstrapper.EnsureReady();
            PlayerStatsBackfillService.EnsureCurrent();

            Database db = SqliteDatabaseRepository.LoadDatabase();
            SqliteDatabaseRepository.RunAggregatedStatsMigrationIfNeeded();
            db.InitializeAfterLoad();
            db.RebuildAllPlayerStatistics();
            loadFailed = false;
            return db;
        }
        catch (Exception e)
        {
            loadFailed = true;
            Debug.LogError("Laden der SQLite-Datenbank fehlgeschlagen: " + e);
            throw;
        }
    }

    private static bool IsWriteBlocked()
    {
        if (!loadFailed)
            return false;

        Debug.LogError("Speichern blockiert: Die SQLite-Datenbank konnte beim Start nicht geladen werden.");
        return true;
    }
}
