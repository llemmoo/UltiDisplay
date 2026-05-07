using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class ImageProcessor : MonoBehaviour
{
    public int resultPort = 9002;

    // Other components read these directly
    [HideInInspector] public int?      confirmedScreenId = null;
    [HideInInspector] public float[]   homographyMatrix  = null;  // 9 floats, row-major

    public struct TouchEvent
    {
        public string type;       // "tap_down", "tap_hold", "tap_up"
        public int screenId;
        public float normX, normY;
    }

    public readonly Queue<TouchEvent> touchQueue = new Queue<TouchEvent>();

    private UdpClient                  _listener;
    private System.Threading.Thread    _thread;
    private readonly Queue<string>     _queue = new();

    void Start()
    {
        _listener = new UdpClient(resultPort);
        _thread   = new System.Threading.Thread(ListenLoop) { IsBackground = true };
        _thread.Start();
        Debug.Log("[ImageProcessor] Listening on port " + resultPort);
    }

    void Update()
    {
        lock (_queue)
        {
            while (_queue.Count > 0)
                ParseMessage(_queue.Dequeue());
        }
    }

    void ParseMessage(string json)
    {
        // Using JsonUtility-compatible structs to avoid Newtonsoft dependency
        // If you have Newtonsoft, you can use JObject.Parse instead
        var msg = JsonUtility.FromJson<PythonMessage>(json);

        if (msg.type == "homography")
        {
            homographyMatrix  = msg.matrix;
            confirmedScreenId = msg.screen_id >= 0 ? msg.screen_id : (int?)null;
            Debug.Log($"[ImageProcessor] Homography received, screen_id={confirmedScreenId}");
        }
        else if (msg.type == "touch")
        {
            touchQueue.Enqueue(new TouchEvent { type = msg.event_type, screenId = msg.screen_id, normX = msg.norm_x, normY = msg.norm_y });
            Debug.Log($"[ImageProcessor] Touch screen={msg.screen_id} ({msg.norm_x:F3}, {msg.norm_y:F3})");
        }
    }

    void ListenLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, resultPort);
        while (true)
        {
            try
            {
                var data = _listener.Receive(ref ep);
                var json = Encoding.UTF8.GetString(data);
                lock (_queue) { _queue.Enqueue(json); }
            }
            catch { break; }
        }
    }

    void OnDestroy()
    {
        _thread?.Abort();
        _listener?.Close();
    }

    // Must be serializable for JsonUtility
    [System.Serializable]
    private class PythonMessage
    {
        public string   type;
        public float[]  matrix;
        public int      screen_id = -1;  // -1 means null/unidentified
        public float[]  corners;
        public float    norm_x;
        public float    norm_y;
        public string   event_type;
    }
}