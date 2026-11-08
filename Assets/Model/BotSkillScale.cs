using UnityEngine;

public static class BotSkillScale
{
    public const float Min = 1f;
    public const float Max = 10f;
    public const float Step = 0.5f;
    public const float Default = 5f;

    public const float EasyMax = 2.5f;
    public const float MediumMax = 5.5f;
    public const float HardMax = 8.5f;

    public static float Normalize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return Default;

        return Mathf.Clamp(Mathf.Round(value / Step) * Step, Min, Max);
    }

    public static string GetTierKey(float value)
    {
        float skill = Normalize(value);

        if (skill <= EasyMax)
            return "Easy";
        if (skill <= MediumMax)
            return "Medium";
        if (skill <= HardMax)
            return "Hard";

        return "Pro";
    }

    public static float FromLegacyDifficulty(string difficulty)
    {
        switch (difficulty?.Trim().ToUpperInvariant())
        {
            case "MEDIUM":
                return 4f;
            case "HARD":
                return 7f;
            case "PRO":
                return 10f;
            default:
                return 1f;
        }
    }
}
