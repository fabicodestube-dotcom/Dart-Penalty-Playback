using UnityEngine;
using System.Collections.Generic; // Ermöglicht die Nutzung von Listen/Arrays

public class LanguagePopup : MonoBehaviour
{
    public OptionsHandler optionsHandler;

    [Tooltip("Add all your language buttons here in the Inspector")]
    public List<LanguageButton> languageButtons = new List<LanguageButton>();

    private GameObject currentlyActiveBorder;

    // Wird von Unity aufgerufen, wenn das Skript geladen wird
    private void Start()
    {
        InitializeCurrentLanguageSelection();
    }

    /// <summary>
    /// Checks the currently saved language and highlights the corresponding button.
    /// </summary>
    private void InitializeCurrentLanguageSelection()
    {
        // 1. Hole den aktuell aktiven Sprachcode (z. B. aus deinem AppSettingsManager)
        // HINWEIS: Passe diesen Aufruf an, je nachdem wie deine Methode im AppSettingsManager genau heißt!
        string currentLocale = AppSettingsManager.Instance.Settings.Language.lastLocaleCode; 

        // 2. Durchsuche alle registrierten Buttons nach dem passenden Sprachcode
        foreach (LanguageButton button in languageButtons)
        {
            if (button != null && string.Equals(button.localeCode, currentLocale, System.StringComparison.OrdinalIgnoreCase))
            {
                // Wenn der richtige Button gefunden wurde, aktiviere seine Umrandung
                if (button.borderObject != null)
                {
                    button.borderObject.SetActive(true);
                    currentlyActiveBorder = button.borderObject;
                }
                break; // Schleife abbrechen, da wir den passenden Button gefunden haben
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


