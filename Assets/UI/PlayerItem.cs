using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface IPlayerItemOwner
{
    void RenameClicked(Guid id);
    void DeleteClicked(Guid id);
}

[RequireComponent(typeof(PlayerDragHandler))]
public class PlayerItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public TMP_Text playerName;
    public Image backgroundImage;
    public GameObject playerImage;

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

        id = player.GetID();
        list = listOwner;

        dragHandler = GetComponent<PlayerDragHandler>();
        if (dragHandler != null)
            dragHandler.playerID = id;

        menuSetupPanel = GetComponentInParent<SetupHandler>();

        if (playerName != null)
            playerName.text = player.GetName();

        if (playerImage != null)
            playerImage.SetActive(true);

        ApplyBackgroundColor();

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

        backgroundImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.Accent1);
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
