using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to a Canvas GameObject that targets a specific display.
/// It creates a full-screen overlay that modulates brightness at a
/// display-specific frequency, invisible to the human eye but detectable
/// by a camera doing temporal FFT analysis.
///
/// Display 0 (Monitor):   toggles every 3 frames → ~10Hz at 60fps
/// Display 1 (Projector): toggles every 6 frames → ~5Hz  at 60fps
///
/// SETUP:
///   1. Create two empty GameObjects in your scene: "Beacon_Monitor", "Beacon_Projector"
///   2. Add a Canvas component to each. Set Render Mode = Screen Space Overlay.
///      Set Target Display to 0 and 1 respectively.
///   3. Add this component to each. Set displayIndex to match.
///   4. Make sure a CanvasScaler and GraphicRaycaster are NOT required —
///      this canvas is purely visual, no interaction needed.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class StructuredLightBeacon : MonoBehaviour
{
    [Header("Which display this beacon belongs to (0 = monitor, 1 = projector)")]
    public int displayIndex = 0;

    [Header("Alpha when overlay is ON — keep this very low (0.015–0.025)")]
    [Range(0.005f, 0.05f)]
    public float alphaOn = 0.02f;

    // How many Unity frames each half-cycle lasts, per display.
    // Display 0: half-period = 3 frames → full cycle = 6 frames → ~10Hz at 60fps
    // Display 1: half-period = 6 frames → full cycle = 12 frames → ~5Hz  at 60fps
    private static readonly int[] HalfPeriodsByDisplay = { 3, 6 };

    private RawImage _overlay;
    private int _halfPeriod;
    private Canvas _canvas;

    void Awake()
    {
        _halfPeriod = HalfPeriodsByDisplay[Mathf.Clamp(displayIndex, 0, HalfPeriodsByDisplay.Length - 1)];

        // Configure the canvas to target the correct physical display
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.targetDisplay = displayIndex;
        _canvas.sortingOrder = 32767; // Always on top

        // Build the full-screen white overlay at runtime
        var overlayGO = new GameObject("_StructuredLightOverlay");
        overlayGO.transform.SetParent(transform, false);
        overlayGO.layer = gameObject.layer;

        _overlay = overlayGO.AddComponent<RawImage>();
        _overlay.color = new Color(1f, 1f, 1f, 0f);
        _overlay.raycastTarget = false; // Don't block UI interaction

        // Stretch to fill the entire canvas
        var rt = _overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void Update()
    {
        // Square wave: ON for halfPeriod frames, OFF for halfPeriod frames, repeat
        bool isOn = (Time.frameCount / _halfPeriod) % 2 == 0;
        _overlay.color = new Color(1f, 1f, 1f, isOn ? alphaOn : 0f);
    }

    // Call this at runtime to confirm the beacon is alive and check its state.
    // Useful for debugging — hook into OnGUI or a debug panel.
    public string GetDebugInfo()
    {
        bool isOn = (Time.frameCount / _halfPeriod) % 2 == 0;
        float hz = (Application.targetFrameRate > 0 ? Application.targetFrameRate : 60f) / (_halfPeriod * 2f);
        return $"Display {displayIndex} | {hz:F1}Hz | frame={Time.frameCount} | {(isOn ? "ON " : "off")}";
    }
}
