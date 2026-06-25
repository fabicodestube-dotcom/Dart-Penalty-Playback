using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class X01GameSettings : GameSettings
{
    [JsonProperty] public int pointTarget;
    [JsonProperty] public CheckinType checkinType;
    [JsonProperty] public CheckoutType checkoutType;


    public override string GetString()
    {
        string setText = setCount == 1 ? "Set" : "Sets";
        string legText = legCount == 1 ? "Leg" : "Legs";

        string modeText = setsAndLegsMode == SetsAndLegs.BestOf
            ? "Best of"
            : "First to";

        return pointTarget + ", "
            + checkinType.ToDescription() + ", "
            + checkoutType.ToDescription() + ", "
            + modeText + " "
            + setCount + " " + setText + " "
            + legCount + " " + legText;
    }

    public override GameSettings Clone()
    {
        return new X01GameSettings
        {
            setsAndLegsMode = setsAndLegsMode,
            setCount = setCount,
            legCount = legCount,
            soundEnabled = soundEnabled,
            pointTarget = pointTarget,
            checkoutType = checkoutType,
            checkinType = checkinType,
            Penalties = Penalties?.Clone()
        };
    }
}
