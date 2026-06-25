using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System;

[RequireComponent(typeof(PlayerDragHandler))]
public class SetupPlayerItem : MonoBehaviour, IPointerClickHandler
{
    public TMP_Text playerName;
    public Image backgroundImage;
    public Image dartIcon;

    private Guid id;
    private SetupHandler menuSetupPanel;

    public void Initialize(BasePlayer p)
    {
        this.id = p.GetID();

        GetComponent<PlayerDragHandler>().playerID = id;

        // Finde MenuSetupPanel in der Hierarchie
        menuSetupPanel = GetComponentInParent<SetupHandler>();

        if (p is DartBot bot)
        {
            playerName.text = bot.GetNameWithDifficulty();

            // Prefer coloring the dartIcon if present, otherwise fall back to backgroundImage
            if (dartIcon != null)
            {
                switch (bot.GetDifficulty())
                {
                    case DartBotDifficulty.Easy:
                        dartIcon.color = new Color(0.4f, 0.85f, 0.4f); // light green
                        break;
                    case DartBotDifficulty.Medium:
                        dartIcon.color = Color.yellow;
                        break;
                    case DartBotDifficulty.Hard:
                        dartIcon.color = new Color(1f, 0.6f, 0f); // orange
                        break;
                    case DartBotDifficulty.Pro:
                        dartIcon.color = Color.red;
                        break;
                    default:
                        if (backgroundImage != null) backgroundImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.Background2);
                        break;
                }
            }
            else
            {
                if (backgroundImage != null) backgroundImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.Background2);
            }
        }
        else
        {
            backgroundImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.Accent1);
            playerName.text = p.GetName();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Nur bei kurzem Click reagieren, nicht beim Drag
        if (GetComponent<PlayerDragHandler>().IsDraggingThisItem)
            return;

        if (menuSetupPanel != null)
        {
            menuSetupPanel.TogglePlayerBetweenLists(id);
        }
    }
}