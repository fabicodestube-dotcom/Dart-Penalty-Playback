using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SwipeMenuTopBar : MonoBehaviour
{
    public List<GameObject> tabs;

    private SwipeMenu menu;

    private int currentActiveIndex = 0;


    public void Init(SwipeMenu menu, int count)
    {
        this.menu = menu;

        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            tabs[i].GetComponent<UnityEngine.UI.Button>()
                .onClick.AddListener(() => menu.SetPage(index));
        }

        if (ThemeManager.Instance != null && ThemeManager.Instance.IsReady)
        {
            SetActive(currentActiveIndex);
            ThemeManager.Instance.OnThemeChanged += Apply;
        }
        else
        {
            StartCoroutine(WaitForThemeAndApply());
        }
    }

    private System.Collections.IEnumerator WaitForThemeAndApply()
    {
        // wartet bis ThemeManager existiert UND ready ist
        while (ThemeManager.Instance == null || !ThemeManager.Instance.IsReady)
        {
            yield return null; // wartet 1 Frame
        }

        SetActive(currentActiveIndex);
        ThemeManager.Instance.OnThemeChanged += Apply;
    }

    public void SetActive(int index)
    {
        currentActiveIndex = index;
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i].GetComponent<UnityEngine.UI.Image>().color = (i == index) ? 
                ThemeManager.Instance.GetColor(ThemeColorRole.Accent1) : ThemeManager.Instance.GetColor(ThemeColorRole.SingleSelectionButtonInactive);
            tabs[i].GetComponentInChildren<TMP_Text>().color = (i == index) ? 
                ThemeManager.Instance.GetColor(ThemeColorRole.TextOnAccent1) : ThemeManager.Instance.GetColor(ThemeColorRole.OnSingleSelectionButtonInactive);
        }
    }

    private void Apply()
    {
        SetActive(currentActiveIndex);
    }
}