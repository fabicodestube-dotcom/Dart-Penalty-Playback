using TMPro;
using UnityEngine;

/// <summary>
/// One line in the penalty detail popup: (Name | Cost | Number | Line Total).
/// Filling exclusively via Bind() (pop-up code); fields wired in the prefab.
/// </summary>
public class PenaltyDetailPopupItem : MonoBehaviour
{
    [Header("Zellen")]
    public TMP_Text penaltyName;
    public TMP_Text penaltyCount;
    public TMP_Text penaltyCosts;
    public TMP_Text penaltySum;

    /// <summary>Describes the line. All values already in display strings.</summary>
    public void Bind(string name, string cost, string count, string rowTotal)
    {
        if (penaltyName != null)
            penaltyName.text = name ?? "";
        if (penaltyCosts != null)
            penaltyCosts.text = cost ?? "";
        if (penaltyCount != null)
            penaltyCount.text = count ?? "";
        if (penaltySum != null)
            penaltySum.text = rowTotal ?? "";
    }
}
