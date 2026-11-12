using TMPro;
using UnityEngine;

/// <summary>
/// Eine Zeile im Penalty-Detail-Popup: (Name | Kosten | Anzahl | Zeilensumme).
/// Befüllung ausschließlich über Bind() (Popup-Code); Felder im Prefab verdrahtet.
/// </summary>
public class PenaltyDetailPopupItem : MonoBehaviour
{
    [Header("Zellen")]
    public TMP_Text penaltyName;
    public TMP_Text penaltyCount;
    public TMP_Text penaltyCosts;
    public TMP_Text penaltySum;

    /// <summary>Beschreibt die Zeile. Alle Werte bereits als Anzeige-Strings.</summary>
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
