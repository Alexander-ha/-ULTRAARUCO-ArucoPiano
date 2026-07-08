using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal VISUAL smoke test for the ArucoNanoPiano HTTP bridge.
///
/// Renders coloured boxes on a Screen-Space-Overlay Canvas (the SAME path as the project's
/// working text UI), so it is immune to the scene's dual-camera setup (a Pixel-Perfect
/// orthographic camera draws over the Main Camera, which hid earlier 3D attempts).
///
///   • PIANO     — BLUE box, at the piano marker's position in the camera image.
///   • LEFT HAND — GREEN box, RIGHT HAND — YELLOW box.
///   • KEY       — box top-centre: GREY = not seen yet, GREEN = marker visible,
///                 RED = marker hidden (key pressed).
///
/// The marker centres (image px) are auto-calibrated into the screen, so it adapts to any
/// camera resolution/orientation. Drop on any GameObject and link the server (or use
/// Tools ▸ ArucoPiano ▸ Setup Minimal Visual Test), then press Play and open the phone camera.
/// </summary>
public class MinimalArucoTest : MonoBehaviour
{
    [Header("Bridge (auto-found if empty)")]
    public UnityHTTPServer server;
    public Camera viewCamera; // kept for compatibility; not used by the overlay

    [Header("Image-to-screen mapping")]
    [Tooltip("Auto-fit marker pixel coords into the view (adapts to any resolution/orientation).")]
    public bool autoCalibrate = true;
    [Tooltip("Used only when auto-calibrate is OFF: the camera image resolution in px.")]
    public Vector2 referenceResolution = new Vector2(1280f, 720f);

    [Header("Key occlusion indicator")]
    [Range(1, 10)] public int watchKeyIndex = 1;

    [Header("Look & feel")]
    [Range(0f, 30f)] public float smoothing = 14f;
    public float boxSize = 90f;

    Canvas _canvas;
    Font _font;
    RectTransform _piano, _left, _right, _key;
    Image _pianoImg, _leftImg, _rightImg, _keyImg;
    Text _status;

    float _minX = float.MaxValue, _maxX = float.MinValue;
    float _minY = float.MaxValue, _maxY = float.MinValue;
    bool _keyEverSeen;

    static readonly Color ColPiano = new Color(0.20f, 0.45f, 1f);
    static readonly Color ColLeft  = new Color(0.15f, 0.85f, 0.30f);
    static readonly Color ColRight = new Color(1f, 0.80f, 0.10f);
    static readonly Color Seen     = new Color(0.15f, 0.85f, 0.30f);
    static readonly Color Lost     = new Color(0.95f, 0.20f, 0.20f);
    static readonly Color Idle     = new Color(0.5f, 0.5f, 0.55f, 0.5f);

    void Start()
    {
        if (server == null) server = FindAnyObjectByType<UnityHTTPServer>();
        if (server == null)
            Debug.LogError("[MinimalArucoTest] No UnityHTTPServer in the scene (it sits on Main Camera).");

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        BuildCanvas();
        Debug.Log("[MinimalArucoTest] Overlay visual test running. Open the phone camera and point at markers.");
    }

    void BuildCanvas()
    {
        var go = new GameObject("ArucoOverlayCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000; // draw above the project's own UI
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        _piano = MakeBox("Piano", ColPiano, "PIANO", boxSize * 1.15f, out _pianoImg);
        _left  = MakeBox("LeftHand", ColLeft, "L-HAND", boxSize, out _leftImg);
        _right = MakeBox("RightHand", ColRight, "R-HAND", boxSize, out _rightImg);
        _key   = MakeBox("Key", Idle, "KEY", boxSize, out _keyImg);

        _status = MakeStatus();
    }

    RectTransform MakeBox(string name, Color color, string label, float size, out Image img)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero; // bottom-left origin
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        img = go.AddComponent<Image>();
        img.color = color;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        var lblGo = new GameObject("label", typeof(RectTransform));
        lblGo.transform.SetParent(go.transform, false);
        var lrt = lblGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = lblGo.AddComponent<Text>();
        txt.font = _font;
        txt.text = label;
        txt.fontSize = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        return rt;
    }

