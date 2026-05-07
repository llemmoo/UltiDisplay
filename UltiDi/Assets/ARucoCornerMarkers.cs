using UnityEngine;
using UnityEngine.UI;

/// Renders 4 ArUco marker images at the corners of a display canvas.
/// Display 0 uses marker IDs 0-3, Display 1 uses marker IDs 4-7.
/// 
/// SETUP:
///   1. Generate ArUco marker PNGs (IDs 0-7, DICT_4X4_50) from:
///      https://chev.me/arucogen/
///      Save as aruco_0.png ... aruco_7.png into Assets/Resources/Aruco/
///   2. Create two Canvas GameObjects, one per display.
///      Set each Canvas: Render Mode = Screen Space Overlay, Target Display = 0 or 1.
///   3. Attach this component. Set displayIndex accordingly.

[RequireComponent(typeof(Canvas))]
public class ArucoCornerMarkers : MonoBehaviour
{
    public int displayIndex = 0;

    [Tooltip("Size of each marker in pixels")]
    public int markerSize = 80;

    [Tooltip("Offset from screen edge in pixels")]
    public int margin = 10;

    void Start()
    {
        Debug.Log($"Setting up ARuco detectors$");
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = displayIndex;
        canvas.sortingOrder = 32767;

        // IDs 0-3 for display 0, IDs 4-7 for display 1
        int baseId = displayIndex * 4;

        // Corner anchors: top-left, top-right, bottom-right, bottom-left
        Vector2[] anchors = {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0),
        };
        Vector2[] pivots = {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0),
        };

        for (int i = 0; i < 4; i++)
        {
            int markerId = baseId + i;
            
            
            var go = new GameObject($"Marker_{markerId}", typeof(RawImage));
            go.transform.SetParent(transform, false);

            var img = go.GetComponent<RawImage>();
            img.raycastTarget = false;
            
            Texture2D tex = Resources.Load<Texture2D>($"Aruco/Aruco_{markerId}");
            if (tex != null)
            {
                img.texture = tex;
                img.raycastTarget = false;
            }
            else
            {
                img.color = Color.blue; // no continue — still creates the GameObject
                Debug.LogError($"[ArUco] Missing: Resources/Aruco/Aruco_{markerId}");
            }
            
            
            var rt = img.rectTransform;
            rt.anchorMin = anchors[i];
            rt.anchorMax = anchors[i];
            rt.pivot     = pivots[i];
            rt.sizeDelta = new Vector2(markerSize, markerSize);

            // Offset inward from the corner
            float mx = (i == 0 || i == 3) ?  margin : -margin;
            float my = (i == 0 || i == 1) ? -margin :  margin;
            rt.anchoredPosition = new Vector2(mx, my);

        }
    }
}