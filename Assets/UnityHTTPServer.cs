using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System;

public class UnityHTTPServer : MonoBehaviour
{
    [Header("UI References")]
    public Text statusText;
    public Text leftHandText;
    public Text rightHandText;
    public Text pianoText;
    public Text extraKeysText;

    [Header("Server Settings")]
    public int serverPort = 8080;
    public bool showDebugLogs = true;

    private HttpListener listener;
    private bool isRunning = false;
    private string lastData = "";
    private string clientIP = "";

    // --- Additive hook for 3D visualization (does not change existing UI behaviour) ---
    /// <summary>Latest successfully-parsed payload. Null until the first valid message.</summary>
    public RootObject LatestData { get; private set; }
    /// <summary>Raised on the main thread each time a new payload is parsed.</summary>
    public event System.Action<RootObject> OnDataUpdated;

    [System.Serializable]
    public class Corner { public float x; public float y; }
    [System.Serializable]
    public class HandData { public int id; public List<Corner> corners; }
    [System.Serializable]
    public class PianoData { public int id; public List<Corner> corners; }
    [System.Serializable]
    public class KeyData { public int id; public List<Corner> corners; }
    [System.Serializable]
    public class ExtraKeys
    {
        public KeyData key_1; public KeyData key_2; public KeyData key_3;
        public KeyData key_4; public KeyData key_5; public KeyData key_6;
        public KeyData key_7; public KeyData key_8; public KeyData key_9;
        public KeyData key_10; public KeyData key_11; public KeyData key_12;
        public KeyData key_13; public KeyData key_14; public KeyData key_15;
        public KeyData key_16; public KeyData key_17; public KeyData key_18;
        public KeyData key_19; public KeyData key_20; public KeyData key_21;
        public KeyData key_22; public KeyData key_23; public KeyData key_24;
    }
    [System.Serializable]
    public class RootObject
    {
        public HandData left_hand;
        public HandData right_hand;
        public PianoData piano;
        public ExtraKeys extra_keys;
    }

    void Start()
    {
        StartHTTPServer();

        string localIP = GetLocalIPAddress();
        if (statusText != null)
        {
            statusText.text = $"HTTP Server:\nhttp://{localIP}:{serverPort}/\nWaiting for data...";
        }
    }

    void StartHTTPServer()
    {
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{serverPort}/");
            listener.Start();
            isRunning = true;

            StartCoroutine(HandleRequests());

            if (showDebugLogs)
                UnityEngine.Debug.Log($"HTTP Server started on port {serverPort}");
        }
        catch (Exception e)
        {
            if (statusText != null)
                statusText.text = $"Server error: {e.Message}";
            UnityEngine.Debug.LogError($"Server error: {e.Message}");
        }
    }

    IEnumerator HandleRequests()
    {
        while (isRunning)
        {
            if (listener.IsListening)
            {
                IAsyncResult result = listener.BeginGetContext(ProcessRequest, listener);
                yield return new WaitUntil(() => result.IsCompleted);
            }
            // Process the next request on the following frame (~60 Hz) instead of throttling
            // to 10 Hz, which removes most of the visible input lag.
            yield return null;
        }
    }

    void ProcessRequest(IAsyncResult result)
    {
        try
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);

            clientIP = context.Request.RemoteEndPoint.Address.ToString();

            using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                string jsonData = reader.ReadToEnd();

                if (!string.IsNullOrEmpty(jsonData))
                {
                    lock (this)
                    {
                        lastData = jsonData;
                    }

                    if (showDebugLogs)
                        UnityEngine.Debug.Log($"Received from {clientIP}: {jsonData}");
                }
            }

            byte[] responseBytes = Encoding.UTF8.GetBytes("OK");
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Process request error: {e.Message}");
        }
    }

    void Update()
    {
        lock (this)
        {
            if (!string.IsNullOrEmpty(lastData))
            {
                ProcessReceivedData(lastData);
                lastData = "";
            }
        }
    }

    void ProcessReceivedData(string jsonData)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<RootObject>(jsonData);

            // Expose the parsed data so other components (e.g. the 3D test) can react to it.
            LatestData = data;
            OnDataUpdated?.Invoke(data);

            if (statusText != null)
            {
                statusText.text = $"Client: {clientIP}\nLast: {DateTime.Now.ToString("HH:mm:ss")}";
                statusText.color = Color.green;
            }

            UpdateUI(data);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Parse error: {e.Message}");
            UnityEngine.Debug.LogError($"JSON: {jsonData}");
        }
    }

    void UpdateUI(RootObject data)
    {
        // Left Hand
        if (data.left_hand?.corners?.Count > 0)
        {
            Vector2 center = CalculateCenter(data.left_hand.corners);
            if (leftHandText != null)
            {
                leftHandText.text = $"Left Hand: ({center.x:F1}, {center.y:F1})";
                leftHandText.color = Color.green;
            }
        }
        else
        {
            if (leftHandText != null)
            {
                leftHandText.text = "Left Hand: ? PRESSED";
                leftHandText.color = Color.red;
            }
        }

        // Right Hand
        if (data.right_hand?.corners?.Count > 0)
        {
            Vector2 center = CalculateCenter(data.right_hand.corners);
            if (rightHandText != null)
            {
                rightHandText.text = $"Right Hand: ({center.x:F1}, {center.y:F1})";
                rightHandText.color = Color.green;
            }
        }
        else
        {
            if (rightHandText != null)
            {
                rightHandText.text = "Right Hand: ? PRESSED";
                rightHandText.color = Color.red;
            }
        }

        // Piano
        if (data.piano?.corners?.Count > 0)
        {
            Vector2 center = CalculateCenter(data.piano.corners);
            if (pianoText != null)
            {
                pianoText.text = $"Piano: ({center.x:F1}, {center.y:F1})";
                pianoText.color = Color.green;
            }
        }
        else
        {
            if (pianoText != null)
            {
                pianoText.text = "Piano: ? PRESSED";
                pianoText.color = Color.red;
            }
        }

        // Extra Keys
        if (data.extra_keys != null)
        {
            string keysStatus = "";
            var extraKeysType = data.extra_keys.GetType();
            var properties = extraKeysType.GetProperties();

            int pressedCount = 0;
            foreach (var prop in properties)
            {
                if (prop.Name.StartsWith("key_"))
                {
                    var value = prop.GetValue(data.extra_keys) as KeyData;
                    if (value?.corners?.Count > 0)
                    {
                        Vector2 center = CalculateCenter(value.corners);
                        keysStatus += $"  {prop.Name}: ({center.x:F1}, {center.y:F1})\n";
                    }
                    else
                    {
                        keysStatus += $"  {prop.Name}: ?? PRESSED\n";
                        pressedCount++;
                    }
                }
            }
            if (extraKeysText != null)
                extraKeysText.text = $"Keys (pressed: {pressedCount}):\n{keysStatus}";
        }
    }

    Vector2 CalculateCenter(List<Corner> corners)
    {
        if (corners == null || corners.Count == 0)
            return Vector2.zero;

        float sumX = 0, sumY = 0;
        foreach (var corner in corners)
        {
            sumX += corner.x;
            sumY += corner.y;
        }
        return new Vector2(sumX / corners.Count, sumY / corners.Count);
    }

    string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error getting local IP: {e.Message}");
        }
        return "127.0.0.1";
    }

    void OnDestroy()
    {
        isRunning = false;
        if (listener != null && listener.IsListening)
        {
            listener.Stop();
            listener.Close();
        }
    }
}