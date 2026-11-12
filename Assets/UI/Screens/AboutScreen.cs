using UnityEngine;

public class AboutScreen : MonoBehaviour
{
    public WindowHandler windowHandler;

    // Define your links here in the Inspector or directly in the code
    [Header("Web Links")]
    public string githubUrl = "https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback";
    public string kofiUrl = "https://ko-fi.com/fabiscodestube";
    public string privacyPolicyUrl = "https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback/blob/main/PRIVACY_POLICY.md";
    public string licenseUrl = "https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback/blob/main/LICENSE";

// ================= UNITY LIFECYCLE =================

    public void OnGoBackClicked()
    {
        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();

        if (windowHandler != null)
        {
            windowHandler.GoBack();
        }
        else
        {
            Debug.LogError("Kein WindowHandler in der Szene gefunden.");
        }
    }

// ================= PUBLIC METHODS =================

    public void OpenGithub()
    {
        OpenBrowser(githubUrl);
    }

    public void OpenKoFi()
    {
        OpenBrowser(kofiUrl);
    }

    public void OpenPrivacyPolicy()
    {
        OpenBrowser(privacyPolicyUrl);
    }

    public void OpenLicense()
    {
        OpenBrowser(licenseUrl);
    }

// ================= PRIVATE HELPERS =================

    // A small helper method to keep the code clean and catch errors
    private void OpenBrowser(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
        else
        {
            Debug.LogWarning("Der eingegebene Link ist leer!");
        }
    }












}
