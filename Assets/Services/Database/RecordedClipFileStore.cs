using System;
using System.IO;
using System.Security.Cryptography;

public static class RecordedClipFileStore
{
    public static bool TryResolvePath(string category, string fileName, out string fullPath)
    {
        fullPath = null;

        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(fileName))
            return false;

        if (!Enum.TryParse(category, true, out AudioCategory parsedCategory))
            return false;

        if (!IsSafeFileName(fileName))
            return false;

        string folder = GetFolderForCategory(parsedCategory);
        if (folder == null)
            return false;

        fullPath = Path.GetFullPath(Path.Combine(folder, fileName));
        return true;
    }

    public static bool TryReadMetadata(string fullPath, out long fileSize, out string sha256, out string createdAtUtc)
    {
        fileSize = 0;
        sha256 = null;
        createdAtUtc = null;

        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            return false;

        try
        {
            var info = new FileInfo(fullPath);
            fileSize = info.Length;
            createdAtUtc = DatabaseTimestamp.ToUtcIso(info.LastWriteTimeUtc);
            sha256 = ComputeSha256(fullPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ComputeSha256(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    public static string GetFolderForCategory(AudioCategory category)
    {
        return category switch
        {
            AudioCategory.Score => Path.Combine(DatabasePaths.RecordingsRoot, "Scores"),
            AudioCategory.Penalty => Path.Combine(DatabasePaths.RecordingsRoot, "Penalties"),
            AudioCategory.Special => Path.Combine(DatabasePaths.RecordingsRoot, "Special"),
            AudioCategory.Custom => Path.Combine(DatabasePaths.RecordingsRoot, "Special"),
            _ => null
        };
    }

    public static bool IsSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName == "." || fileName == "..")
            return false;

        if (fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0)
            return false;

        return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}
