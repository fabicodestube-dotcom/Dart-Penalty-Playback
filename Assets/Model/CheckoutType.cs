using System;
using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json;

[System.Serializable]
public enum CheckoutType
{
    [JsonProperty, Description("Single Out")] Single,
    [JsonProperty, Description("Double Out")] Double,
    [JsonProperty, Description("Triple Out")] Triple,
    [JsonProperty, Description("Master Out")] Master
}

[System.Serializable]
public enum CheckinType
{
    [JsonProperty, Description("Straight In")] StraightIn,
    [JsonProperty, Description("Double In")] DoubleIn,
    [JsonProperty, Description("Master In")] MasterIn
}




public static class EnumExtensions
{
    public static string ToDescription(this Enum value)
    {
        FieldInfo field = value.GetType().GetField(value.ToString());
        
        if (field != null)
        {
            var attribute = field.GetCustomAttribute<DescriptionAttribute>();
            if (attribute != null)
            {
                return attribute.Description;
            }
        }
        
        return value.ToString(); // Fallback, falls kein Attribut existiert
    }
}
