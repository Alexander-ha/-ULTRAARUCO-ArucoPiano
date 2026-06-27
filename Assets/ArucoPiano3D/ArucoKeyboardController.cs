using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a 3D keyboard + two hands from the HTTP bridge, for a FIXED top-down camera.
///   • PIANO marker → the keyboard moves inside a fixed SQUARE ZONE on the floor (X,Z from the
///     marker centre in the image) and rises/lowers in Y (3rd coordinate, from apparent marker
///     size). It rotates ONLY with the real marker's rotation (yaw from the marker corners) —
///     it never auto-faces the camera.
///   • KEY markers (extra_keys.key_1..key_N) toggle the pressed state of the mapped keys.
///   • Two HAND markers slide two one-finger hands along the keys; the nearest reachable hand
///     glides to a pressed key and presses it.
/// </summary>
public class ArucoKeyboardController : MonoBehaviour
{
    public enum ImageRotation { None, Rotate90, Rotate180, Rotate270 }

    [Header("Image orientation")]
    [Tooltip("Rotate marker coordinates to match your setup (portrait app filmed in landscape). " +
             "Rotate90 ≈ 90° CCW; if movement is mirrored, try Rotate270 or the Invert X/Z flags.")]
    public ImageRotation imageRotation = ImageRotation.Rotate90;

    [Header("Bridge (auto-found if empty)")]
    public UnityHTTPServer server;
    public Camera viewCamera; // optional; placement is world-space now and does not need it

    [Header("Keyboard")]
    public Transform pianoRoot;
    public List<PianoKey3D> keys = new List<PianoKey3D>();

    [Header("Movement zone (square on the floor)")]
    [Tooltip("Centre of the square area the piano may move within (world space).")]
    public Vector3 zoneCenter = Vector3.zero;
    [Tooltip("Side length of the square zone (world metres). Align the camera to this.")]
    public float zoneSize = 1.0f;
    public bool invertX = false;
    public bool invertZ = false;
    [Tooltip("Image Y that maps to the zone centre (0.5 = frame centre, 0.75 = 3/4 down). Vertical calibration.")]
    [Range(0f, 1f)] public float verticalCenter = 0.75f;
    [Tooltip("OFF = the piano stops following the marker's position and just sits at the centre " +
             "(the marker is still tracked for presence and rotation).")]
    public bool trackPianoPosition = true;
    [Tooltip("Height the piano sits at when position tracking is OFF.")]
    public float pianoRestHeight = 0.1f;

    [Header("Height = 3rd coordinate (from marker apparent size)")]
    public float nearSizePx = 220f;  // marker close (big)  -> heightMax
    public float farSizePx = 60f;    // marker far (small)  -> heightMin
    public float heightMin = 0.0f;
    public float heightMax = 0.4f;

    [Header("Rotation (from the real marker only)")]
    public bool useMarkerRotation = true;
    [Tooltip("Flip if the piano yaws the wrong way (+1 / -1).")]
    public float yawSign = 1f;
    public float yawOffset = 0f;
    [Range(0f, 30f)] public float rotationSmoothing = 12f;

    [Header("Smoothing / calibration")]
    [Range(0f, 30f)] public float positionSmoothing = 12f;
    public bool autoCalibrate = true;
    public Vector2 referenceResolution = new Vector2(1280f, 720f);

    [Header("Key markers -> key index")]
    public int[] markerKeyIndices = { 3, 7, 11 };

    [Header("Hands")]
    public HandDriverBase leftHand;
    public HandDriverBase rightHand;
    [Tooltip("Image px (hand offset from the piano marker) -> keyboard local metres. Negate if hands slide the wrong way.")]
    public float handPxToLocal = 0.0016f;
    [Tooltip("How far (local metres) a hand may slide left/right of the keyboard centre = half the keyboard width.")]
    public float handTravelHalfWidth = 0.3f;
    public float handReach = 0.18f;
    public float handArriveThreshold = 0.03f;

    [Header("Hand horizontal calibration (frame -> keyboard)")]
    [Tooltip("Frame centre maps to piano centre; nudge left/right with this (fraction of the frame).")]
    [Range(-0.5f, 0.5f)] public float handCenterOffset = 0f;
    [Tooltip("Global fine-tune multiplier on the hand gain.")]
    public float handSpanScale = 1f;
    [Tooltip("Hand-marker frame fraction (from centre) at which a hand reaches its octave centre (~0.3 = 30%).")]
    public float handAnchorFraction = 0.3f;
    [Tooltip("Keyboard-local X the LEFT hand reaches at -anchorFraction (builder sets it = малая octave centre).")]
    public float leftAnchorX = -0.1f;
    [Tooltip("...the RIGHT hand at +anchorFraction (вторая octave centre).")]
    public float rightAnchorX = 0.1f;

