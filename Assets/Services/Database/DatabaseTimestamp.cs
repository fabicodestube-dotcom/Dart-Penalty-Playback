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

    public static bool TryNormalize(string value, out string normalized)
    {
        normalized = value;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        if (!HasExplicitOffset(trimmed))
            return false;

        if (!DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset parsed))
            return false;

        normalized = parsed.UtcDateTime.ToString(UtcFormat, CultureInfo.InvariantCulture);
        return true;
    }

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            return true;

        int length = value.Length;
        if (length < 6)
            return false;

        char sign = value[length - 6];
        if (sign != '+' && sign != '-')
            return false;

        if (value[length - 3] != ':')
            return false;

        return char.IsDigit(value[length - 5]) &&
               char.IsDigit(value[length - 4]) &&
               char.IsDigit(value[length - 2]) &&
               char.IsDigit(value[length - 1]);
    }
}
