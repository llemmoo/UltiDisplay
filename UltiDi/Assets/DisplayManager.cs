using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    void Start()
    {
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
            Debug.Log("Projector display activated");
        }
        else
        {
            Debug.LogWarning("No second display found");
        }
    }
}