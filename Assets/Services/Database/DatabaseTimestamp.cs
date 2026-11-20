using System;
using System.Globalization;

public static class DatabaseTimestamp
{
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    public static string ToUtcIso(DateTime value)
    {
        return value.ToUniversalTime().ToString(UtcFormat, CultureInfo.InvariantCulture);
    }

    public static string ToUtcIso(DateTime? value)
    {
        return value.HasValue ? ToUtcIso(value.Value) : null;
    }
}
