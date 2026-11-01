using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;

public sealed class RecordedClipIntegrityIssue
{
    public long Id;
    public string Category;
    public string FileName;
    public string Reason;

    public override string ToString()
    {
        return "Id=" + Id + ", Category=" + (Category ?? "") +
               ", File=" + (FileName ?? "") + ", Reason=" + Reason;
    }
}

public sealed class RecordedClipIntegrityReport
{
    public int RowCount;
    public int FileCount;
    public List<RecordedClipIntegrityIssue> InvalidRows = new List<RecordedClipIntegrityIssue>();
    public List<RecordedClipIntegrityIssue> MetadataRows = new List<RecordedClipIntegrityIssue>();
    public List<RecordedClipIntegrityIssue> DuplicateSlotRows = new List<RecordedClipIntegrityIssue>();
    public List<string> OrphanFiles = new List<string>();

    public bool IsHealthy =>
        InvalidRows.Count == 0 &&
        MetadataRows.Count == 0 &&
        DuplicateSlotRows.Count == 0 &&
        OrphanFiles.Count == 0;

    public string ToSummary()
    {
        return "Rows=" + RowCount +
               ", Files=" + FileCount +
               ", InvalidRows=" + InvalidRows.Count +
               ", MetadataRows=" + MetadataRows.Count +
               ", DuplicateSlots=" + DuplicateSlotRows.Count +
               ", OrphanFiles=" + OrphanFiles.Count +
               ", Healthy=" + IsHealthy;
    }
}

public sealed class RecordedClipRepairResult
{
    public RecordedClipIntegrityReport Before;
    public RecordedClipIntegrityReport After;
    public int DeletedRows;
    public int RefreshedMetadataRows;
    public int DeletedDuplicateRows;
    public int RenumberedSlots;
    public int QuarantinedFiles;
    public string QuarantineRoot;
    public List<string> Failures = new List<string>();

    public string ToSummary()
    {
        return "DeletedRows=" + DeletedRows +
               ", RefreshedMetadata=" + RefreshedMetadataRows +
               ", DeletedDuplicates=" + DeletedDuplicateRows +
               ", RenumberedSlots=" + RenumberedSlots +
               ", QuarantinedFiles=" + QuarantinedFiles +
               ", Failures=" + Failures.Count +
               ", Before=[" + (Before != null ? Before.ToSummary() : "") + "]" +
               ", After=[" + (After != null ? After.ToSummary() : "") + "]";
    }
}

public static class RecordedClipIntegrityService
{
    private const string QuarantineFolderName = "_quarantine";

