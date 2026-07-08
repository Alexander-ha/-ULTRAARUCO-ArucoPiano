using UnityEngine;

/// <summary>
/// Common interface the keyboard controller uses to drive a hand, so the same logic works for
/// the simple cube hand (<see cref="Hand3D"/>) and the Ultraleap Ghost-hand rig
/// (<see cref="GhostHandRig"/>). The controller only needs: where the hand is along the keys,
/// and a command to slide there and (optionally) press.
/// </summary>
public abstract class HandDriverBase : MonoBehaviour
{
    /// <summary>Current position of the hand along the keyboard, in pianoRoot-local X.</summary>
    public abstract float CurrentLocalX { get; }

    /// <summary>Slide toward <paramref name="localX"/>; press the finger when <paramref name="pressing"/>.</summary>
    public abstract void SetTarget(float localX, bool pressing);
}
