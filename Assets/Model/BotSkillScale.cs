using UnityEngine;

public static class BotSkillScale
{
    public const float Min = 1f;
    public const float Max = 10f;
    public const float Step = 0.1f;
    public const float Default = 5f;

    public static float Normalize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return Default;

        return Mathf.Clamp(Mathf.Round(value / Step) * Step, Min, Max);
    }
}
