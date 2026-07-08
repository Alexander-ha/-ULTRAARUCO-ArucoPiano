using UnityEngine;

/// <summary>
/// A simple hand: a cube body that slides along the keyboard's local X, with one straight
/// finger that dips down to "press" a key. The controller tells it where to slide and whether
/// to press; the motion here is smoothed so the hand glides (never teleports).
/// </summary>
public class Hand3D : HandDriverBase
{
    [Tooltip("The straight finger (child). It drops down when pressing.")]
    public Transform finger;

    [Header("Motion")]
    [Tooltip("How fast the hand slides along the keys.")]
    public float moveSpeed = 8f;
    [Tooltip("How fast the finger presses/releases.")]
    public float fingerSpeed = 14f;
    [Tooltip("How far the finger drops when pressing (local metres).")]
    public float fingerPressDrop = 0.03f;
    [Tooltip("Extra forward tilt of the finger while pressing (degrees).")]
    public float fingerPressTilt = 12f;

    float _targetX;
    bool _pressing;
    float _ft;            // finger press amount 0..1
    float _fingerRestY;
    Quaternion _fingerRestRot;

    public override float CurrentLocalX => transform.localPosition.x;

    void Awake()
    {
        _targetX = transform.localPosition.x; // don't slide on the first frame
        if (finger != null)
        {
            _fingerRestY = finger.localPosition.y;
            _fingerRestRot = finger.localRotation;
        }
    }

    /// <summary>Slide toward <paramref name="localX"/>; press the finger when <paramref name="pressing"/>.</summary>
    public override void SetTarget(float localX, bool pressing)
    {
        _targetX = localX;
        _pressing = pressing;
    }

    void Update()
    {
        var p = transform.localPosition;
        p.x = Mathf.Lerp(p.x, _targetX, 1f - Mathf.Exp(-moveSpeed * Time.deltaTime));
        transform.localPosition = p;

        _ft = Mathf.MoveTowards(_ft, _pressing ? 1f : 0f, fingerSpeed * Time.deltaTime);
        if (finger != null)
        {
            var fp = finger.localPosition;
            fp.y = _fingerRestY - fingerPressDrop * _ft;
            finger.localPosition = fp;
            finger.localRotation = _fingerRestRot * Quaternion.Euler(fingerPressTilt * _ft, 0f, 0f);
        }
    }
}