    [Header("Zone visual")]
    public bool showZone = true;
    public Color zoneColor = new Color(1f, 0.9f, 0.2f);
    public float zoneLineWidth = 0.008f;

    readonly Dictionary<int, PianoKey3D> _byIndex = new Dictionary<int, PianoKey3D>();
    bool[] _everSeen;
    float _minX = float.MaxValue, _maxX = float.MinValue;
    float _minY = float.MaxValue, _maxY = float.MinValue;
    float _minHX = float.MaxValue, _maxHX = float.MinValue; // separate frame range for the hands
    bool _placed;

    float _leftFreeX, _rightFreeX;
    Vector2 _pianoCenter;
    bool _pianoSeen;
    readonly List<float> _pressedX = new List<float>();
    readonly List<(float d, int h, int k)> _pairs = new List<(float, int, int)>();

    void Awake()
    {
        _byIndex.Clear();
        foreach (var k in keys)
            if (k != null) _byIndex[k.keyIndex] = k;
        _everSeen = new bool[markerKeyIndices != null ? markerKeyIndices.Length : 0];
    }

    void Start()
    {
        if (server == null) server = FindAnyObjectByType<UnityHTTPServer>();
        if (server == null) Debug.LogError("[ArucoKeyboardController] No UnityHTTPServer in scene.");
        if (leftHand != null) _leftFreeX = leftHand.CurrentLocalX;
        if (rightHand != null) _rightFreeX = rightHand.CurrentLocalX;
        if (showZone) BuildZoneVisual();
    }

    // Key position along the keyboard, expressed in pianoRoot's local frame. Works whether the
    // keys are direct children (cubes) or nested+scaled (the real model).
    float KeyLocalX(PianoKey3D k) => pianoRoot.InverseTransformPoint(k.transform.position).x;

    void Update()
    {
        if (pianoRoot == null) return;
        var data = server != null ? server.LatestData : null;
        if (data == null) return;

        UpdatePianoPose(data.piano);

        var lg = leftHand as GhostHandRig;
        var rg = rightHand as GhostHandRig;
        if (lg != null || rg != null)
            UpdateMultiFinger(data, lg, rg);   // multi-finger chord path (real hands)
        else
        {
            UpdateKeyStates(data.extra_keys);  // legacy single-finger / cube path
            UpdateHands(data);
        }
    }

    readonly List<PianoKey3D> _leftKeys = new List<PianoKey3D>();
    readonly List<PianoKey3D> _rightKeys = new List<PianoKey3D>();

    void UpdateMultiFinger(UnityHTTPServer.RootObject data, GhostHandRig lg, GhostHandRig rg)
    {
        _leftKeys.Clear();
        _rightKeys.Clear();

        if (!_pianoSeen)
        {
            lg?.DriveChord(_leftKeys, _leftFreeX);   // idle while the piano marker is lost
            rg?.DriveChord(_rightKeys, _rightFreeX);
            return;
        }

        float lFree = ComputeFreeX(data.left_hand, ref _leftFreeX, leftAnchorX, -handAnchorFraction);
        float rFree = ComputeFreeX(data.right_hand, ref _rightFreeX, rightAnchorX, handAnchorFraction);

        // Active keys = marker hidden (after first sighting). Hand them to the nearest hand in reach.
        if (markerKeyIndices != null)
            for (int i = 0; i < markerKeyIndices.Length; i++)
            {
                var key = GetKey(data.extra_keys, i + 1);
                bool visible = key != null && key.corners != null && key.corners.Count > 0;
                if (visible) _everSeen[i] = true;
                bool active = _everSeen[i] && !visible;
                if (!active || !_byIndex.TryGetValue(markerKeyIndices[i], out var pk)) continue;
                if (!pk.interactive) continue; // inspector toggle: disabled key

                float keyX = KeyLocalX(pk);
                float dl = lg != null ? Mathf.Abs(keyX - lg.CurrentLocalX) : float.MaxValue;
                float dr = rg != null ? Mathf.Abs(keyX - rg.CurrentLocalX) : float.MaxValue;
                bool lReach = dl <= handReach;
                bool rReach = dr <= handReach;
                if (lReach && (!rReach || dl <= dr)) _leftKeys.Add(pk);
                else if (rReach) _rightKeys.Add(pk);
                // else: out of reach of BOTH hands -> stays unpressed (item 6)
            }

        lg?.DriveChord(_leftKeys, lFree);
        rg?.DriveChord(_rightKeys, rFree);

        // A key dips ONLY if a finger actually reached it (items 4 & 6).
        foreach (var key in keys)
        {
            if (key == null) continue;
            bool pressed = (lg != null && lg.IsKeyPressed(key)) || (rg != null && rg.IsKeyPressed(key));
            key.SetPressed(pressed);
        }
    }

