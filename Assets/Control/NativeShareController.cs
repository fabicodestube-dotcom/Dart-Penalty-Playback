using UnityEngine;

public class NativeShareController : MonoBehaviour
{
    public WindowHandler windowHandler;

    // Replace 'your.app.id' with your actual app package ID from the Build Settings
    private string playStoreUrl = "https://github.com/fabicodestube-dotcom/Dart-Penalty-Playback/";
    private string shareMessage = "Check out my new Darts App that I found on GitHub! ";

    public void Start()
    {
        EnsureWindowHandler();
    }

    public void OnShareButtonClick()
    {
        string fullTextToShare = shareMessage + playStoreUrl;

        #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent"))
            {
                // Sets the Android action to share (SEND)
                intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                
                // Set type to text/plain for messengers like WhatsApp and Discord
                intentObject.Call<AndroidJavaObject>("setType", "text/plain");
                
                // Pack the text and Play Store link into the Android intent
                intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), fullTextToShare);

                // Opens the native Android chooser menu ("Share via...") at the bottom of the screen
                using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "Share via..."))
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        currentActivity.Call("startActivity", chooser);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error during native Android sharing: " + e.Message);
        }
        #elif UNITY_EDITOR
        // Testing output only for you in the Unity Editor
        Debug.Log("[Editor Test] Share button pressed. The following text would be sent: " + fullTextToShare);
        #endif

        windowHandler.ToggleQuickMenu();   
    }

    private void EnsureWindowHandler()
    {
        if (windowHandler == null)
        {
            windowHandler = FindFirstObjectByType<WindowHandler>();
            if (windowHandler == null)
            {
                Debug.LogError("WindowHandler not found! Please ensure that a WindowHandler is present in the scene.");
            }
        }
    }
}