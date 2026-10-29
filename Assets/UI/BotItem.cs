using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerDragHandler))]
public class BotItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public TMP_Text playerName;
    public Image backgroundImage;
    public Image botImage;

    [Header("Buttons (optional, auto-find fallback)")]
    public Button renameButton;
    public Button deleteButton;

    private void Awake()
    {
        if (renameButton == null)
            renameButton = FindButtonByName("Rename");
        if (deleteButton == null)
            deleteButton = FindButtonByName("Delete");

        if (renameButton != null)
        {
            renameButton.onClick.RemoveListener(Rename);
            renameButton.onClick.AddListener(Rename);
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(Delete);
            deleteButton.onClick.AddListener(Delete);
        }
    }

    private Button FindButtonByName(string contains)
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            if (b != null && b.gameObject != null
                && b.gameObject.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                return b;
        }
        return null;
    }
    private Guid id;
    private object list;
    private SetupHandler menuSetupPanel;
    private PlayerDragHandler dragHandler;

    public Guid ID => id;

    public void Initialize(BasePlayer player)
    {
        Initialize(player, null);
    }

    public void Initialize(BasePlayer player, object listOwner)
    {
        if (player == null)
            return;

        if (!(player is DartBot bot))
            return;

        id = player.GetID();
        list = listOwner;

        dragHandler = GetComponent<PlayerDragHandler>();
        if (dragHandler != null)
            dragHandler.playerID = id;

        menuSetupPanel = GetComponentInParent<SetupHandler>();

        if (playerName != null)
            playerName.text = bot.GetNameWithDifficulty();

        if (botImage != null)
            botImage.gameObject.SetActive(true);

        ApplyBackgroundColor();
        ApplyDifficultyColor(bot.GetDifficulty());

        // Rename/Delete nur in PlayerList (IPlayerItemOwner), nicht in Setup.
        bool isEditable = list is IPlayerItemOwner;
        if (renameButton != null)
            renameButton.interactable = isEditable;
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(isEditable);
    }

    private void ApplyBackgroundColor()
    {
        if (backgroundImage == null || ThemeManager.Instance == null)
            return;

        backgroundImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.Background2);
    }

    private void ApplyDifficultyColor(DartBotDifficulty difficulty)
    {
        if (botImage == null || ThemeManager.Instance == null)
            return;

        botImage.color = ThemeManager.Instance.GetBotDifficultyColor(difficulty);
    }

    public void Rename()
    {
        if (list is IPlayerItemOwner owner)
            owner.RenameClicked(id);
    }

    public void Delete()
    {
        if (list is IPlayerItemOwner owner)
            owner.DeleteClicked(id);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragHandler != null && dragHandler.IsDraggingThisItem)
            return;

        if (menuSetupPanel != null)
            menuSetupPanel.TogglePlayerBetweenLists(id);
    }
}