    public static RecordedClipIntegrityReport Scan(bool verifyHashes = false)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            return Scan(connection, verifyHashes);
        }
    }

    internal static RecordedClipIntegrityReport Scan(SQLiteConnection connection, bool verifyHashes)
    {
        var rows = SqliteDatabaseRepository.LoadAllRecordedClips(connection);
        var report = new RecordedClipIntegrityReport
        {
            RowCount = rows.Count
        };

        var expectedFiles = new HashSet<string>(StringComparer.Ordinal);
        var seenSlots = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!RecordedClipFileStore.TryResolvePath(row.Category, row.FileName, out string fullPath))
            {
                report.InvalidRows.Add(CreateIssue(row, "Ungültige Kategorie oder Dateiname"));
                continue;
            }

            expectedFiles.Add(fullPath);

            if (!File.Exists(fullPath))
            {
                report.InvalidRows.Add(CreateIssue(row, "Datei fehlt"));
                continue;
            }

            var info = new FileInfo(fullPath);
            if (!row.FileSize.HasValue || row.FileSize.Value != info.Length)
            {
                report.MetadataRows.Add(CreateIssue(row, "Dateigröße weicht ab oder Metadaten fehlen"));
            }
            else if (string.IsNullOrWhiteSpace(row.Sha256))
            {
                report.MetadataRows.Add(CreateIssue(row, "SHA-256 fehlt"));
            }
            else if (verifyHashes)
            {
                try
                {
                    string actualHash = RecordedClipFileStore.ComputeSha256(fullPath);
                    if (!string.Equals(actualHash, row.Sha256, StringComparison.OrdinalIgnoreCase))
                        report.MetadataRows.Add(CreateIssue(row, "SHA-256 weicht ab"));
                }
                catch (Exception e)
                {
                    report.MetadataRows.Add(CreateIssue(row, "Hash nicht lesbar: " + e.Message));
                }
            }

            string slotKey = BuildSlotKey(row);
            if (!seenSlots.Add(slotKey))
                report.DuplicateSlotRows.Add(CreateIssue(row, "Doppelter Slot"));
        }

        List<string> files = GetRecordingFiles();
        report.FileCount = files.Count;

        foreach (string file in files)
        {
            if (!expectedFiles.Contains(file))
                report.OrphanFiles.Add(file);
        }

        return report;
    }

    public static RecordedClipRepairResult Repair(bool quarantineOrphans, bool verifyHashes = true)
    {
        SqliteDatabaseService.EnsureSchema();

        using (var connection = SqliteDatabaseService.Open())
        {
            var result = new RecordedClipRepairResult
            {
                Before = Scan(connection, verifyHashes)
            };

            try
            {
                connection.BeginTransaction();

                foreach (var issue in result.Before.InvalidRows)
                    result.DeletedRows += connection.Execute("DELETE FROM recorded_clips WHERE id = ?;", issue.Id);

                var refreshedIds = new HashSet<long>();
                foreach (var issue in result.Before.MetadataRows)
                {
                    if (!refreshedIds.Add(issue.Id))
                        continue;

                    if (SqliteDatabaseRepository.RefreshRecordedClipMetadata(connection, issue.Id))
                        result.RefreshedMetadataRows++;
                    else
                        result.Failures.Add("Metadaten konnten nicht aktualisiert werden: " + issue);
                }

                var slotRepair = SqliteDatabaseRepository.RepairRecordedClipSlots(connection);
                result.DeletedDuplicateRows = slotRepair.DeletedDuplicates;
                result.RenumberedSlots = slotRepair.RenumberedSlots;

                connection.Commit();
            }
            catch
            {
                connection.Rollback();
                throw;
            }

            if (quarantineOrphans && result.Before.OrphanFiles.Count > 0)
                QuarantineOrphanFiles(result);

            result.After = Scan(connection, false);
            return result;
        }
    }

    private static void QuarantineOrphanFiles(RecordedClipRepairResult result)
    {
        result.QuarantineRoot = Path.Combine(
            DatabasePaths.RecordingsRoot,
            QuarantineFolderName,
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")
        );

        foreach (string orphanFile in result.Before.OrphanFiles)
        {
            try
            {
                string relativePath = Path.GetRelativePath(DatabasePaths.RecordingsRoot, orphanFile);
                string targetPath = Path.Combine(result.QuarantineRoot, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Move(orphanFile, targetPath);
                result.QuarantinedFiles++;
            }
            catch (Exception e)
            {
                result.Failures.Add(orphanFile + ": " + e.Message);
            }
        }
    }

    private static List<string> GetRecordingFiles()
    {
        var files = new List<string>();

        if (!Directory.Exists(DatabasePaths.RecordingsRoot))
            return files;

        string root = Path.GetFullPath(DatabasePaths.RecordingsRoot);

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(file);
            string relativePath = Path.GetRelativePath(root, fullPath).Replace('\\', '/');

            if (relativePath == QuarantineFolderName ||
                relativePath.StartsWith(QuarantineFolderName + "/", StringComparison.Ordinal))
                continue;

            files.Add(fullPath);
        }

        return files;
    }

    private static string BuildSlotKey(SqliteDatabaseRepository.RecordedClipRow row)
    {
        return (row.Category ?? "") + "\u001f" +
               row.Score + "\u001f" +
               (row.PenaltyType ?? "") + "\u001f" +
               (row.SpecialType ?? "") + "\u001f" +
               (row.CustomKey ?? "") + "\u001f" +
               row.SlotIndex;
    }

    private static RecordedClipIntegrityIssue CreateIssue(
        SqliteDatabaseRepository.RecordedClipRow row,
        string reason)
    {
        return new RecordedClipIntegrityIssue
        {
            Id = row.Id,
            Category = row.Category,
            FileName = row.FileName,
            Reason = reason
        };
    }
}
