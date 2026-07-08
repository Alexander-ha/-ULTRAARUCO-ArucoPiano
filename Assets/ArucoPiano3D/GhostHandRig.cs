using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Multi-finger hand driver for the Ultraleap GenericHand rig (no Leap hardware).
/// Each of the 5 fingers reaches its assigned key's contact point with CCD inverse kinematics
/// (axis-free). Index/middle/ring idle in a pre-curled "playing" pose spread evenly over their 3
/// joints; the thumb and pinky stay straight. The hand slides so the ASSIGNED fingers line up with
/// their keys (not the hand centre), so fingers actually land on the keys.
///
/// Chords: a finger keeps its key; a new nearby pressed key takes a DIFFERENT free finger.
/// Press points: white+thumb -> edge, white+other -> centre, black -> edge (index/middle/ring only).
/// </summary>
public class GhostHandRig : HandDriverBase
{
    public enum FingerId { Thumb = 0, Index = 1, Middle = 2, Ring = 3, Pinky = 4 }
    static readonly string[] Names = { "thumb", "index", "middle", "ring", "pinky" };

    [Header("Slide")]
    public float moveSpeed = 8f;

    [Header("Finger IK")]
    [Range(1, 12)] public int ccdIterations = 6;
    public float weightSpeed = 10f;
    [Tooltip("Resting curl applied to EACH joint of index/middle/ring/pinky (degrees). Thumb stay straight.")]
    public float idleCurlAngle = 14f;
    [Tooltip("A finger counts as pressing only if its tip gets within this of the contact (metres).")]
    public float contactThreshold = 0.014f;
    [Tooltip("A finger won't be assigned to a contact farther than this from its base (metres).")]
    public float maxReach = 0.06f;
    [Tooltip("The hand may pull at most this far from the marker position to reach keys (metres).")]
    public float maxPull = 0.05f;

    class Finger
    {
        public FingerId id;
        public Transform[] joints;     // a, b, (c)
        public Transform tip;          // _end
        public Quaternion[] bind;
        public Quaternion[] idleLocal; // scratch
        public Quaternion[] activeLocal;
        public Vector3 bindTipLocal;   // tip in wrist-local at bind
        public float offsetX;          // tip X relative to the hand centre (wrapper-local)
        public PianoKey3D key;
        public Vector3 contact;
        public bool hasContact;
        public float weight;
        public bool pressing;
        public bool IdleCurls => id == FingerId.Index || id == FingerId.Middle || id == FingerId.Ring || id == FingerId.Pinky;
    }

    Transform _wrist;
    Finger[] _fingers;
    Finger[] _ordered;                 // fingers sorted left->right by X (for no-crossing assignment)
    readonly List<PianoKey3D> _sortKeys = new List<PianoKey3D>();
    bool _ready;

    Transform Wrist => _wrist != null ? _wrist : transform;
    public override float CurrentLocalX => transform.localPosition.x;
    public override void SetTarget(float localX, bool pressing) { } // controller uses DriveChord

    void Awake() => Build();

    void Build()
    {
        _wrist = FindByName(transform, "Wrist");
        var root = Wrist;
        var list = new List<Finger>();
        for (int t = 0; t < Names.Length; t++)
        {
            var a = FindByName(root, Names[t] + "_a");
            var b = FindByName(root, Names[t] + "_b");
            var c = FindByName(root, Names[t] + "_c");
            var tip = FindByName(root, Names[t] + "_end");
            if (a == null || tip == null) continue;

            var joints = new List<Transform> { a };
            if (b != null) joints.Add(b);
            if (c != null) joints.Add(c);

            var f = new Finger { id = (FingerId)t, joints = joints.ToArray(), tip = tip };
            int n = f.joints.Length;
            f.bind = new Quaternion[n];
            f.idleLocal = new Quaternion[n];
            f.activeLocal = new Quaternion[n];
            for (int i = 0; i < n; i++) f.bind[i] = f.joints[i].localRotation;
            f.bindTipLocal = root.InverseTransformPoint(tip.position);
            f.offsetX = transform.InverseTransformPoint(tip.position).x; // tip X relative to hand centre
            list.Add(f);
        }
        _fingers = list.ToArray();
        _ordered = (Finger[])_fingers.Clone();
        System.Array.Sort(_ordered, (a, b) => a.offsetX.CompareTo(b.offsetX));
        _ready = _fingers.Length > 0;
        if (!_ready) Debug.LogWarning($"[GhostHandRig] '{name}': no finger bones found.");
    }

    static bool Allowed(FingerId id, PianoKey3D key)
    {
        if (key.isBlack) return id == FingerId.Index || id == FingerId.Middle || id == FingerId.Ring || id == FingerId.Pinky;
        return true;
    }

    static Vector3 ContactFor(Finger f, PianoKey3D key)
    {
        if (key.isBlack) return key.EdgeContact;
        return f.id == FingerId.Thumb ? key.EdgeContact : key.CenterContact;
    }

