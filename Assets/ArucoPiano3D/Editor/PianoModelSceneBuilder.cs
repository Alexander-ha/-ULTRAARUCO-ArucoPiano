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

    // Two groups of 4 white + 1 black, one per octave/hand = 10 interactive keys (5 per hand).
    const int GroupWhite = 4;
    const int GroupBlack = 1;

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

        // --- Pick 8 white + 2 black interactive keys, contiguous near the centre.
        // Black keys are taller, so their top sits higher than the white keys.
        var topY = new float[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            var r = keys[i].GetComponent<Renderer>();
            topY[i] = r != null ? r.bounds.max.y : 0f;
        }
        float minTop = topY.Min(), maxTop = topY.Max();
        float blackThreshold = minTop + (maxTop - minTop) * 0.5f;
        System.Func<int, bool> isBlack = i => topY[i] > blackThreshold;

        // Collect a group of (wantW white + wantB black) scanning right from 'start'.
        System.Func<int, List<int>> collect = start =>
        {
            var g = new List<int>(); int w = 0, b = 0;
            for (int i = Mathf.Clamp(start, 0, keys.Count - 1);
                 i < keys.Count && (w < GroupWhite || b < GroupBlack); i++)
            {
                if (isBlack(i)) { if (b < GroupBlack) { g.Add(i); b++; } }
                else { if (w < GroupWhite) { g.Add(i); w++; } }
            }
            return g;
        };

        // Two groups about an octave apart -> one per hand (lower octave + upper octave).
        int mid = keys.Count / 2;
        var sel = collect(mid - 18).Concat(collect(mid + 2))
            .Distinct().OrderBy(i => keys[i].localPosition.x).ToList();
        int nWhite = sel.Count(i => !isBlack(i)), nBlack = sel.Count(isBlack);

        var interactive = new List<PianoKey3D>();
        foreach (int idx in sel)
        {
            var keyT = keys[idx];
            float pressSign = 1f;
            var mf = keyT.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                pressSign = mf.sharedMesh.bounds.center.z >= 0f ? 1f : -1f;

            var key = keyT.gameObject.AddComponent<PianoKey3D>();
            key.keyIndex = idx;
            key.isBlack = isBlack(idx);
            key.hingeAxis = Vector3.right;          // lateral axis = keyboard width
            key.hingeOffsetLocal = Vector3.zero;    // origin is already at the rear hinge
            key.pressAngle = (key.isBlack ? 9f : 7f) * pressSign;
            key.useColorFeedback = true;            // no rest tint (keeps model colour); orange when pressed
            key.pressedColor = new Color(1f, 0.45f, 0.1f);
            interactive.Add(key);
        }
        var markerKeys = sel.ToArray();

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
