using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class CricketGameSettings : GameSettings
{
    [JsonProperty] public bool pointsEnabled = true;
    [JsonProperty] public bool cutThroatEnabled = false;

    public override string GetString()
    {
        string points = pointsEnabled ? "Points on" : "Points off";
        string ct = cutThroatEnabled ? "CutThroat on" : "CutThroat off";

        string setText = setCount == 1 ? "Set" : "Sets";
        string legText = legCount == 1 ? "Leg" : "Legs";

        string modeText = setsAndLegsMode == SetsAndLegs.BestOf
            ? "Best of"
            : "First to";

        return $"{points}, {ct}, {modeText} {setCount} {setText} {legCount} {legText}";
    }

    public bool IsValid(out string reason)
    {
        if (setCount < 1 || legCount < 1)
        {
            reason = "Sets/Legs must be >= 1";
            return false;
        }

        // As requested: Only "Punkte aus" + "CutThroat an" is not allowed.
        if (!pointsEnabled && cutThroatEnabled)
        {
            reason = "Cricket: 'Points off' + 'CutThroat on' is not allowed.";
            return false;
        }

        reason = null;
        return true;
    }

    public override GameSettings Clone()
    {
        return new CricketGameSettings
        {
            setsAndLegsMode = setsAndLegsMode,
            setCount = setCount,
            legCount = legCount,
            soundEnabled = soundEnabled,
            pointsEnabled = pointsEnabled,
            cutThroatEnabled = cutThroatEnabled,
            Penalties = Penalties?.Clone()
        };
    }
}

