#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds a single-camera scene using the REAL piano model (Assets/Piano/Piano.fbx) instead of
/// cube keys. The 88 keys (PianoKey.001..088) are direct children of the model root; their origin
/// sits at the rear hinge line, so a key "press" is just a small rotation about the key's own
/// origin (no pivot guessing). Three keys are made interactive (driven by key_1..key_3).
///
/// Hands are NOT added here — those come with the Ghost Hands step (item 3). The piano still moves
/// in the square floor zone and rotates only with the real marker.
/// Menu: Tools ▸ ArucoPiano ▸ Build 3D Piano Model Scene. Saves Assets/Scenes/PianoModel.unity.
/// </summary>
public static class PianoModelSceneBuilder
{
    const string ModelPath = "Assets/Piano/Piano.fbx";
    const string KeyPrefix = "PianoKey";

    const float TargetWidth = 0.5f;       // scaled keyboard width (world metres)
    const float ZoneSize = 0.7f;          // square the piano centre may move within (bigger now)
    const float CameraFov = 60f;
    const float CameraPitch = 70f;        // mostly top-down, angled so keys aren't hidden by the lid

    // Interactive keys = two full octaves (24 keys), one per hand.
    // 88-key piano: leftmost key (index 0) = A0 = MIDI 21.
    const int MidiOfFirstKey = 21;
    const int LowOctaveMidi = 48;   // малая октава = C3..B3
    const int HighOctaveMidi = 72;  // вторая октава = C5..B5

    // Cover parts that hide the keys from above — hidden on build.
    static readonly string[] HideParts = { "Flatboard", "Felt" };

    [MenuItem("Tools/ArucoPiano/Build 3D Piano Model Scene")]
    public static void Build()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (asset == null)
        {
            EditorUtility.DisplayDialog("ArucoPiano", $"Piano model not found at {ModelPath}.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Fix the magenta look: the model ships with built-in "Standard" materials; convert to URP.
        PianoMaterialConverter.ConvertFolder("Assets/Piano");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Camera + server + light.
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = CameraFov;
        cam.nearClipPlane = 0.02f;
        camGo.AddComponent<AudioListener>();
        var server = camGo.AddComponent<UnityHTTPServer>();
        server.serverPort = 8080;
        float frame = TargetWidth + ZoneSize + 0.1f;
        float dist = frame / (2f * Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad));
        float pitch = CameraPitch * Mathf.Deg2Rad;
        camGo.transform.position = new Vector3(0f, dist * Mathf.Sin(pitch), -dist * Mathf.Cos(pitch));
        camGo.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        // --- Piano model under PianoRoot.
        var root = new GameObject("PianoRoot");
        var model = (GameObject)Object.Instantiate(asset);
        model.name = "PianoModel";
        model.transform.SetParent(root.transform, false);

        // Hide the lid / fallboard parts that cover the keys from above.
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
            foreach (var prefix in HideParts)
                if (t.name.StartsWith(prefix)) { t.gameObject.SetActive(false); break; }

        var keys = model.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith(KeyPrefix))
            .OrderBy(t => t.localPosition.x)
            .ToList();
        if (keys.Count == 0)
        {
            Debug.LogError("[ArucoPiano] No PianoKey.* children found in the model.");
            return;
        }

        // Scale the model so the keyboard is TargetWidth wide, then centre the keyboard on the root.
        float minX = keys.Min(t => t.localPosition.x), maxX = keys.Max(t => t.localPosition.x);
        float modelScale = TargetWidth / Mathf.Max(1e-4f, maxX - minX);
        model.transform.localScale = Vector3.one * modelScale;

        Vector3 center = Vector3.zero;
        foreach (var k in keys) center += k.position;       // world == root-local (root at origin)
        center /= keys.Count;
        model.transform.localPosition -= center;            // keyboard centred at the root origin

        // --- Interactive keys: малая октава (C3..B3) + вторая октава (C5..B5) = 24 keys.
        int[] blackClasses = { 1, 3, 6, 8, 10 };
        string[] noteNames = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
        System.Func<int, int> midiOf = idx => MidiOfFirstKey + idx;
        System.Func<int, bool> inOctaves = idx =>
        {
            int m = midiOf(idx);
            return (m >= LowOctaveMidi && m < LowOctaveMidi + 12) ||
                   (m >= HighOctaveMidi && m < HighOctaveMidi + 12);
        };
        var sel = Enumerable.Range(0, keys.Count).Where(inOctaves)
                            .OrderBy(i => keys[i].localPosition.x).ToList();

