using System;

public sealed class WinSequence
{
    private WinLevel level = WinLevel.None;
    private string playerName;
    private Action onComplete;

    public void Report(WinLevel newLevel, string name, Action complete = null)
    {
        if (newLevel >= level)
        {
            level = newLevel;
            playerName = name;
        }

        if (complete != null)
            onComplete = complete;
    }

    public bool TryTake(out WinLevel resultLevel, out string resultName, out Action complete)
    {
        resultLevel = level;
        resultName = playerName;
        complete = onComplete;

        bool hasValue = level != WinLevel.None;

        level = WinLevel.None;
        playerName = null;
        onComplete = null;

        return hasValue;
    }
}
