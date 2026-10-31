using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    public List<RecordedClipIntegrityIssue> DuplicateSlotRows = new List<RecordedClipIntegrityIssue>();
    public List<string> OrphanFiles = new List<string>();

    public bool IsHealthy =>
        InvalidRows.Count == 0 &&
        DuplicateSlotRows.Count == 0 &&
        OrphanFiles.Count == 0;

    public string ToSummary()
    {
        return "Rows=" + RowCount +
               ", Files=" + FileCount +
               ", InvalidRows=" + InvalidRows.Count +
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
    public int QuarantinedFiles;
    public string QuarantineRoot;
    public List<string> Failures = new List<string>();

    public string ToSummary()
    {
        return "DeletedRows=" + DeletedRows +
               ", QuarantinedFiles=" + QuarantinedFiles +
               ", Failures=" + Failures.Count +
               ", Before=[" + (Before != null ? Before.ToSummary() : "") + "]" +
               ", After=[" + (After != null ? After.ToSummary() : "") + "]";
    }
}

public static class RecordedClipIntegrityService
{
    private const string ScoresFolderName = "Scores";
    private const string PenaltiesFolderName = "Penalties";
    private const string SpecialFolderName = "Special";
    private const string QuarantineFolderName = "_quarantine";

    public static RecordedClipIntegrityReport Scan()
    {
        SqliteDatabaseService.EnsureSchema();

        var rows = SqliteDatabaseRepository.LoadAllRecordedClips()
            ?? new List<SqliteDatabaseRepository.RecordedClipRow>();

        var report = new RecordedClipIntegrityReport
        {
            RowCount = rows.Count
        };

        var expectedFiles = new HashSet<string>(StringComparer.Ordinal);
        var seenSlots = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row == null)
                continue;

            string folder = GetFolderForCategory(row.Category);
            if (folder == null)
            {
                report.InvalidRows.Add(CreateIssue(row, "Unbekannte Kategorie"));
                continue;
            }

            if (!IsSafeFileName(row.FileName))
            {
                report.InvalidRows.Add(CreateIssue(row, "Ungültiger Dateiname"));
                continue;
            }

            string fullPath = Path.GetFullPath(Path.Combine(folder, row.FileName));
            expectedFiles.Add(fullPath);

            if (!File.Exists(fullPath))
                report.InvalidRows.Add(CreateIssue(row, "Datei fehlt"));

            if (row.SlotIndex > 0)
            {
                string slotKey = BuildSlotKey(row);
                if (!seenSlots.Add(slotKey))
                    report.DuplicateSlotRows.Add(CreateIssue(row, "Doppelter Slot"));
            }
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

    public static RecordedClipRepairResult Repair(bool quarantineOrphans)
    {
        var result = new RecordedClipRepairResult
        {
            Before = Scan()
        };

        var invalidIds = result.Before.InvalidRows
            .Select(issue => issue.Id)
            .Distinct()
            .ToList();

        if (invalidIds.Count > 0)
            result.DeletedRows = SqliteDatabaseRepository.DeleteRecordedClipsByIds(invalidIds);

        if (quarantineOrphans && result.Before.OrphanFiles.Count > 0)
            QuarantineOrphanFiles(result);

        result.After = Scan();
        return result;
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

    private static string GetFolderForCategory(string category)
    {
        if (!Enum.TryParse(category, true, out AudioCategory parsed))
            return null;

        return parsed switch
        {
            AudioCategory.Score => Path.Combine(DatabasePaths.RecordingsRoot, ScoresFolderName),
            AudioCategory.Penalty => Path.Combine(DatabasePaths.RecordingsRoot, PenaltiesFolderName),
            AudioCategory.Special => Path.Combine(DatabasePaths.RecordingsRoot, SpecialFolderName),
            AudioCategory.Custom => Path.Combine(DatabasePaths.RecordingsRoot, SpecialFolderName),
            _ => null
        };
    }

    private static bool IsSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName == "." || fileName == "..")
            return false;

        if (fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0)
            return false;

        return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
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
