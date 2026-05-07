using UnityEngine;

/// Thin component attached to each draggable window GameObject.
/// WindowManager drives all the logic — this just holds state.
public class DraggableWindow : MonoBehaviour
{
    public string label;

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}