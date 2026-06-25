using UnityEngine;

/// <summary>
/// A single 3D piano key that tilts about a hinge when pressed, and tints toward a
/// "pressed" colour for extra visibility. Pressed state is driven externally
/// (by <see cref="ArucoKeyboardController"/>, from whether the key's ArUco marker is hidden).
/// </summary>
[DisallowMultipleComponent]
public class PianoKey3D : MonoBehaviour
{
    public int keyIndex;
    [Tooltip("Black key — only index/middle/ring press it, on the edge.")]
    public bool isBlack;

    [Header("Finger contact points (top of the key)")]
    [Tooltip("Where along the key (0=hinge .. 1=front tip) the EDGE contact sits.")]
    [Range(0f, 1f)] public float edgeFraction = 0.9f;
    [Tooltip("Where along the key the CENTER contact sits (front portion, before the black keys).")]
    [Range(0f, 1f)] public float centerFraction = 0.55f;

    [Header("Press animation")]
    [Tooltip("How far the key tilts when pressed, degrees.")]
    public float pressAngle = 10f;
    [Tooltip("Hinge axis in the key's local space (X = lateral, normal for a keyboard).")]
    public Vector3 hingeAxis = Vector3.right;
    [Tooltip("Hinge pivot offset from the key origin, local metres (e.g. (0,0,-length/2) = back edge).")]
    public Vector3 hingeOffsetLocal = Vector3.zero;
    [Tooltip("How fast the key reaches the target state.")]
    public float responsiveness = 16f;

    [Header("Colour feedback")]
    [Tooltip("If false the key keeps its model material untouched (only the rotation animates).")]
    public bool useColorFeedback = true;
    public Color restColor = Color.white;
    public Color pressedColor = new Color(1f, 0.5f, 0.1f);

    Quaternion _restRot;
    Vector3 _restPos;
    Vector3 _pivot;
    float _t;
    bool _pressed;
    Renderer _renderer;
    MaterialPropertyBlock _mpb;
    Color _restColor = Color.white; // captured from the model material (no tint at rest)

    // Contact geometry in the key's local space (so it follows the key as it dips).
    Vector3 _topCenterLocal;   // x, top, hinge
    Vector3 _frontDirLocal;    // local vector from hinge to the front tip (top surface)
    bool _haveContact;

    public bool IsPressed => _pressed;
    public float PressAmount => _t;

    void Awake()
    {
        _restRot = transform.localRotation;
        _restPos = transform.localPosition;
        _pivot = _restPos + _restRot * hingeOffsetLocal;
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        if (_renderer != null && _renderer.sharedMaterial != null)
        {
            var m = _renderer.sharedMaterial;
            _restColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                       : m.HasProperty("_Color") ? m.GetColor("_Color") : _restColor;
        }
        ComputeContact();
        ApplyColor();
    }

    void ComputeContact()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Bounds b = mf.sharedMesh.bounds;       // local space
        float frontSign = b.center.z >= 0f ? 1f : -1f;
        float topY = b.center.y + b.extents.y;
        // Hinge is the rear edge (origin); front tip is the far end along Z.
        _topCenterLocal = new Vector3(b.center.x, topY, 0f);
        _frontDirLocal = new Vector3(0f, 0f, (b.center.z + frontSign * b.extents.z));
        _haveContact = true;
    }

    /// <summary>World point a finger should press for the front EDGE of the key.</summary>
    public Vector3 EdgeContact => ContactWorld(edgeFraction);
    /// <summary>World point for the CENTER (front portion) of a white key.</summary>
    public Vector3 CenterContact => ContactWorld(centerFraction);

    Vector3 ContactWorld(float frac)
    {
        if (!_haveContact) return transform.position;
        return transform.TransformPoint(_topCenterLocal + _frontDirLocal * frac);
    }

    public void SetPressed(bool pressed) => _pressed = pressed;

    void Update()
    {
        _t = Mathf.MoveTowards(_t, _pressed ? 1f : 0f, responsiveness * Time.deltaTime);

        Quaternion dq = Quaternion.AngleAxis(pressAngle * _t, hingeAxis.normalized);
        transform.localRotation = dq * _restRot;
        transform.localPosition = _pivot + dq * (_restPos - _pivot);

        ApplyColor();
    }

    void ApplyColor()
    {
        if (_renderer == null || !useColorFeedback) return;
        _mpb.Clear();
        if (_t > 0.001f)
        {
            // Only tint toward the pressed colour; at rest the model material shows untouched.
            Color c = Color.Lerp(_restColor, pressedColor, _t);
            _mpb.SetColor("_BaseColor", c); // URP Lit
            _mpb.SetColor("_Color", c);     // built-in fallback
        }
        _renderer.SetPropertyBlock(_mpb);
    }
}