    /// <summary>Drive this hand for one frame.</summary>
    public void DriveChord(List<PianoKey3D> keys, float idleLocalX)
    {
        if (!_ready) return;
        var parent = transform.parent;

        // 1-2. Fresh ORDER-PRESERVING assignment each frame: keys left->right map to fingers
        // left->right, so fingers never cross. All reachable active keys stay pressed (chord).
        foreach (var f in _fingers) f.key = null;
        if (keys != null && keys.Count > 0 && parent != null)
        {
            _sortKeys.Clear();
            foreach (var k in keys) if (k != null) _sortKeys.Add(k);
            _sortKeys.Sort((a, b) => KeyX(parent, a).CompareTo(KeyX(parent, b)));

            float handX = transform.localPosition.x;
            int fi = 0;
            foreach (var key in _sortKeys)
            {
                float keyX = KeyX(parent, key);
                int best = -1; float bestD = float.MaxValue;
                for (int j = fi; j < _ordered.Length; j++)
                {
                    var f = _ordered[j];
                    if (!Allowed(f.id, key)) continue;
                    if (Vector3.Distance(Wrist.TransformPoint(f.bindTipLocal), ContactFor(f, key)) > maxReach * 2.5f) continue;
                    float d = Mathf.Abs((handX + f.offsetX) - keyX);
                    if (d < bestD) { bestD = d; best = j; }
                }
                if (best >= 0) { _ordered[best].key = key; fi = best + 1; }
                // else: key not assignable here -> stays unpressed
            }
        }

        // 3. Slide so assigned fingers line up with their keys, but PULL at most maxPull from the marker.
        float targetX = idleLocalX; int n = 0; float sum = 0f;
        if (parent != null)
            foreach (var f in _fingers)
                if (f.key != null) { sum += KeyX(parent, f.key) - f.offsetX; n++; }
        if (n > 0) targetX = Mathf.Clamp(sum / n, idleLocalX - maxPull, idleLocalX + maxPull);
        var p = transform.localPosition;
        p.x = Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-moveSpeed * Time.deltaTime));
        transform.localPosition = p;

        // 4. Pose every finger (idle pre-curl <-> CCD to the contact, blended by weight).
        foreach (var f in _fingers)
        {
            bool active = f.key != null;
            if (active) { f.contact = ContactFor(f, f.key); f.hasContact = true; }
            f.weight = Mathf.MoveTowards(f.weight, active ? 1f : 0f, weightSpeed * Time.deltaTime);

            PoseFinger(f);

            f.pressing = active && f.weight > 0.6f && f.hasContact &&
                         (f.tip.position - f.contact).sqrMagnitude <= contactThreshold * contactThreshold;
        }
    }

    public bool IsKeyPressed(PianoKey3D key)
    {
        if (!_ready) return false;
        foreach (var f in _fingers) if (f.key == key && f.pressing) return true;
        return false;
    }

    void PoseFinger(Finger f)
    {
        int n = f.joints.Length;

        // Idle pose: even curl over the 3 joints (index/middle/ring only), about the geometric
        // flexion axis (perpendicular to the bone and world-down) — no per-rig axis tuning.
        for (int i = 0; i < n; i++) f.joints[i].localRotation = f.bind[i];
        if (f.IdleCurls && idleCurlAngle != 0f)
            for (int i = 0; i < n; i++)
            {
                Vector3 next = (i + 1 < n) ? f.joints[i + 1].position : f.tip.position;
                Vector3 boneDir = next - f.joints[i].position;
                Vector3 axis = Vector3.Cross(boneDir, Vector3.down);
                if (axis.sqrMagnitude > 1e-8f)
                    f.joints[i].rotation = Quaternion.AngleAxis(idleCurlAngle, axis.normalized) * f.joints[i].rotation;
            }
        for (int i = 0; i < n; i++) f.idleLocal[i] = f.joints[i].localRotation;

        // Active pose: CCD to the contact.
        if (f.weight > 0.001f && f.hasContact)
        {
            for (int i = 0; i < n; i++) f.joints[i].localRotation = f.bind[i];
            for (int it = 0; it < ccdIterations; it++)
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    Vector3 toTip = f.tip.position - f.joints[i].position;
                    Vector3 toTarget = f.contact - f.joints[i].position;
                    if (toTip.sqrMagnitude < 1e-10f || toTarget.sqrMagnitude < 1e-10f) continue;
                    f.joints[i].rotation = Quaternion.FromToRotation(toTip, toTarget) * f.joints[i].rotation;
                }
                if ((f.tip.position - f.contact).sqrMagnitude < 1e-6f) break;
            }
            for (int i = 0; i < n; i++) f.activeLocal[i] = f.joints[i].localRotation;

            for (int i = 0; i < n; i++)
                f.joints[i].localRotation = Quaternion.Slerp(f.idleLocal[i], f.activeLocal[i], f.weight);
        }
        // else: joints already at idle pose.
    }

    static float KeyX(Transform parent, PianoKey3D key) => parent.InverseTransformPoint(key.transform.position).x;

    static Transform FindByName(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }
}
