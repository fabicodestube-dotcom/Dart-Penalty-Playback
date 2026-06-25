using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class SetsAndLegsScrollView : MonoBehaviour
{
    public List<GameObject> evenButtons;
    public RectTransform rect;

    public void ShowEvenButtons(bool show)
    {
        foreach (var button in evenButtons)
        {
            button.SetActive(show);
        }

        rect.sizeDelta = new Vector2(rect.sizeDelta.x, show ? 1505 : 755);
    }
}