        var interactive = new List<PianoKey3D>();
        foreach (int idx in sel)
        {
            var keyT = keys[idx];
            int m = midiOf(idx);
            bool black = System.Array.IndexOf(blackClasses, m % 12) >= 0;

            float pressSign = 1f;
            var mf = keyT.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                pressSign = mf.sharedMesh.bounds.center.z >= 0f ? 1f : -1f;

            var key = keyT.gameObject.AddComponent<PianoKey3D>();
            key.keyIndex = idx;
            key.isBlack = black;
            key.interactive = true;
            key.hingeAxis = Vector3.right;
            key.hingeOffsetLocal = Vector3.zero;
            key.pressAngle = (black ? 9f : 7f) * pressSign;
            key.useColorFeedback = true;            // no rest tint; orange + glow when pressed
            key.pressedColor = new Color(1f, 0.45f, 0.1f);

            // Note sound: "{Note}{Octave}.mp3" (e.g. C3, Db5).
            string note = noteNames[m % 12] + (m / 12 - 1);
            key.noteClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/piano-mp3/{note}.mp3");
            if (key.noteClip == null) Debug.LogWarning($"[ArucoPiano] sound not found: Assets/piano-mp3/{note}.mp3");

            // Enable emission so the key can glow when pressed (add Bloom to a URP Volume for the bloom look).
            var rend = keyT.GetComponent<Renderer>();
            var rmat = rend != null ? rend.sharedMaterial : null;
            if (rmat != null)
            {
                rmat.EnableKeyword("_EMISSION");
                rmat.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                EditorUtility.SetDirty(rmat);
            }
            interactive.Add(key);
        }
        var markerKeys = sel.ToArray();
        int nWhite = sel.Count(i => System.Array.IndexOf(blackClasses, midiOf(i) % 12) < 0);
        int nBlack = sel.Count - nWhite;

        // --- Controller (no hands yet).
        var ctrlGo = new GameObject("ArucoKeyboardController");
        var ctrl = ctrlGo.AddComponent<ArucoKeyboardController>();
        ctrl.server = server;
        ctrl.viewCamera = cam;
        ctrl.pianoRoot = root.transform;
        ctrl.keys = interactive;
        ctrl.markerKeyIndices = markerKeys;
        ctrl.zoneCenter = Vector3.zero;
        ctrl.zoneSize = ZoneSize;
        ctrl.handTravelHalfWidth = TargetWidth * 0.5f;
        // Hand calibration anchors: where the малая / вторая octave centres sit (keyboard-local X).
        var lowKeys = sel.Where(i => midiOf(i) < 60).ToList();   // малая (C3..B3)
        var highKeys = sel.Where(i => midiOf(i) >= 72).ToList(); // вторая (C5..B5)
        ctrl.leftAnchorX = lowKeys.Count > 0 ? lowKeys.Average(i => root.transform.InverseTransformPoint(keys[i].position).x) : -0.1f;
        ctrl.rightAnchorX = highKeys.Count > 0 ? highKeys.Average(i => root.transform.InverseTransformPoint(keys[i].position).x) : 0.1f;
        ctrl.handAnchorFraction = 0.3f;
        ctrl.heightMin = 0f;
        ctrl.heightMax = 0.2f;
        ctrl.useMarkerRotation = true;
        ctrl.leftHand = null;
        ctrl.rightHand = null;

        Selection.activeGameObject = ctrlGo;

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/PianoModel.unity");

        Debug.Log($"[ArucoPiano] Piano model scene built (Assets/Scenes/PianoModel.unity). " +
                  $"{keys.Count} keys found; {nWhite} white + {nBlack} black interactive (highlighted blue, " +
                  $"key_1..key_{markerKeys.Length}, left->right). After building, run 'Add Ghost Hands To Scene'. " +
                  $"If a key tilts the wrong way, flip its Press Angle sign.");
    }
}
#endif