    void UpdatePianoPose(UnityHTTPServer.PianoData piano)
    {
        _pianoSeen = piano != null && piano.corners != null && piano.corners.Count > 0;
        if (!_pianoSeen) return; // marker lost -> hold

        _pianoCenter = Center(piano.corners);
        Feed(_pianoCenter);

        Vector3 world;
        if (trackPianoPosition)
        {
            // Position inside the square zone (clamped by construction since nx,ny are 0..1).
            float nx = NormalizeX(_pianoCenter.x);
            float ny = NormalizeY(_pianoCenter.y);
            float sizeT = Mathf.Clamp01(Mathf.InverseLerp(farSizePx, nearSizePx, ApparentSize(piano.corners)));
            float ox = (nx - 0.5f) * zoneSize; if (invertX) ox = -ox;
            float oz = (verticalCenter - ny) * zoneSize; if (invertZ) oz = -oz;
            world = zoneCenter + new Vector3(ox, Mathf.Lerp(heightMin, heightMax, sizeT), oz);
        }
        else
        {
            // Not tracking position: just sit at the centre (marker still tracked for presence/rotation).
            world = zoneCenter + Vector3.up * pianoRestHeight;
        }

        if (!_placed) { pianoRoot.position = world; _placed = true; }
        else pianoRoot.position = Vector3.Lerp(pianoRoot.position, world, 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime));

