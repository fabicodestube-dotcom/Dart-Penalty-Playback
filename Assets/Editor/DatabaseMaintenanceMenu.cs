using UnityEditor;
using UnityEngine;

public static class DatabaseMaintenanceMenu
{
    [MenuItem("Tools/Database/Scan Clip Integrity")]
    public static void ScanClipIntegrity()
    {
        try
        {
            var report = RecordedClipIntegrityService.Scan();
            Debug.Log("[DB-MAINTENANCE] Clip integrity: " + report.ToSummary());

            foreach (var issue in report.InvalidRows)
                Debug.LogWarning("[DB-MAINTENANCE] Invalid clip row: " + issue);

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
            var result = RecordedClipIntegrityService.Repair(true);
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
}
