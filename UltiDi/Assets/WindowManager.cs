using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// Manages draggable windows across two displays.
/// 
/// SETUP:
///   1. Add this component to any persistent GameObject (e.g. your DisplayManager).
///   2. Assign imageProcessor reference in inspector.
///   3. Assign display0Canvas and display1Canvas (your two Canvas GameObjects).
///   4. Call CreateWindow() at Start or manually to spawn windows.
///
/// Each window is a RectTransform child of whichever display canvas it lives on.
/// On grab it becomes invisible. On drop it re-appears on the target display.

public class WindowManager : MonoBehaviour
{
    [Header("References")]
    public ImageProcessor imageProcessor;
    public Canvas         display0Canvas;   // targetDisplay = 0
    public Canvas         display1Canvas;   // targetDisplay = 1

    [Header("Window appearance")]
    public int   windowWidth  = 300;
    public int   windowHeight = 200;

    // Internal state
    private readonly List<DraggableWindow> _windows = new();
    private DraggableWindow _grabbed = null;
    private Vector2 _grabOffset;

    // -------------------------------------------------------------------

    void Start()
    {
        // Spawn one window on display 0 to start
        CreateWindow("Window A", new Color(0.2f, 0.5f, 1f), 0, 0.5f, 0.5f);
    }

    void Update()
    {
        if (imageProcessor == null) return;
        
        // At the top of WindowManager.Update(), temporarily:
        if (imageProcessor.touchQueue.Count > 0)
            Debug.Log($"[WindowManager] Processing {imageProcessor.touchQueue.Count} touch events");
        
        while (imageProcessor.touchQueue.Count > 0)
        {
            var touch = imageProcessor.touchQueue.Dequeue();

            if (touch.type == "tap_down")
            {
                _grabbed = FindWindowAt(touch.screenId, touch.normX, touch.normY);
                if (_grabbed != null)
                {
                    var rt = _grabbed.GetComponent<RectTransform>();
                    // Store offset from window center to touch point (in norm space)
                    _grabOffset = new Vector2(touch.normX - rt.anchorMin.x, touch.normY - (1f - rt.anchorMin.y));
                    _grabbed.SetVisible(false);
                }
            }
            else if (touch.type == "tap_up" && _grabbed != null)
            {
                Canvas targetCanvas = touch.screenId == 0 ? display0Canvas : display1Canvas;
                DropWindow(_grabbed, targetCanvas, touch.normX - _grabOffset.x, touch.normY - _grabOffset.y);
                Debug.Log($"[WindowManager] Dropped {_grabbed.label} at ({touch.normX:F2},{touch.normY:F2})");
                _grabbed = null;
            }
        }
    }

    // -------------------------------------------------------------------
    // Public API

    public DraggableWindow CreateWindow(string label, Color color, int displayIndex, float normX, float normY)
    {
        Canvas canvas = displayIndex == 0 ? display0Canvas : display1Canvas;
        if (canvas == null) { Debug.LogError("[WindowManager] Canvas not assigned"); return null; }

        var go  = new GameObject($"Window_{label}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var img   = go.GetComponent<Image>();
        img.color = color;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(windowWidth, windowHeight);
        SetNormalisedPosition(rt, canvas, normX, normY);

        // Label
        var labelGO  = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var tmp = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 24;
        tmp.color     = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin         = Vector2.zero;
        lrt.anchorMax         = Vector2.one;
        lrt.offsetMin         = Vector2.zero;
        lrt.offsetMax         = Vector2.zero;

        var win = go.AddComponent<DraggableWindow>();
        win.label = label;
        _windows.Add(win);
        return win;
    }

    // -------------------------------------------------------------------
    // Private helpers

    private DraggableWindow FindWindowAt(int screenId, float nx, float ny)
    {
        Canvas canvas = screenId == 0 ? display0Canvas : display1Canvas;
        if (canvas == null) return null;

        var canvasRect = canvas.GetComponent<RectTransform>();
        var canvasSize = canvasRect.rect.size;

        // Convert norm touch (y=0 top) to canvas local space (y=0 bottom-left)
        Vector2 touchInCanvas = new Vector2(
            nx * canvasSize.x,
            (1f - ny) * canvasSize.y   // flip Y since canvas local has y=0 at bottom
        );

        foreach (var w in _windows)
        {
            if (w.transform.parent != canvas.transform) continue;

            var rt = w.GetComponent<RectTransform>();
            // Get window center in canvas local space
            Vector2 windowCenter = new Vector2(
                rt.anchorMin.x * canvasSize.x,
                rt.anchorMin.y * canvasSize.y   // anchorMin.y = 1f - ny, already flipped
            );

            float halfW = rt.sizeDelta.x * 0.5f;
            float halfH = rt.sizeDelta.y * 0.5f;

            if (Mathf.Abs(touchInCanvas.x - windowCenter.x) < halfW &&
                Mathf.Abs(touchInCanvas.y - windowCenter.y) < halfH)
            {
                return w;
            }
        }
        return null;
    }

    private void DropWindow(DraggableWindow win, Canvas targetCanvas, float nx, float ny)
    {
        var rt = win.GetComponent<RectTransform>();
        rt.SetParent(targetCanvas.transform, false);
        SetNormalisedPosition(rt, targetCanvas, nx, ny);
        win.SetVisible(true);
    }

    private static void SetNormalisedPosition(RectTransform rt, Canvas canvas, float nx, float ny)
    {
        // Anchor to a point, offset zero — cleanest for multi-display
        rt.anchorMin         = new Vector2(nx, 1f - ny);
        rt.anchorMax         = new Vector2(nx, 1f - ny);
        rt.pivot             = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition  = Vector2.zero;
    }

    private static Vector2 NormToCanvasLocal(RectTransform canvasRect, float nx, float ny)
    {
        var rect = canvasRect.rect;
        return new Vector2(rect.x + nx * rect.width, rect.y + (1f - ny) * rect.height);
    }

    private static Vector2 NormToScreenPoint(Canvas canvas, float nx, float ny)
    {
        var display = Display.displays[canvas.targetDisplay];
        return new Vector2(nx * display.renderingWidth, (1f - ny) * display.renderingHeight);
    }
}