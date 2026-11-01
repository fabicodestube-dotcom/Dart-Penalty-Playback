using UnityEditor;
using UnityEngine;

public static class DatabaseMaintenanceMenu
{
    [MenuItem("Tools/Database/Scan Clip Integrity")]
    public static void ScanClipIntegrity()
    {
        ScanClips(false);
    }

    [MenuItem("Tools/Database/Scan Clip Integrity (Hashes)")]
    public static void ScanClipIntegrityWithHashes()
    {
        ScanClips(true);
    }

    private static void ScanClips(bool verifyHashes)
    {
        try
        {
            var report = RecordedClipIntegrityService.Scan(verifyHashes);
            Debug.Log("[DB-MAINTENANCE] Clip integrity: " + report.ToSummary());

            foreach (var issue in report.InvalidRows)
                Debug.LogWarning("[DB-MAINTENANCE] Invalid clip row: " + issue);

            foreach (var issue in report.MetadataRows)
                Debug.LogWarning("[DB-MAINTENANCE] Clip metadata: " + issue);

            foreach (var issue in report.DuplicateSlotRows)
                Debug.LogWarning("[DB-MAINTENANCE] Duplicate clip slot: " + issue);

            foreach (string orphan in report.OrphanFiles)
                Debug.LogWarning("[DB-MAINTENANCE] Orphan clip file: " + orphan);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Clip scan failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Repair Clip Index")]
    public static void RepairClipIndex()
    {
        try
        {
            var result = RecordedClipIntegrityService.Repair(true, true);
            Debug.Log("[DB-MAINTENANCE] Clip repair: " + result.ToSummary());

            if (!string.IsNullOrEmpty(result.QuarantineRoot))
                Debug.Log("[DB-MAINTENANCE] Orphans moved to: " + result.QuarantineRoot);

            foreach (string failure in result.Failures)
                Debug.LogWarning("[DB-MAINTENANCE] Repair failure: " + failure);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Clip repair failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Scan Database")]
    public static void ScanDatabase()
    {
        try
        {
            var report = DatabaseConsistencyService.Scan(true);
            Debug.Log("[DB-MAINTENANCE] Database consistency: " + report.ToSummary());

            foreach (string issue in report.Issues)
                Debug.LogWarning("[DB-MAINTENANCE] Issue: " + issue);

            foreach (string warning in report.Warnings)
                Debug.LogWarning("[DB-MAINTENANCE] Warning: " + warning);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Database scan failed: " + e);
        }
    }

    [MenuItem("Tools/Database/Repair Database")]
    public static void RepairDatabase()
    {
        try
        {
            var result = DatabaseConsistencyService.Repair(true, true);
            Debug.Log("[DB-MAINTENANCE] Database repair: " + result.ToSummary());

            if (result.ClipRepair != null && !string.IsNullOrEmpty(result.ClipRepair.QuarantineRoot))
                Debug.Log("[DB-MAINTENANCE] Clip orphans moved to: " + result.ClipRepair.QuarantineRoot);

            foreach (string failure in result.Failures)
                Debug.LogWarning("[DB-MAINTENANCE] Repair failure: " + failure);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[DB-MAINTENANCE] Database repair failed: " + e);
        }
    }
}
