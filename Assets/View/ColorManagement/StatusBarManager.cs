using UnityEngine;

public class StatusBarManager : MonoBehaviour
{

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Set status bar color to transparent
        SetBarColor(Color.clear, "setStatusBarColor");
        // Clear full screen flags
        ClearFlags(1024); // WindowManager.LayoutParams.FLAG_FULLSCREEN = 1024
#endif
    }

    void SetBarColor(Color color, string methodName)
    {
        RunOnUiThread(() => GetWindow().Call(methodName, ColorToARGB(color)));
    }

    void ClearFlags(int flags)
    {
        RunOnUiThread(() => GetWindow().Call("clearFlags", flags));
    }

    AndroidJavaObject GetWindow()
    {
        AndroidJavaClass windowClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = windowClass.GetStatic<AndroidJavaObject>("currentActivity");
        return activity.Call<AndroidJavaObject>("getWindow");
    }

    void RunOnUiThread(System.Action action)
    {
        AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
        activity.Call("runOnUiThread", new AndroidJavaRunnable(action));
    }

    int ColorToARGB(Color32 color)
    {
        int value = 0;
        value |= color.a << 24; // Alpha
        value |= color.r << 16;
        value |= color.g << 8;
        value |= color.b;
        return value;
    }
}