    Text MakeStatus()
    {
        var go = new GameObject("Status", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(14f, -14f);
        rt.sizeDelta = new Vector2(900f, 80f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        var txtGo = new GameObject("text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 4f); trt.offsetMax = new Vector2(-8f, -4f);
        var t = txtGo.AddComponent<Text>();
        t.font = _font;
        t.fontSize = 16;
        t.alignment = TextAnchor.UpperLeft;
        t.color = Color.white;
        t.text = "Waiting for data from phone...";
        return t;
    }

    void Update()
    {
        if (_canvas == null) return;
        var data = server != null ? server.LatestData : null;
        if (data == null)
        {
            if (_status != null) _status.text = "Server up — no marker data received yet.\n(Open the phone camera and point at the markers.)";
            return;
        }

        Feed(data.piano?.corners);
        Feed(data.left_hand?.corners);
        Feed(data.right_hand?.corners);

        bool p = Place(_piano, _pianoImg, data.piano?.corners, ColPiano, out Vector2 pc);
        bool l = Place(_left, _leftImg, data.left_hand?.corners, ColLeft, out Vector2 lc);
        bool r = Place(_right, _rightImg, data.right_hand?.corners, ColRight, out _);
        bool keyPressed = UpdateKey(GetKey(data.extra_keys, watchKeyIndex));

        string rel = (p && l) ? $"{(lc.x - pc.x):F0}px" : "--";
        if (_status != null)
            _status.text =
                $"piano:{S(p)}  left:{S(l)}  right:{S(r)}   key#{watchKeyIndex}:{(keyPressed ? "PRESSED" : (_keyEverSeen ? "up" : "wait"))}\n" +
                $"L-hand offset from piano: {rel}    (Blue=Piano  Green=Left  Yellow=Right  KeyBox: green=visible/red=pressed)";
    }

    static string S(bool seen) => seen ? "OK" : "lost";

    void Feed(List<UnityHTTPServer.Corner> corners)
    {
        if (corners == null || corners.Count == 0) return;
        Vector2 c = Center(corners);
        _minX = Mathf.Min(_minX, c.x); _maxX = Mathf.Max(_maxX, c.x);
        _minY = Mathf.Min(_minY, c.y); _maxY = Mathf.Max(_maxY, c.y);
    }

    bool Place(RectTransform rt, Image img, List<UnityHTTPServer.Corner> corners, Color color, out Vector2 center)
    {
        center = Vector2.zero;
        bool seen = corners != null && corners.Count > 0;
        if (!seen) { img.color = Idle; return false; }

        center = Center(corners);
        float nx = NormalizeX(center.x);
        float ny = NormalizeY(center.y);

        // 8%..92% of the screen so boxes never sit exactly on the edge.
        float sx = (0.08f + 0.84f * nx) * Screen.width;
        float sy = (0.08f + 0.84f * (1f - ny)) * Screen.height;
        Vector2 target = new Vector2(sx, sy);
        rt.anchoredPosition = (smoothing > 0f)
            ? Vector2.Lerp(rt.anchoredPosition, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime))
            : target;
        img.color = color;
        return true;
    }

    bool UpdateKey(UnityHTTPServer.KeyData key)
    {
        bool visible = key != null && key.corners != null && key.corners.Count > 0;
        if (visible) _keyEverSeen = true;
        bool pressed = _keyEverSeen && !visible;

        _keyImg.color = !_keyEverSeen ? Idle : (visible ? Seen : Lost);
        _key.sizeDelta = Vector2.one * (pressed ? boxSize * 1.3f : boxSize);
        _key.anchoredPosition = new Vector2(Screen.width * 0.5f, Screen.height * (pressed ? 0.84f : 0.88f));
        return pressed;
    }

    // ---- helpers ----------------------------------------------------------------------
    float NormalizeX(float x)
    {
        if (autoCalibrate && _maxX - _minX > 1f) return Mathf.Clamp01(Mathf.InverseLerp(_minX, _maxX, x));
        return Mathf.Clamp01(x / Mathf.Max(1f, referenceResolution.x));
    }

    float NormalizeY(float y)
    {
        if (autoCalibrate && _maxY - _minY > 1f) return Mathf.Clamp01(Mathf.InverseLerp(_minY, _maxY, y));
        return Mathf.Clamp01(y / Mathf.Max(1f, referenceResolution.y));
    }

    static Vector2 Center(List<UnityHTTPServer.Corner> corners)
    {
        float sx = 0f, sy = 0f;
        foreach (var c in corners) { sx += c.x; sy += c.y; }
        return new Vector2(sx / corners.Count, sy / corners.Count);
    }

    static UnityHTTPServer.KeyData GetKey(UnityHTTPServer.ExtraKeys ek, int i)
    {
        if (ek == null) return null;
        switch (i)
        {
            case 1: return ek.key_1; case 2: return ek.key_2; case 3: return ek.key_3;
            case 4: return ek.key_4; case 5: return ek.key_5; case 6: return ek.key_6;
            case 7: return ek.key_7; case 8: return ek.key_8; case 9: return ek.key_9;
            case 10: return ek.key_10; default: return null;
        }
    }
}
