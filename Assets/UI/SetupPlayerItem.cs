
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerDragHandler))]
public class SetupPlayerItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public TMP_Text playerName;
    public Image backgroundImage;
    public GameObject playerImage;
    public GameObject botImage;

    private Guid id;
    private SetupHandler menuSetupPanel;
    private PlayerDragHandler dragHandler;

    public void Initialize(BasePlayer player)
    {
        if (player == null)
            return;

        id = player.GetID();

        dragHandler = GetComponent<PlayerDragHandler>();
        dragHandler.playerID = id;

        menuSetupPanel = GetComponentInParent<SetupHandler>();

        bool isBot = player is DartBot;

        if (playerName != null)
        {
            playerName.text = isBot
                ? ((DartBot)player).GetNameWithDifficulty()
                : player.GetName();
        }

        if (playerImage != null)
            playerImage.SetActive(!isBot);

        if (botImage != null)
            botImage.SetActive(isBot);

        ApplyBackgroundColor(isBot);

        if (isBot)
            ApplyDifficultyColor(((DartBot)player).GetDifficulty());
    }

    private void ApplyBackgroundColor(bool isBot)
    {
        if (backgroundImage == null || ThemeManager.Instance == null)
            return;

        backgroundImage.color = ThemeManager.Instance.GetColor(
            isBot
                ? ThemeColorRole.Background2
                : ThemeColorRole.Accent1);
    }

    private void ApplyDifficultyColor(DartBotDifficulty difficulty)
    {
        if (botImage == null)
            return;

        Image image = botImage.GetComponent<Image>();

        if (image == null)
            return;

        switch (difficulty)
        {
            case DartBotDifficulty.Easy:
                image.color = new Color(0.4f, 0.85f, 0.4f);
                break;

            case DartBotDifficulty.Medium:
                image.color = Color.yellow;
                break;

            case DartBotDifficulty.Hard:
                image.color = new Color(1f, 0.6f, 0f);
                break;

            case DartBotDifficulty.Pro:
                image.color = Color.red;
                break;

            default:
                image.color = Color.clear;
                break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ignore clicks that are part of a drag operation.
        if (dragHandler != null && dragHandler.IsDraggingThisItem)
            return;

        if (menuSetupPanel != null)
            menuSetupPanel.TogglePlayerBetweenLists(id);
    }
}
