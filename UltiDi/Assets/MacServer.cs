using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class MacServer : MonoBehaviour
{
    private UdpClient udpClient;
    private Thread receiveThread;
    private string latestMessage;
    private bool newMessageAvailable = false;
    public int port = 9000;
    private Dictionary<int, byte[][]> frameBuffer = new Dictionary<int, byte[][]>();
    private Dictionary<int, int> receivedChunks = new Dictionary<int, int>();

    // Latest received frame data
    [HideInInspector] public byte[] latestFrameData;
    [HideInInspector] public Vector2 latestTouchPosition;
    public bool newFrameAvailable = false;

    void Start()
    {
        udpClient = new UdpClient(port);
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"Server listening on port {port}");
    }

    private void ReceiveLoop()
{
    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
    while (true)
    {
        try
        {
            byte[] data = udpClient.Receive(ref endPoint);

            if (data[0] == 0x01)
            {
                // Parse header
                int fID = System.BitConverter.ToInt32(data, 1);
                int chunkIndex = System.BitConverter.ToUInt16(data, 5);
                int totalChunks = System.BitConverter.ToUInt16(data, 7);
                byte[] chunkData = data[9..];

                // Initialize buffer for this frame
                if (!frameBuffer.ContainsKey(fID))
                {
                    frameBuffer[fID] = new byte[totalChunks][];
                    receivedChunks[fID] = 0;
                }

                // Store chunk
                frameBuffer[fID][chunkIndex] = chunkData;
                receivedChunks[fID]++;

                // Check if frame is complete
                if (receivedChunks[fID] == totalChunks)
                {
                    // Reassemble
                    List<byte> fullFrame = new List<byte>();
                    foreach (var chunk in frameBuffer[fID])
                        fullFrame.AddRange(chunk);

                    latestFrameData = fullFrame.ToArray();
                    newFrameAvailable = true;

                    // Cleanup old frames
                    frameBuffer.Remove(fID);
                    receivedChunks.Remove(fID);

                    Debug.Log($"Frame {fID} reassembled — {latestFrameData.Length} bytes");
                }
            }
            else if (data[0] == 0x02)
            {
                float x = System.BitConverter.ToSingle(data, 1);
                float y = System.BitConverter.ToSingle(data, 5);
                latestTouchPosition = new Vector2(x, y);
            }
            else
            {
                string message = System.Text.Encoding.UTF8.GetString(data);
                latestMessage = message;
                newMessageAvailable = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ReceiveLoop error: {e.Message}");
            break;
        }
    }
}

    void Update()
    {
        if (newFrameAvailable)
        {
            newFrameAvailable = false;
            // Pass latestFrameData to your OpenCV processor here
        }
        if (newMessageAvailable)
        {
            newMessageAvailable = false;
            Debug.Log($"Received: {latestMessage}");
        }
    }

    void OnDestroy()
    {
        receiveThread?.Abort();
        udpClient?.Close();
    }
}