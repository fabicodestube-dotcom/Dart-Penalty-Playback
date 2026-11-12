using System;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayersScreen : MonoBehaviour
{
    public AppHandler appHandler;
    public WindowHandler windowHandler;
    public PlayerList playerList;

    // Popups
    public UIScreen popupAddPlayer;
    public UIScreen popupRenamePlayer;
    public UIScreen popupDeletePlayer;

    public TMP_InputField addInput;
    public TMP_InputField renameInput;

    public TMP_Text renameHeadline;
    public TMP_Text deleteText;

    // Error texts
    public TMP_Text addErrorText;
    public TMP_Text renameErrorText;

    public Image addOutline;
    public Image renameOutline;

    // Buttons for interaction logic
    public Button addConfirmButton;
    public Button renameConfirmButton;

    private Guid currentPlayerID;
    private bool isResetting; // Prevents red flare-up when emptying


    void Start()
    {
        playerList.Initialize(this);

        RegisterAddPlayerEvent();

        SetupInputField(addInput, addOutline, addErrorText, addConfirmButton);
        SetupInputField(renameInput, renameOutline, renameErrorText, renameConfirmButton);

        RefreshList();
    }

    public void Onable()
    {
        RegisterAddPlayerEvent();       
    }

    public void Osable()
    {
        UnregisterAddPlayerEvent();      
    }

    private void RefreshList()
    {
        playerList.UpdateList(appHandler.GetPlayers());
    }

    // =========================
    // ADD / RENAME / DELETE (Logic)
    // =========================

    public void OpenAddPopup()
    {
        currentPlayerID = Guid.Empty;
        isResetting = true;
        addInput.text = "";
        addErrorText.text = "";
        addConfirmButton.interactable = false;
        isResetting = false;

        OpenPopup(popupAddPlayer);
    }

    public void ConfirmAdd()
    {
        if (TryValidate(addInput.text, out var name, out _))
        {
            appHandler.AddPlayer(name);
            CloseAddPopup();
            //RefreshList();
        }
    }

    public void CloseAddPopup()
    {
        isResetting = true;
        //addInput.text = "";
        ClosePopup();
        isResetting = false;
    }

    public void OpenRenamePopup(Guid id)
    {
        currentPlayerID = id;
        isResetting = true;
        
        renameErrorText.text = "";
        string name = appHandler.GetPlayerNameByID(id);
        renameHeadline.text = LocalizationText.Format("Rename_player", "Rename player {0}", name);
        renameInput.text = name;
        
        // Initial check (since the current name is valid)
        renameConfirmButton.interactable = TryValidate(name, out _, out _);
        
        isResetting = false;
        OpenPopup(popupRenamePlayer);
    }

    public void ConfirmRename()
    {
        if (TryValidate(renameInput.text, out var name, out _))
        {
            appHandler.RenamePlayer(currentPlayerID, name);
            CloseRenamePopup();
            RefreshList();
        }
    }

    public void CloseRenamePopup()
    {
        isResetting = true;
        //renameInput.text = "";
        //renameHeadline.text = "";
        ClosePopup();
        isResetting = false;
    }

    public void OpenDeletePopup(Guid id)
    {
        currentPlayerID = id;
        string name = appHandler.GetPlayerNameByID(id);
        deleteText.text = LocalizationText.Format("Delete_player_expl", "Are you sure you want to delete player {0}? This action cannot be undone!", name);
        OpenPopup(popupDeletePlayer);
    }

    public void ConfirmDelete()
    {
        appHandler.DeletePlayer(currentPlayerID);
        CloseDeletePopup();
        RefreshList();
    }

    public void CloseDeletePopup()
    {
        //deleteText.text = "";
        ClosePopup();
    }

    // =========================
    // HELPERS & VALIDATION
    // =========================

    private void OpenPopup(UIScreen popup)
    {
        windowHandler.ShowPopup(popup);
    }

    private void ClosePopup()
    {
        windowHandler.HidePopup();
    }

    private void SetupInputField(TMP_InputField input, Image outline, TMP_Text errorField, Button confirmButton)
    {
        input.onSelect.AddListener(_ =>
            outline.color = ThemeManager.Instance.GetColor(ThemeColorRole.Accent1));

        input.onDeselect.AddListener(_ => {
            // Reset to Inactive Color only if the input is valid
            if (TryValidate(input.text, out _, out _))
                outline.color = ThemeManager.Instance.GetColor(ThemeColorRole.SingleSelectionButtonInactive);
        });

        input.onValueChanged.AddListener(val => {
            if (isResetting) return;

            bool isValid = TryValidate(val, out _, out string errorMessage);
            
            outline.color = isValid 
                ? ThemeManager.Instance.GetColor(ThemeColorRole.Accent1) 
                : ThemeManager.Instance.GetColor(ThemeColorRole.Error);

            errorField.text = isValid ? "" : errorMessage;
            errorField.color = ThemeManager.Instance.GetColor(ThemeColorRole.Error);
            
            if (confirmButton != null)
                confirmButton.interactable = isValid;
        });
    }

    private bool TryValidate(string input, out string result, out string errorMessage)
    {
        errorMessage = "";
        
        // 1. Cleanup
        result = Regex.Replace(input.Trim(), @"\s+", " ");
        result = Regex.Replace(result, @"[^\w\säöüÄÖÜß]", "");

        // 2. Longitudinal test
        if (string.IsNullOrWhiteSpace(result) || result.Length < 2)
        {
            errorMessage = "Name is too short (min. 2 characters).";
            return false;
        }

        if (result.Length > 12)
        {
            errorMessage = "Name is too long (max. 12 characters).";
            return false;
        }

        // Check for at least one letter
        if (!Regex.IsMatch(result, @"\p{L}"))
        {
            errorMessage = "Name must contain at least one letter.";
            return false;
        }

        // 4. Uniqueness check
        string cleanedName = result;
        bool isDuplicate = appHandler.GetPlayers()
            .Any(p => p.GetName().Equals(cleanedName, System.StringComparison.OrdinalIgnoreCase) 
                 && p.GetID() != currentPlayerID);

        if (isDuplicate)
        {
            errorMessage = "Name bereits vergeben.";
            return false;
        }

        return true;
    }

    private void RegisterAddPlayerEvent()
    {
        if (appHandler == null)
        {
            appHandler = FindFirstObjectByType<AppHandler>();
        }

        if (appHandler == null)
            return;

        appHandler.OnPlayerAdded += AddDartBotHelper;
    }

    private void UnregisterAddPlayerEvent()
    {
        if (appHandler == null)
        {
            appHandler = FindFirstObjectByType<AppHandler>();
        }

        if (appHandler == null)
            return;
    }

    private void AddDartBotHelper(Guid g)
    {
        RefreshList();
    }
}
