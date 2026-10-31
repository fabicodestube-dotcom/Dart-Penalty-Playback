using System;
using System.IO;
using UnityEngine;

public static class DatabasePaths
{
    public const string DatabaseFileName = "darts.sqlite";
    public const string RecordingsFolderName = "Recordings";
    public const string BackupsFolderName = "Backups";
    public const string StagingFolderName = "DatabaseStaging";

#if UNITY_EDITOR
    public static string RootOverride { get; set; }
#endif

    public static string Root =>
#if UNITY_EDITOR
        string.IsNullOrEmpty(RootOverride) ? Application.persistentDataPath : RootOverride;
#else
        Application.persistentDataPath;
#endif

    public static string DatabasePath => Path.Combine(Root, DatabaseFileName);
    public static string RecordingsRoot => Path.Combine(Root, RecordingsFolderName);
    public static string BackupsRoot => Path.Combine(Root, BackupsFolderName);
    public static string StagingRoot => Path.Combine(Root, StagingFolderName);

    public static string CreateStagingDirectory(string purpose)
    {
        string path = Path.Combine(
            StagingRoot,
            purpose + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(path);
        return path;
    }

    public static void TryDeleteDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DatabasePaths] Could not delete staging directory '" + path + "': " + e.Message);
        }
    }
}
