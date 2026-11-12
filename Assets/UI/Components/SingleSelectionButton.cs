using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleSelectionButton : MonoBehaviour
{
    public TMP_Text buttonLabel;
    public Image backgroundImage;

    private SingleSelectionGroup _group;

    public void Init(SingleSelectionGroup group)
    {
        _group = group;
    }

    public void OnClick()
    {
        _group.SetActive(this);
    }

    public void Apply(bool isActive)
    {
        if (isActive)
        {
            backgroundImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.Accent1);
            buttonLabel.color     = ThemeManager.Instance.GetColor(ThemeColorRole.TextOnAccent1);
        }
        else
        {
            backgroundImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.SingleSelectionButtonInactive);
            buttonLabel.color     = ThemeManager.Instance.GetColor(ThemeColorRole.OnSingleSelectionButtonInactive);
        }
    }
}