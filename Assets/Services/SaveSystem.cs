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

    public static void SaveDatabase(Database db)
    {
        if (db == null || IsWriteBlocked()) return;

        SqliteDatabaseRepository.SaveDatabase(db);
    }

    public static void SaveGame(Game game)
    {
        if (game == null || IsWriteBlocked()) return;

        SqliteDatabaseRepository.SaveGame(game);
    }

    public static void SaveAggregatedPlayerStats(Game game)
    {
        if (game == null || IsWriteBlocked()) return;

        SqliteDatabaseRepository.SaveAggregatedPlayerStats(game);
    }

    public static void RebuildAggregatedPlayerStats()
    {
        if (IsWriteBlocked()) return;

        SqliteDatabaseRepository.RebuildAggregatedPlayerStats();
    }

    public static void DeleteGame(Guid id)
    {
        if (IsWriteBlocked()) return;

        SqliteDatabaseRepository.DeleteGame(id);
    }

    public static void DeleteGamesByMode(GameMode mode)
    {
        if (IsWriteBlocked()) return;

        SqliteDatabaseRepository.DeleteGamesByMode(mode);
    }

    public static void DeleteAllGames()
    {
        if (IsWriteBlocked()) return;

        SqliteDatabaseRepository.DeleteAllGames();
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
