using System;
using UnityEngine;

public static class SaveSystem
{
    private static bool loadFailed;

    public static void SaveDatabase(Database db)
    {
        if (db == null) return;

        if (loadFailed)
        {
            Debug.LogError("Speichern blockiert: Die SQLite-Datenbank konnte beim Start nicht geladen werden.");
            return;
        }

        SqliteDatabaseRepository.SaveDatabase(db);
    }

    public static void SaveGame(Game game)
    {
        if (game == null) return;

        if (loadFailed)
        {
            Debug.LogError("Speichern blockiert: Die SQLite-Datenbank konnte beim Start nicht geladen werden.");
            return;
        }

        SqliteDatabaseRepository.SaveGame(game);
    }

    public static void SaveAggregatedPlayerStats(Game game)
    {
        if (game == null) return;

        if (loadFailed)
        {
            Debug.LogError("Speichern blockiert: Die SQLite-Datenbank konnte beim Start nicht geladen werden.");
            return;
        }

        SqliteDatabaseRepository.SaveAggregatedPlayerStats(game);
    }

    public static void RebuildAggregatedPlayerStats()
    {
        if (loadFailed)
        {
            Debug.LogError("Speichern blockiert: Die SQLite-Datenbank konnte beim Start nicht geladen werden.");
            return;
        }

        SqliteDatabaseRepository.RebuildAggregatedPlayerStats();
    }

    public static void DeleteGame(Guid id)
    {
        SqliteDatabaseRepository.DeleteGame(id);
    }

    public static void DeleteGamesByMode(GameMode mode)
    {
        SqliteDatabaseRepository.DeleteGamesByMode(mode);
    }

    public static void DeleteAllGames()
    {
        SqliteDatabaseRepository.DeleteAllGames();
    }

    public static Game LoadGameById(Guid id)
    {
        return SqliteDatabaseRepository.LoadGameById(id);
    }

    public static Database LoadDatabase()
    {
        try
        {
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
            return new Database();
        }
    }
}
