using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuSwitcher : MonoBehaviour
{
    [System.Serializable]
    private class MenuItem
    {
        public Image image;
        public TMP_Text text;
    }

    [SerializeField] private MenuItem game;
    [SerializeField] private MenuItem players;
    [SerializeField] private MenuItem history;
    [SerializeField] private MenuItem statistics;

    private MenuItem _active;

    private void Start()
    {
        ThemeManager.Instance.OnThemeChanged += ApplyTheme;

        // Default
        _active = game; // ❗ nur State, kein Apply

        if (ThemeManager.Instance.ActiveTheme != null)
        {
            ApplyTheme(); // optional initial sync
        }
    }

    private void OnDestroy()
    {
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnThemeChanged -= ApplyTheme;
    }

    // ================= BUTTON EVENTS =================

    public void OnClickGameButton() => SetActive(game);
    public void OnClickPlayersButton() => SetActive(players);
    public void OnClickOptionsButton() => SetActive(history);
    public void OnClickStatisticsButton() => SetActive(statistics);

    public void SetActiveByScreen(ScreenId screenId)
    {
        switch (screenId)
        {
            case ScreenId.Zoggen:
                SetActive(game);
                break;
            case ScreenId.Players:
                SetActive(players);
                break;
            case ScreenId.History:
                SetActive(history);
                break;
            case ScreenId.Statistic:
                SetActive(statistics);
                break;
            default:
                // For non-bottom-bar screens (X01Game, Summary, GameDetail, etc.) keep current selection.
                break;
        }
    }
    

    // ================= CORE =================

    private void SetActive(MenuItem item)
    {
        _active = item;

        if (ThemeManager.Instance.ActiveTheme != null)
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        ApplyItem(game);
        ApplyItem(players);
        ApplyItem(history);
        ApplyItem(statistics);
        
    }

    private void ApplyItem(MenuItem item)
    {
        bool isActive = item == _active;

        if (isActive)
        {
            item.image.color = ThemeManager.Instance.GetColor(ThemeColorRole.Accent1);
            item.text.color  = ThemeManager.Instance.GetColor(ThemeColorRole.Accent1); // oder TextOnAccent1
        }
        else
        {
            item.image.color = ThemeManager.Instance.GetColor(ThemeColorRole.BottomBarInactive);
            item.text.color  = ThemeManager.Instance.GetColor(ThemeColorRole.BottomBarInactive);
        }
    }
}