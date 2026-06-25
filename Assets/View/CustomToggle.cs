using UnityEngine;

public class CustomToggle : MonoBehaviour
{
    public GameObject activeGO;
    public GameObject inactiveGO;

    private bool isActive;


    public void Initialize(bool activeStart)
    {
        if (activeStart != isActive)
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        isActive = !isActive;
        activeGO.SetActive(isActive);
        inactiveGO.SetActive(!isActive);
    }

    public bool IsActive()
    {
        return isActive;
    }
}
