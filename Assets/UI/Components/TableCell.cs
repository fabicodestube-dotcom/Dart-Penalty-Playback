using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TableCell : MonoBehaviour
{
    public TextMeshProUGUI text;
    public Image image;
    public LayoutElement layoutElement;

    public void SetText(string t)
    {
        if (text != null)
        {
            text.text = t;
            text.enabled = true;
        }
        if (image != null)
            image.enabled = false;
    }

    public void SetIcon(Sprite s)
    {
        if (image != null)
        {
            image.sprite = s;
            image.enabled = true;
            image.preserveAspect = true;
        }
        if (text != null)
            text.enabled = false;
    }

    public void SetPreferredWidth(float w)
    {
        if (layoutElement != null)
        {
            layoutElement.preferredWidth = w;
            layoutElement.minWidth = w;
        }
    }
}
