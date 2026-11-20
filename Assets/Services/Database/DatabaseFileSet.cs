using System;
using System.IO;

/// <summary>
/// Handles the SQLite database together with its WAL/SHM sidecar files.
/// A database is only consistent when all parts are moved/deleted together.
/// </summary>
public static class DatabaseFileSet
{
    public static string WalPath(string databasePath)
    {
        return databasePath + "-wal";
    }

    public static string ShmPath(string databasePath)
    {
        return databasePath + "-shm";
    }

    public static bool Exists(string databasePath)
    {
        return !string.IsNullOrEmpty(databasePath) &&
               (File.Exists(databasePath) || File.Exists(WalPath(databasePath)));
    }

    public static void Delete(string databasePath)
    {
        if (string.IsNullOrEmpty(databasePath))
            return;

        DeleteIfExists(databasePath);
        DeleteIfExists(WalPath(databasePath));
        DeleteIfExists(ShmPath(databasePath));
    }

    public static void DeleteSidecars(string databasePath)
    {
        if (string.IsNullOrEmpty(databasePath))
            return;

        DeleteIfExists(WalPath(databasePath));
        DeleteIfExists(ShmPath(databasePath));
    }

    public static void MoveTo(string sourceDatabasePath, string destinationDatabasePath)
    {
        if (string.IsNullOrEmpty(sourceDatabasePath) || string.IsNullOrEmpty(destinationDatabasePath))
            throw new ArgumentException("Source and destination database paths are required.");

        Directory.CreateDirectory(Path.GetDirectoryName(destinationDatabasePath));

        MoveIfExists(sourceDatabasePath, destinationDatabasePath);
        MoveIfExists(WalPath(sourceDatabasePath), WalPath(destinationDatabasePath));
        MoveIfExists(ShmPath(sourceDatabasePath), ShmPath(destinationDatabasePath));
    }

    public static void Copy(string sourceDatabasePath, string destinationDatabasePath)
    {
        if (!File.Exists(sourceDatabasePath))
            throw new FileNotFoundException("Database file is missing.", sourceDatabasePath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationDatabasePath));

        File.Copy(sourceDatabasePath, destinationDatabasePath, true);
        CopyIfExists(WalPath(sourceDatabasePath), WalPath(destinationDatabasePath));
        CopyIfExists(ShmPath(sourceDatabasePath), ShmPath(destinationDatabasePath));
    }

    public static void Replace(string sourceDatabasePath, string destinationDatabasePath)
    {
        if (!File.Exists(sourceDatabasePath))
            throw new FileNotFoundException("Staged database file is missing.", sourceDatabasePath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationDatabasePath));
        DeleteSidecars(destinationDatabasePath);

        if (File.Exists(destinationDatabasePath))
        {
            try
            {
                File.Replace(sourceDatabasePath, destinationDatabasePath, null);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (IOException)
            {
            }

            File.Delete(destinationDatabasePath);
        }

        File.Move(sourceDatabasePath, destinationDatabasePath);

        MoveIfExists(WalPath(sourceDatabasePath), WalPath(destinationDatabasePath));
        MoveIfExists(ShmPath(sourceDatabasePath), ShmPath(destinationDatabasePath));
    }

    private static void MoveIfExists(string source, string destination)
    {
        if (!File.Exists(source))
            return;

        if (File.Exists(destination))
            File.Delete(destination);

        File.Move(source, destination);
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (!File.Exists(source))
            return;

        if (File.Exists(destination))
            File.Delete(destination);

        File.Copy(source, destination);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
