using System;

public enum DatabaseBootstrapStatus
{
    NotStarted,
    Running,
    Ready,
    Failed
}

public sealed class DatabaseBootstrapResult
{
    public bool Success;
    public bool CreatedNewDatabase;
    public bool RestoredFromBackup;
    public bool Migrated;
    public int FromSchemaVersion;
    public int ToSchemaVersion;
    public string BackupPath;
    public string Error;
    public Exception Exception;

    public static DatabaseBootstrapResult Failed(string error, Exception exception = null)
    {
        return new DatabaseBootstrapResult
        {
            Success = false,
            Error = error,
            Exception = exception
        };
    }
}

public sealed class DatabaseBootstrapException : Exception
{
    public DatabaseBootstrapException(string message)
        : base(message)
    {
    }

    public DatabaseBootstrapException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