        // Rotation comes ONLY from the real marker (yaw about the vertical axis).
        Quaternion target = Quaternion.identity;
        if (useMarkerRotation && piano.corners.Count >= 2)
            target = Quaternion.Euler(0f, MarkerYaw(piano.corners) * yawSign + yawOffset, 0f);
        pianoRoot.rotation = rotationSmoothing > 0f
            ? Quaternion.Slerp(pianoRoot.rotation, target, 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime))
            : target;
    }

    float MarkerYaw(List<UnityHTTPServer.Corner> corners)
    {
        // Angle of the first marker edge (rotated to the chosen orientation).
        Vector2 p0 = RotXY(corners[0].x, corners[0].y);
        Vector2 p1 = RotXY(corners[1].x, corners[1].y);
        return Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
    }

    void UpdateKeyStates(UnityHTTPServer.ExtraKeys ek)
    {
        if (markerKeyIndices == null) return;
        for (int i = 0; i < markerKeyIndices.Length; i++)
        {
            var key = GetKey(ek, i + 1);
            bool visible = key != null && key.corners != null && key.corners.Count > 0;
            if (visible) _everSeen[i] = true;
            bool pressed = _everSeen[i] && !visible;
            if (_byIndex.TryGetValue(markerKeyIndices[i], out var pk))
                pk.SetPressed(pressed);
        }
    }

    void UpdateHands(UnityHTTPServer.RootObject data)
    {
        if (leftHand == null && rightHand == null) return;
        if (!_pianoSeen) return;

        float lFree = ComputeFreeX(data.left_hand, ref _leftFreeX, leftAnchorX, -handAnchorFraction);
        float rFree = ComputeFreeX(data.right_hand, ref _rightFreeX, rightAnchorX, handAnchorFraction);

        _pressedX.Clear();
        if (markerKeyIndices != null)
            for (int i = 0; i < markerKeyIndices.Length; i++)
                if (_byIndex.TryGetValue(markerKeyIndices[i], out var pk) && pk.IsPressed)
                    _pressedX.Add(KeyLocalX(pk));

        var hands = new HandDriverBase[] { leftHand, rightHand };
        var free = new float[] { lFree, rFree };
        var assignedKey = new int[] { -1, -1 };

        _pairs.Clear();
        for (int h = 0; h < 2; h++)
        {
            if (hands[h] == null) continue;
            for (int k = 0; k < _pressedX.Count; k++)
            {
                float d = Mathf.Abs(_pressedX[k] - free[h]);
                if (d <= handReach) _pairs.Add((d, h, k));
            }
        }
        _pairs.Sort((a, b) => a.d.CompareTo(b.d));

        bool[] handUsed = { false, false };
        bool[] keyUsed = new bool[_pressedX.Count];
        foreach (var p in _pairs)
            if (!handUsed[p.h] && !keyUsed[p.k]) { assignedKey[p.h] = p.k; handUsed[p.h] = true; keyUsed[p.k] = true; }

        for (int h = 0; h < 2; h++)
        {
            if (hands[h] == null) continue;
            if (assignedKey[h] >= 0)
            {
                float tx = _pressedX[assignedKey[h]];
                bool arrived = Mathf.Abs(hands[h].CurrentLocalX - tx) < handArriveThreshold;
                hands[h].SetTarget(tx, arrived);
            }
            else hands[h].SetTarget(free[h], false);
        }
    }

    float ComputeFreeX(UnityHTTPServer.HandData hand, ref float stored, float anchorX, float anchorFrac)
    {
        if (hand != null && hand.corners != null && hand.corners.Count > 0)
        {
            // Frame centre -> keyboard centre; ±anchorFrac of the frame -> anchorX (octave centre).
            float hx = Center(hand.corners).x;
            _minHX = Mathf.Min(_minHX, hx); _maxHX = Mathf.Max(_maxHX, hx);
            float nx = (autoCalibrate && _maxHX - _minHX > 1f)
                ? Mathf.Clamp01(Mathf.InverseLerp(_minHX, _maxHX, hx))
                : Mathf.Clamp01(hx / Mathf.Max(1f, referenceResolution.x));
            float gain = Mathf.Abs(anchorFrac) > 1e-4f ? anchorX / anchorFrac : (2f * handTravelHalfWidth);
            float fx = (nx - 0.5f + handCenterOffset) * gain * handSpanScale;
            stored = Mathf.Clamp(fx, -handTravelHalfWidth, handTravelHalfWidth);
        }
        return stored;
    }

    // ---- zone visual ------------------------------------------------------------------
    void BuildZoneVisual()
    {
        var go = new GameObject("MovementZone");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.positionCount = 4;
        lr.useWorldSpace = true;
        lr.widthMultiplier = zoneLineWidth;
        lr.numCornerVertices = 2;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        lr.material = new Material(sh);
        lr.startColor = lr.endColor = zoneColor;

        float h = zoneSize * 0.5f;
        Vector3 c = zoneCenter;
        lr.SetPositions(new[]
        {
            c + new Vector3(-h, 0f, -h),
            c + new Vector3( h, 0f, -h),
            c + new Vector3( h, 0f,  h),
            c + new Vector3(-h, 0f,  h),
        });
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = zoneColor;
        float h = zoneSize * 0.5f;
        Vector3 a = zoneCenter + new Vector3(-h, 0, -h), b = zoneCenter + new Vector3(h, 0, -h);
        Vector3 cc = zoneCenter + new Vector3(h, 0, h), d = zoneCenter + new Vector3(-h, 0, h);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, cc); Gizmos.DrawLine(cc, d); Gizmos.DrawLine(d, a);
    }

    // ---- helpers ----------------------------------------------------------------------
    void Feed(Vector2 c)
    {
        _minX = Mathf.Min(_minX, c.x); _maxX = Mathf.Max(_maxX, c.x);
        _minY = Mathf.Min(_minY, c.y); _maxY = Mathf.Max(_maxY, c.y);
    }

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

    // Rotate a raw image point to compensate for the phone orientation.
    Vector2 RotXY(float x, float y)
    {
        switch (imageRotation)
        {
            case ImageRotation.Rotate90:  return new Vector2(-y, x);
            case ImageRotation.Rotate180: return new Vector2(-x, -y);
            case ImageRotation.Rotate270: return new Vector2(y, -x);
            default:                       return new Vector2(x, y);
        }
    }

    Vector2 Center(List<UnityHTTPServer.Corner> corners)
    {
        float sx = 0f, sy = 0f;
        foreach (var c in corners) { var p = RotXY(c.x, c.y); sx += p.x; sy += p.y; }
        return new Vector2(sx / corners.Count, sy / corners.Count);
    }

    float ApparentSize(List<UnityHTTPServer.Corner> corners)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var c in corners)
        {
            var p = RotXY(c.x, c.y);
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        return Mathf.Max(maxX - minX, maxY - minY);
    }

    static UnityHTTPServer.KeyData GetKey(UnityHTTPServer.ExtraKeys ek, int i)
    {
        if (ek == null) return null;
        switch (i)
        {
            case 1: return ek.key_1; case 2: return ek.key_2; case 3: return ek.key_3;
            case 4: return ek.key_4; case 5: return ek.key_5; case 6: return ek.key_6;
            case 7: return ek.key_7; case 8: return ek.key_8; case 9: return ek.key_9;
            case 10: return ek.key_10; case 11: return ek.key_11; case 12: return ek.key_12;
            case 13: return ek.key_13; case 14: return ek.key_14; case 15: return ek.key_15;
            case 16: return ek.key_16; case 17: return ek.key_17; case 18: return ek.key_18;
            case 19: return ek.key_19; case 20: return ek.key_20; case 21: return ek.key_21;
            case 22: return ek.key_22; case 23: return ek.key_23; case 24: return ek.key_24;
            default: return null;
        }
    }
}
