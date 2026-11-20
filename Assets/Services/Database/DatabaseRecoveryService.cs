using System;
using UnityEngine;

/// <summary>
/// Coordinates recovery after the database has already passed the bootstrap gate
/// but the actual repository load failed.
/// </summary>
public static class DatabaseRecoveryService
{
    public static bool RecoveredThisSession { get; private set; }

    public static void ResetForTests()
    {
        RecoveredThisSession = false;
    }

    public static bool TryRecoverForLoadFailure(out string restoredBackup, out string error)
    {
        restoredBackup = null;
        error = null;

        try
        {
            if (!DatabaseBackupService.TryRestoreLatestDatabaseBackup(
                    DatabaseSchema.CurrentVersion,
                    out restoredBackup,
                    out error))
            {
                Debug.LogError("[DatabaseRecovery] Restore after load failure failed: " + error);
                return false;
            }

            DatabaseBootstrapper.ResetForRecovery();
            RecoveredThisSession = true;

            Debug.LogWarning("[DatabaseRecovery] Database restored after load failure: " + restoredBackup);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            Debug.LogError("[DatabaseRecovery] Recovery failed: " + e);
            return false;
        }
    }
}
