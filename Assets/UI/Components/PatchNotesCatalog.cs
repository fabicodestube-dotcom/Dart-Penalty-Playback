using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Patch Notes Catalog")]
public class PatchNotesCatalog : ScriptableObject
{
    public List<PatchNotesEntry> entries = new List<PatchNotesEntry>();
}

[System.Serializable]
public class PatchNotesEntry
{
    public string version = "0.9";
    public string releaseLabel = "Pre-release";
    [TextArea(10, 40)] public string notes;

    public string DisplayTitle
    {
        get
        {
            if (string.IsNullOrWhiteSpace(releaseLabel))
                return "Version " + version;

            return "Version " + version + " (" + releaseLabel + ")";
        }
    }
}
