using UnityEngine;
using System.Collections.Generic; // Allows the use of lists/arrays

public class LanguagePopup : MonoBehaviour
{
    public OptionsScreen optionsHandler;

    [Tooltip("Add all your language buttons here in the Inspector")]
    public List<LanguageButton> languageButtons = new List<LanguageButton>();

    private GameObject currentlyActiveBorder;

    // Called by Unity when the script is loaded
    private void Start()
    {
        InitializeCurrentLanguageSelection();
    }

    /// <summary>
    /// Checks the currently saved language and highlights the corresponding button.
    /// </summary>
    private void InitializeCurrentLanguageSelection()
    {
        // Get the currently active language code (e.g. from your AppSettingsManager)
        // NOTE: Adjust this call depending on what your method is called in AppSettingsManager!
        string currentLocale = AppSettingsManager.Instance.Settings.Language.lastLocaleCode; 

        // Search all registered buttons for the appropriate language code
        foreach (LanguageButton button in languageButtons)
        {
            if (button != null && string.Equals(button.localeCode, currentLocale, System.StringComparison.OrdinalIgnoreCase))
            {
                // If the correct button has been found, activate its border
                if (button.borderObject != null)
                {
                    button.borderObject.SetActive(true);
                    currentlyActiveBorder = button.borderObject;
                }
                break; // Cancel the loop, as we have found the right button
            }
        }
    }

    /// <summary>
    /// Called via the Button's OnClick event in the Inspector.
    /// </summary>
    public void SelectLanguage(LanguageButton clickedButton)
    {
        if (clickedButton == null) return;

        if (clickedButton.borderObject == null)
        {
            Debug.LogWarning($"No border object assigned to {clickedButton.gameObject.name}!");
            return;
        }

        if (currentlyActiveBorder == clickedButton.borderObject) return;

        if (currentlyActiveBorder != null)
        {
            currentlyActiveBorder.SetActive(false);
        }

        clickedButton.borderObject.SetActive(true);
        currentlyActiveBorder = clickedButton.borderObject;

        optionsHandler.SetLanguage(clickedButton.localeCode);
    }
}


