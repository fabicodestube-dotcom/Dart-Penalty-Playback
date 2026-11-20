using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ZipCompressionLevel = System.IO.Compression.CompressionLevel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SQLite;
using UnityEngine;

public sealed class DatabaseBackupResult
{
    public bool Success;
    public string Path;
    public string Error;
}

public static class DatabaseBackupService
{
    private const int BackupFormatVersion = 1;
    private const int RollingBackupCount = 5;
    private const int QuarantineDirectoryCount = 5;
    private const string DatabaseEntryName = "database/darts.sqlite";
    private const string RecordingsEntryPrefix = "recordings/";
    private const string ManifestEntryName = "manifest.json";

    private static readonly string[] CountedTables =
    {
        "players",
        "games",
        "throws",
        "recorded_clips",
        "player_game_stats"
    };

    public static DatabaseBackupResult CreateBackup(string reason, int schemaVersion, bool required)
    {
        return CreateBackup(reason, schemaVersion, true, required);
    }

    public static DatabaseBackupResult CreateBackup(
        string reason,
        int schemaVersion,
        bool includeRecordings,
        bool required)
    {
        var result = new DatabaseBackupResult();

        if (!File.Exists(DatabasePaths.DatabasePath))
        {
            result.Error = "Die Datenbankdatei fehlt.";
            if (required)
                throw new DatabaseBootstrapException(result.Error);
            return result;
        }

        string stagingDirectory = null;

        try
        {
            Directory.CreateDirectory(DatabasePaths.BackupsRoot);
            stagingDirectory = DatabasePaths.CreateStagingDirectory("backup");

            string stagedDatabase = Path.Combine(stagingDirectory, DatabasePaths.DatabaseFileName);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string finalPath = Path.Combine(
                DatabasePaths.BackupsRoot,
                "darts-backup-" + timestamp + "-v" + schemaVersion + "-" + SanitizeFilePart(reason) + ".zip"
            );
            string partialPath = finalPath + ".part";

            using (var source = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
            {
                source.Backup(stagedDatabase);
            }

            bool healthy = required
                ? DatabaseHealth.TryFullCheck(stagedDatabase, out string healthError)
                : DatabaseHealth.TryQuickCheck(stagedDatabase, out healthError);

            if (!healthy)
                throw new InvalidOperationException("Backup database failed integrity check: " + healthError);

            var manifest = new BackupManifest
            {
                FormatVersion = BackupFormatVersion,
                CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                AppVersion = Application.version,
                SchemaVersion = schemaVersion,
                Reason = reason,
                IncludesRecordings = includeRecordings
            };

            manifest.TableCounts = ReadTableCounts(stagedDatabase);

            if (File.Exists(partialPath))
                File.Delete(partialPath);

            using (var archive = ZipFile.Open(partialPath, ZipArchiveMode.Create))
            {
                AddFile(archive, stagedDatabase, DatabaseEntryName, manifest, false);

                if (includeRecordings && Directory.Exists(DatabasePaths.RecordingsRoot))
                    AddDirectory(archive, DatabasePaths.RecordingsRoot, RecordingsEntryPrefix, manifest);

                AddManifest(archive, manifest);
            }

            if (!IsZipReadable(partialPath))
                throw new InvalidOperationException("Backup ZIP could not be read back.");

            if (File.Exists(finalPath))
                File.Delete(finalPath);

            File.Move(partialPath, finalPath);
            ApplyRetention();
            TryRecordBackupTimestamp();

            result.Success = true;
            result.Path = finalPath;

            Debug.Log("[DatabaseBackup] Created backup: " + finalPath);
            return result;
        }
        catch (Exception e)
        {
            result.Error = e.Message;
            Debug.LogError("[DatabaseBackup] Backup failed: " + e);

            if (required)
                throw new DatabaseBootstrapException("Backup could not be created: " + e.Message, e);

            return result;
        }
        finally
        {
            DatabasePaths.TryDeleteDirectory(stagingDirectory);
        }
    }

    public static void CreateRollingBackupIfDue(int schemaVersion)
    {
        try
        {
            string newest = GetBackupFiles().FirstOrDefault();
            if (newest != null)
            {
                TimeSpan age = DateTime.UtcNow - File.GetLastWriteTimeUtc(newest);
                if (age < TimeSpan.FromHours(24) && IsZipReadable(newest))
                    return;
            }

            CreateBackup("rolling", schemaVersion, false, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DatabaseBackup] Rolling backup skipped: " + e.Message);
        }
    }

    public static bool TryCreateKnownGoodSnapshot(int schemaVersion, out string path, out string error)
    {
        path = null;
        error = null;
        string stagingDirectory = null;

        try
        {
            if (!File.Exists(DatabasePaths.DatabasePath))
            {
                error = "Die Datenbankdatei fehlt.";
                return false;
            }

            Directory.CreateDirectory(DatabasePaths.KnownGoodRoot);
            stagingDirectory = DatabasePaths.CreateStagingDirectory("known-good");

            string stagedDatabase = Path.Combine(stagingDirectory, DatabasePaths.DatabaseFileName);

            using (var source = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadOnly))
            {
                source.Backup(stagedDatabase);
            }

            using (var connection = new SQLiteConnection(stagedDatabase, SQLiteOpenFlags.ReadOnly))
            {
                if (!DatabaseHealth.TryQuickCheck(connection, out error))
                {
                    error = "Known-good snapshot failed integrity check: " + error;
                    return false;
                }

                int version = DatabaseHealth.GetUserVersion(connection);
                if (version != schemaVersion)
                {
                    error = "Known-good snapshot has schema v" + version + ", expected v" + schemaVersion + ".";
                    return false;
                }

                if (!DatabaseHealth.TryValidateSchema(connection, schemaVersion, out error))
                    return false;
            }

            DatabaseFileSet.Replace(stagedDatabase, DatabasePaths.KnownGoodDatabasePath);
            path = DatabasePaths.KnownGoodDatabasePath;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
        finally
        {
            DatabasePaths.TryDeleteDirectory(stagingDirectory);
        }
    }

    public static void EnsureKnownGoodSnapshot(int schemaVersion)
    {
        if (TryCreateKnownGoodSnapshot(schemaVersion, out _, out string error))
            return;

        Debug.LogWarning("[DatabaseBackup] Known-good snapshot failed: " + error);
    }

    public static bool TryRestoreLatestDatabaseBackup(
        int currentSchemaVersion,
        out string restoredBackup,
        out string error)
    {
        restoredBackup = null;
        error = null;

        foreach (string candidate in GetRestoreCandidates())
        {
            string candidateError;

            try
            {
                bool restored = IsZipPath(candidate)
                    ? TryRestoreZipBackup(candidate, currentSchemaVersion, out candidateError)
                    : TryRestoreKnownGood(candidate, currentSchemaVersion, out candidateError);

                if (restored)
                {
                    restoredBackup = candidate;
                    Debug.Log("[DatabaseBackup] Restored backup: " + candidate);
                    return true;
                }
            }
            catch (Exception e)
            {
                candidateError = e.Message;
                Debug.LogWarning("[DatabaseBackup] Restore failed for '" + candidate + "': " + e.Message);
            }

            if (!string.IsNullOrEmpty(candidateError))
            {
                error = candidateError;
                Debug.LogWarning("[DatabaseBackup] Rejected candidate '" + candidate + "': " + candidateError);
            }
        }

        error ??= "Kein gültiges Backup gefunden.";
        return false;
    }

    public static bool TryValidateBackup(string backupPath, out string error)
    {
        error = null;

        if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
        {
            error = "Backup-Datei fehlt.";
            return false;
        }

        string stagingDirectory = null;

        try
        {
            stagingDirectory = DatabasePaths.CreateStagingDirectory("validate-backup");

            if (IsZipPath(backupPath))
            {
                ZipFile.ExtractToDirectory(backupPath, stagingDirectory);

                if (!TryValidateManifest(stagingDirectory, out BackupManifest manifest, out error))
                    return false;

                string stagedDatabase = Path.Combine(stagingDirectory, "database", DatabasePaths.DatabaseFileName);
                if (!File.Exists(stagedDatabase))
                {
                    error = "Datenbank fehlt im Backup.";
                    return false;
                }

                using (var connection = new SQLiteConnection(stagedDatabase, SQLiteOpenFlags.ReadOnly))
                {
                    if (!DatabaseHealth.TryQuickCheck(connection, out error))
                        return false;

                    int version = DatabaseHealth.GetUserVersion(connection);
                    if (!IsCompatibleVersion(version, DatabaseSchema.CurrentVersion))
                    {
                        error = "Backup hat eine nicht unterstützte Schema-Version: v" + version + ".";
                        return false;
                    }
                }

                return true;
            }

            using (var connection = new SQLiteConnection(backupPath, SQLiteOpenFlags.ReadOnly))
            {
                if (!DatabaseHealth.TryQuickCheck(connection, out error))
                    return false;

                int version = DatabaseHealth.GetUserVersion(connection);
                if (version != DatabaseSchema.CurrentVersion)
                {
                    error = "Known-good Snapshot hat Schema v" + version +
                            ", erwartet v" + DatabaseSchema.CurrentVersion + ".";
                    return false;
                }

                return DatabaseHealth.TryValidateSchema(connection, version, out error);
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
        finally
        {
            DatabasePaths.TryDeleteDirectory(stagingDirectory);
        }
    }

    private static bool TryRestoreZipBackup(string backupPath, int currentSchemaVersion, out string error)
    {
        error = null;
        string stagingDirectory = null;

        try
        {
            stagingDirectory = DatabasePaths.CreateStagingDirectory("restore");
            ZipFile.ExtractToDirectory(backupPath, stagingDirectory);

            if (!TryValidateManifest(stagingDirectory, out BackupManifest manifest, out error))
                return false;

            string stagedDatabase = Path.Combine(stagingDirectory, "database", DatabasePaths.DatabaseFileName);
            if (!File.Exists(stagedDatabase))
            {
                error = "Datenbank fehlt im Backup.";
                return false;
            }

            using (var connection = new SQLiteConnection(stagedDatabase, SQLiteOpenFlags.ReadOnly))
            {
                if (!DatabaseHealth.TryQuickCheck(connection, out error))
                    return false;

                int backupSchemaVersion = DatabaseHealth.GetUserVersion(connection);

                if (!IsCompatibleVersion(backupSchemaVersion, currentSchemaVersion))
                {
                    error = "Backup-Schema v" + backupSchemaVersion +
                            " ist mit v" + currentSchemaVersion + " nicht kompatibel.";
                    return false;
                }

                if (manifest != null && manifest.SchemaVersion != backupSchemaVersion)
                {
                    error = "Manifest-Schemaversion passt nicht zur Datenbank.";
                    return false;
                }
            }

            RestoreStagedDatabase(stagingDirectory, stagedDatabase);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
        finally
        {
            DatabasePaths.TryDeleteDirectory(stagingDirectory);
        }
    }

    private static bool TryRestoreKnownGood(string knownGoodPath, int currentSchemaVersion, out string error)
    {
        error = null;
        string stagingDirectory = null;

        try
        {
            if (!File.Exists(knownGoodPath))
            {
                error = "Known-good Snapshot fehlt.";
                return false;
            }

            using (var connection = new SQLiteConnection(knownGoodPath, SQLiteOpenFlags.ReadOnly))
            {
                if (!DatabaseHealth.TryQuickCheck(connection, out error))
                    return false;

                int version = DatabaseHealth.GetUserVersion(connection);
                if (version != currentSchemaVersion)
                {
                    error = "Known-good Snapshot hat Schema v" + version +
                            ", erwartet v" + currentSchemaVersion + ".";
                    return false;
                }

                if (!DatabaseHealth.TryValidateSchema(connection, version, out error))
                    return false;
            }

            stagingDirectory = DatabasePaths.CreateStagingDirectory("restore-known-good");
            string stagedDatabase = Path.Combine(stagingDirectory, DatabasePaths.DatabaseFileName);
            DatabaseFileSet.Copy(knownGoodPath, stagedDatabase);
            RestoreStagedDatabase(stagingDirectory, stagedDatabase);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
        finally
        {
            DatabasePaths.TryDeleteDirectory(stagingDirectory);
        }
    }

    private static bool IsCompatibleVersion(int backupVersion, int currentVersion)
    {
        if (backupVersion < 0 || backupVersion > currentVersion)
            return false;

        if (backupVersion == currentVersion)
            return true;

        return DatabaseMigrationRunner.CanUpgrade(backupVersion, currentVersion);
    }

    private static bool TryValidateManifest(
        string stagingDirectory,
        out BackupManifest manifest,
        out string error)
    {
        manifest = null;
        error = null;

        string manifestPath = Path.Combine(stagingDirectory, ManifestEntryName);
        if (!File.Exists(manifestPath))
        {
            error = "Manifest fehlt.";
            return false;
        }

        try
        {
            manifest = JsonConvert.DeserializeObject<BackupManifest>(File.ReadAllText(manifestPath));
        }
        catch (Exception e)
        {
            error = "Manifest konnte nicht gelesen werden: " + e.Message;
            return false;
        }

        if (manifest == null)
        {
            error = "Manifest ist leer.";
            return false;
        }

        if (manifest.FormatVersion < 1 || manifest.FormatVersion > BackupFormatVersion)
        {
            error = "Nicht unterstützte Manifest-Version: " + manifest.FormatVersion;
            return false;
        }

        if (manifest.Files == null || manifest.Files.Count == 0)
        {
            error = "Manifest enthält keine Dateien.";
            return false;
        }

        var expectedFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (BackupFileInfo file in manifest.Files)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.Path))
            {
                error = "Manifest enthält einen ungültigen Dateieintrag.";
                return false;
            }

            if (!expectedFiles.Add(file.Path))
            {
                error = "Manifest enthält doppelten Dateieintrag: " + file.Path;
                return false;
            }

            string fullPath = ResolveExtractedPath(stagingDirectory, file.Path);
            if (fullPath == null)
            {
                error = "Manifest enthält einen unsicheren Pfad: " + file.Path;
                return false;
            }

            if (!File.Exists(fullPath))
            {
                error = "Datei aus Manifest fehlt: " + file.Path;
                return false;
            }

            long size = new FileInfo(fullPath).Length;
            if (size != file.Size)
            {
                error = "Dateigröße stimmt nicht: " + file.Path;
                return false;
            }

            if (!string.Equals(ComputeSha256(fullPath), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                error = "Prüfsumme stimmt nicht: " + file.Path;
                return false;
            }
        }

        if (!expectedFiles.Contains(DatabaseEntryName))
        {
            error = "Datenbank fehlt im Manifest.";
            return false;
        }

        foreach (string actualFile in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(stagingDirectory, actualFile).Replace('\\', '/');
            if (relativePath == ManifestEntryName)
                continue;

            if (!expectedFiles.Contains(relativePath))
            {
                error = "Unerwartete Datei im Backup: " + relativePath;
                return false;
            }
        }

        return true;
    }

    private static string ResolveExtractedPath(string rootDirectory, string relativePath)
    {
        string normalizedRoot = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(
            Path.Combine(normalizedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))
        );

        if (string.Equals(fullPath, normalizedRoot, StringComparison.Ordinal))
            return null;

        if (!fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;

        return fullPath;
    }

    private static void RestoreStagedDatabase(string stagingDirectory, string stagedDatabase)
    {
        string quarantineDirectory = DatabasePaths.CreateQuarantineDirectory("restore");
        string quarantinedDatabase = Path.Combine(quarantineDirectory, DatabasePaths.DatabaseFileName);
        string stagedRecordings = Path.Combine(stagingDirectory, "recordings");
        string quarantinedRecordings = Path.Combine(quarantineDirectory, DatabasePaths.RecordingsFolderName);
        bool movedDatabase = false;
        bool movedRecordings = false;

        try
        {
            if (DatabaseFileSet.Exists(DatabasePaths.DatabasePath))
            {
                DatabaseFileSet.MoveTo(DatabasePaths.DatabasePath, quarantinedDatabase);
                movedDatabase = true;
            }

            DatabaseFileSet.Replace(stagedDatabase, DatabasePaths.DatabasePath);

            if (Directory.Exists(stagedRecordings))
            {
                if (Directory.Exists(DatabasePaths.RecordingsRoot))
                {
                    Directory.Move(DatabasePaths.RecordingsRoot, quarantinedRecordings);
                    movedRecordings = true;
                }

                Directory.Move(stagedRecordings, DatabasePaths.RecordingsRoot);
            }

            PruneQuarantine();
        }
        catch
        {
            try
            {
                if (File.Exists(DatabasePaths.DatabasePath))
                    DatabaseFileSet.Delete(DatabasePaths.DatabasePath);

                if (movedDatabase && File.Exists(quarantinedDatabase))
                    DatabaseFileSet.MoveTo(quarantinedDatabase, DatabasePaths.DatabasePath);

                if (Directory.Exists(DatabasePaths.RecordingsRoot))
                    Directory.Delete(DatabasePaths.RecordingsRoot, true);

                if (movedRecordings && Directory.Exists(quarantinedRecordings))
                    Directory.Move(quarantinedRecordings, DatabasePaths.RecordingsRoot);
            }
            catch (Exception rollbackException)
            {
                Debug.LogError("[DatabaseBackup] Restore rollback failed: " + rollbackException);
            }

            throw;
        }
    }

    private static void AddManifest(ZipArchive archive, BackupManifest manifest)
    {
        var entry = archive.CreateEntry(ManifestEntryName, ZipCompressionLevel.Optimal);
        using (var stream = entry.Open())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }
    }

    private static void AddDirectory(
        ZipArchive archive,
        string rootDirectory,
        string entryPrefix,
        BackupManifest manifest)
    {
        string normalizedRoot = Path.GetFullPath(rootDirectory);

        foreach (string file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(file);
            string relativePath = fullPath.Substring(normalizedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');

            AddFile(archive, fullPath, entryPrefix + relativePath, manifest, true);
        }
    }

    private static void AddFile(
        ZipArchive archive,
        string filePath,
        string entryName,
        BackupManifest manifest,
        bool isRecording)
    {
        var fileInfo = new FileInfo(filePath);
        var entry = archive.CreateEntry(entryName, ZipCompressionLevel.Optimal);

        using (var entryStream = entry.Open())
        using (var fileStream = File.OpenRead(filePath))
        {
            fileStream.CopyTo(entryStream);
        }

        if (isRecording)
        {
            manifest.RecordingFileCount++;
            manifest.RecordingBytes += fileInfo.Length;
        }

        manifest.Files.Add(new BackupFileInfo
        {
            Path = entryName,
            Size = fileInfo.Length,
            Sha256 = ComputeSha256(filePath)
        });
    }

    private static Dictionary<string, int> ReadTableCounts(string databasePath)
    {
        var counts = new Dictionary<string, int>();

        using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly))
        {
            foreach (string table in CountedTables)
            {
                if (!DatabaseHealth.TableExists(connection, table))
                    continue;

                counts[table] = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM " + table + ";");
            }
        }

        return counts;
    }

    private static bool IsZipReadable(string zipPath)
    {
        try
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                return archive.GetEntry(DatabaseEntryName) != null && archive.GetEntry(ManifestEntryName) != null;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsZipPath(string path)
    {
        return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetBackupFiles()
    {
        if (!Directory.Exists(DatabasePaths.BackupsRoot))
            return Enumerable.Empty<string>();

        return Directory
            .EnumerateFiles(DatabasePaths.BackupsRoot, "darts-backup-*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> GetRestoreCandidates()
    {
        var candidates = new List<string>();

        if (File.Exists(DatabasePaths.KnownGoodDatabasePath))
            candidates.Add(DatabasePaths.KnownGoodDatabasePath);

        candidates.AddRange(GetBackupFiles());

        return candidates
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ApplyRetention()
    {
        try
        {
            var files = GetBackupFiles().ToList();
            if (files.Count <= RollingBackupCount)
                return;

            var keep = new HashSet<string>(files.Take(RollingBackupCount), StringComparer.Ordinal);
            foreach (var group in files.GroupBy(ParseSchemaVersion))
            {
                string newestForVersion = group.FirstOrDefault();
                if (newestForVersion != null)
                    keep.Add(newestForVersion);
            }

            foreach (string file in files)
            {
                if (keep.Contains(file))
                    continue;

                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[DatabaseBackup] Could not delete old backup '" + file + "': " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DatabaseBackup] Retention cleanup failed: " + e.Message);
        }
    }

    private static void PruneQuarantine()
    {
        try
        {
            if (!Directory.Exists(DatabasePaths.CorruptRoot))
                return;

            var directories = Directory
                .EnumerateDirectories(DatabasePaths.CorruptRoot, "*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .ToArray();

            for (int i = QuarantineDirectoryCount; i < directories.Length; i++)
            {
                try
                {
                    Directory.Delete(directories[i], true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[DatabaseBackup] Could not delete old quarantine '" + directories[i] + "': " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DatabaseBackup] Quarantine cleanup failed: " + e.Message);
        }
    }

    private static void TryRecordBackupTimestamp()
    {
        try
        {
            using (var connection = new SQLiteConnection(DatabasePaths.DatabasePath, SQLiteOpenFlags.ReadWrite))
            {
                DatabaseMeta.Set(
                    connection,
                    DatabaseSchema.LastBackupMetaKey,
                    DateTime.UtcNow.ToString("o")
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DatabaseBackup] Could not record backup timestamp: " + e.Message);
        }
    }

    private static int ParseSchemaVersion(string backupPath)
    {
        string name = Path.GetFileNameWithoutExtension(backupPath);
        int markerIndex = name.LastIndexOf("-v", StringComparison.Ordinal);
        if (markerIndex < 0)
            return -1;

        int start = markerIndex + 2;
        int end = name.IndexOf('-', start);
        if (end < 0)
            end = name.Length;

        return int.TryParse(name.Substring(start, end - start), out int version)
            ? version
            : -1;
    }

    private static string ComputeSha256(string filePath)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hash = sha.ComputeHash(stream);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                builder.Append(value.ToString("x2"));

            return builder.ToString();
        }
    }

    private static string SanitizeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "backup";

        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
        }

        return builder.ToString();
    }

    private sealed class BackupManifest
    {
        public int FormatVersion;
        public string CreatedAtUtc;
        public string AppVersion;
        public int SchemaVersion;
        public string Reason;
        public bool IncludesRecordings;
        public Dictionary<string, int> TableCounts = new Dictionary<string, int>();
        public int RecordingFileCount;
        public long RecordingBytes;
        public List<BackupFileInfo> Files = new List<BackupFileInfo>();
    }

    private sealed class BackupFileInfo
    {
        public string Path;
        public long Size;
        public string Sha256;
    }
}
