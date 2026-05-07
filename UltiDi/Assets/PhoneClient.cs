using System;
using UnityEngine;
using System.Net.Sockets;
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class PhoneClient : MonoBehaviour
{
    private UdpClient _udpClient;
    //private readonly string _imageServerAddress = "10.192.45.144";
    private readonly string _imageServerAddress = "192.168.0.121";
    private readonly int _macPort = 9001;
    
    private readonly float _sendInterval = 0.1f; // 10fps
    private float _timer;
    private int _frameID;
    private const int ChunkSize = 8192;

    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private TMP_Text frameStatusText;
    [SerializeField] private TMP_Text touchStatusText;
    
    // Drag overlay — wire up a full-screen Canvas with a RawImage child in the inspector
    // The RawImage should be a small colored square (e.g. 120x120, centered)
    [SerializeField] private GameObject dragOverlay;
    
    private bool _isTouching = false;
    
    private void Start()
    {
        _udpClient = new UdpClient();
        cameraManager.frameReceived += OnCameraFrameReceived;
        if (dragOverlay != null) dragOverlay.SetActive(false);
        frameStatusText.text = "Ready...";
        touchStatusText.text = "Waiting for touch input...";
    }
    
    private void OnDisable()
    {
        cameraManager.frameReceived -= OnCameraFrameReceived;
    }

    private void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
 
            byte phase;
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    phase = 0;
                    _isTouching = true;
                    if (dragOverlay != null) dragOverlay.SetActive(true);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    phase = 1;
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                default:
                    phase = 2;
                    _isTouching = false;
                    if (dragOverlay != null) dragOverlay.SetActive(false);
                    break;
            }
 
            SendTouchInput(touch.position, phase);
        }
    }



    private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        frameStatusText.text = "Event fired!";
        _timer += Time.deltaTime;
        if (_timer < _sendInterval) return;
        _timer = 0f;
        
        if (cameraManager == null)
        {
            frameStatusText.text = "ERROR: No camera manager assigned";
            return;
        }
        frameStatusText.text = "Acquiring image...";
        if (cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            frameStatusText.text = "Image acquired, encoding...";
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width, image.height),
                outputFormat = TextureFormat.RGB24
            };
            frameStatusText.text = "Encoded, sending chunks...";
            try
            {

                var size = image.GetConvertedDataSize(conversionParams);
                var buffer = new NativeArray<byte>(size, Allocator.Temp);
                image.Convert(conversionParams, buffer);

                // Load into Texture2D so we can JPEG encode
                var tex = new Texture2D(image.width, image.height, TextureFormat.RGB24, false);
                tex.LoadRawTextureData(buffer.ToArray());
                tex.Apply();
                buffer.Dispose();
                image.Dispose();

                // JPEG encode at quality 80
                var jpegBytes = tex.EncodeToJPG(95);
                Destroy(tex);

                var totalChunks = Mathf.CeilToInt((float)jpegBytes.Length / ChunkSize);
                _frameID++;

                for (int i = 0; i < totalChunks; i++)
                {
                    var offset = i * ChunkSize;
                    var chunkLength = Mathf.Min(ChunkSize, jpegBytes.Length - offset);

                    var packet = new byte[9 + chunkLength];
                    packet[0] = 0x01;
                    BitConverter.GetBytes(_frameID).CopyTo(packet, 1);
                    BitConverter.GetBytes((ushort)i).CopyTo(packet, 5);
                    BitConverter.GetBytes((ushort)totalChunks).CopyTo(packet, 7);
                    Array.Copy(jpegBytes, offset, packet, 9, chunkLength);

                    _udpClient.Send(packet, packet.Length, _imageServerAddress, _macPort);
                }

                frameStatusText.text = $"Frame {_frameID} — {jpegBytes.Length / 1024}KB in {totalChunks} chunks";
            }
            catch (Exception e)
            {
                frameStatusText.text = $"SEND ERROR: {e.Message}";
            }
        }
        else
        {
            frameStatusText.text = "WARNING: Could not acquire camera image";
        }
    }

    private void SendTouchInput(Vector2 pos, byte phase)
    {
        try
        {
            // Packet: [0x02][x:f32 4B][y:f32 4B][phase:1B]
            var packet = new byte[10];
            packet[0] = 0x02;
            BitConverter.GetBytes(pos.x).CopyTo(packet, 1);
            BitConverter.GetBytes(pos.y).CopyTo(packet, 5);
            packet[9] = phase;
            _udpClient.Send(packet, packet.Length, _imageServerAddress, _macPort);

            // Temporary debug — remove once confirmed working
            touchStatusText.text = $"Touch phase={phase} x={pos.x:F0} y={pos.y:F0}";
        }
        catch (Exception e)
        {
            touchStatusText.text = $"TOUCH SEND ERROR: {e.Message}";
        }
    }
    
    private void OnDestroy() => _udpClient?.Close();
}