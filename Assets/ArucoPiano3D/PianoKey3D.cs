using UnityEngine;

/// <summary>
/// A single 3D piano key: tilts about a hinge when pressed, tints/glows toward a pressed colour,
/// puffs smoke and plays its note (full attack, decays to a sustained floor while held — never
/// fully silent). Pressed state is driven by <see cref="ArucoKeyboardController"/>.
/// </summary>
[DisallowMultipleComponent]
public class PianoKey3D : MonoBehaviour
{
    public int keyIndex;
    [Tooltip("Black key — pressed on the edge (not by the thumb).")]
    public bool isBlack;
    [Tooltip("Quick on/off: uncheck to disable this key (it won't react to its marker).")]
    public bool interactive = true;

    [Header("Finger contact points (top of the key)")]
    [Range(0f, 1f)] public float edgeFraction = 0.9f;
    [Range(0f, 1f)] public float centerFraction = 0.55f;

    [Header("Press animation")]
    public float pressAngle = 10f;
    public Vector3 hingeAxis = Vector3.right;
    public Vector3 hingeOffsetLocal = Vector3.zero;
    public float responsiveness = 16f;

    [Header("Colour + glow")]
    public bool useColorFeedback = true;
    public Color restColor = Color.white;
    public Color pressedColor = new Color(1f, 0.5f, 0.1f);
    [Tooltip("Emission strength when pressed (needs Bloom on a URP Volume to really 'glow').")]
    public float glowIntensity = 2.5f;

    [Header("Sound")]
    public AudioClip noteClip;
    [Range(0f, 1f)] public float startVolume = 1f;
    [Tooltip("Volume it decays to while held (never goes fully silent).")]
    [Range(0f, 1f)] public float sustainVolume = 0.25f;
    [Tooltip("Seconds to decay from full to the sustain level.")]
    public float decayTime = 0.9f;
    [Tooltip("Seconds to fade out after release.")]
    public float releaseTime = 0.5f;

    [Header("Smoke")]
    public int smokePuff = 10;

    Quaternion _restRot;
    Vector3 _restPos;
    Vector3 _pivot;
    float _t;
    bool _pressed, _prevPressed;
    Renderer _renderer;
    MaterialPropertyBlock _mpb;
    Color _restColor = Color.white;

    Vector3 _topCenterLocal, _frontDirLocal;
    bool _haveContact;

    AudioSource _audio;
    float _vol;
    ParticleSystem _smoke;

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
        BuildAudio();
        BuildSmoke();
        ApplyColor();
    }

    void ComputeContact()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Bounds b = mf.sharedMesh.bounds;
        float frontSign = b.center.z >= 0f ? 1f : -1f;
        float topY = b.center.y + b.extents.y;
        _topCenterLocal = new Vector3(b.center.x, topY, 0f);
        _frontDirLocal = new Vector3(0f, 0f, b.center.z + frontSign * b.extents.z);
        _haveContact = true;
    }

    void BuildAudio()
    {
        if (noteClip == null) return;
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.clip = noteClip;
        _audio.loop = true;          // loop so a held note never goes silent
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;    // 2D
        _audio.volume = 0f;
    }

    void BuildSmoke()
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = _topCenterLocal + _frontDirLocal * edgeFraction;

        _smoke = go.AddComponent<ParticleSystem>();
        _smoke.Stop();
        var main = _smoke.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 0.04f;
        main.startSize = 0.012f;
        main.startColor = new Color(0.85f, 0.85f, 0.88f, 0.45f);
        main.gravityModifier = -0.03f;          // drift up
        main.maxParticles = 64;
        var emission = _smoke.emission; emission.enabled = false; // we Emit() bursts manually
        var shape = _smoke.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.004f;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null) psr.material = new Material(sh);
    }

    public Vector3 EdgeContact => ContactWorld(edgeFraction);
    public Vector3 CenterContact => ContactWorld(centerFraction);

    Vector3 ContactWorld(float frac)
    {
        if (!_haveContact) return transform.position;
        return transform.TransformPoint(_topCenterLocal + _frontDirLocal * frac);
    }

    public void SetPressed(bool pressed) => _pressed = pressed;

    void Update()
    {
        // Press onset -> trigger smoke + (re)start the note.
        if (_pressed && !_prevPressed) OnPress();
        _prevPressed = _pressed;

        _t = Mathf.MoveTowards(_t, _pressed ? 1f : 0f, responsiveness * Time.deltaTime);

        Quaternion dq = Quaternion.AngleAxis(pressAngle * _t, hingeAxis.normalized);
        transform.localRotation = dq * _restRot;
        transform.localPosition = _pivot + dq * (_restPos - _pivot);

        UpdateAudio();
        ApplyColor();
    }

    void OnPress()
    {
        if (_smoke != null) _smoke.Emit(Mathf.Max(0, smokePuff));
        if (_audio != null)
        {
            _vol = startVolume;
            _audio.volume = _vol;
            _audio.time = 0f;
            if (!_audio.isPlaying) _audio.Play();
        }
    }

    void UpdateAudio()
    {
        if (_audio == null) return;
        if (_pressed)
        {
            // Decay from full toward the sustain floor (stays there while held).
            float rate = decayTime > 0f ? (startVolume - sustainVolume) / decayTime : 999f;
            _vol = Mathf.MoveTowards(_vol, sustainVolume, rate * Time.deltaTime);
        }
        else
        {
            float rate = releaseTime > 0f ? 1f / releaseTime : 999f;
            _vol = Mathf.MoveTowards(_vol, 0f, rate * Time.deltaTime);
            if (_vol <= 0.001f && _audio.isPlaying) _audio.Stop();
        }
        _audio.volume = _vol;
    }

    void ApplyColor()
    {
        if (_renderer == null || !useColorFeedback) return;
        _mpb.Clear();
        if (_t > 0.001f)
        {
            Color c = Color.Lerp(_restColor, pressedColor, _t);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            _mpb.SetColor("_EmissionColor", pressedColor * (glowIntensity * _t)); // glow
        }
        _renderer.SetPropertyBlock(_mpb);
    }
}
