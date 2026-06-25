using UnityEngine;
using UnityEngine.UI;

public class UIOnClick : MonoBehaviour
{
    public WindowHandler windowHandler;
    public ScreenId target;

    public void GoToTarget()
    {
        windowHandler.GoTo(target);
    }
}