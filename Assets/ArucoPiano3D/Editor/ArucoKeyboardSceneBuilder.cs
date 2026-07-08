#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds a fresh, single-camera 3D scene with a row of keys and two hands, driven by the HTTP
/// bridge. Menu: Tools ▸ ArucoPiano ▸ Build 3D Keys Scene.
///
/// 1 piano marker moves the whole key row in 3 coordinates (kept fully in view); 3 key markers
/// press 3 of the keys; 2 hand markers slide two one-finger hands along the keys. The nearest
/// reachable hand glides to a pressed key and its finger presses it. Saves Assets/Scenes/Keys3D.unity.
/// </summary>
public static class ArucoKeyboardSceneBuilder
{
    const int KeyCount = 14;
    static readonly int[] MarkerKeyIndices = { 3, 7, 11 }; // key_1..key_3 -> these keys

    const float KeyWidth = 0.045f, KeyGap = 0.006f, KeyLength = 0.16f, KeyHeight = 0.02f;
    const float HandHoverY = 0.085f, HandHoverZ = 0.10f;

    // Fixed top-down camera (straight down), framed to the square movement zone.
    const float ZoneSize = 1.0f;
    const float CameraFov = 60f;

    [MenuItem("Tools/ArucoPiano/Build 3D Keys Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Single camera + HTTP server on it.
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = CameraFov;
        cam.nearClipPlane = 0.03f;
        camGo.AddComponent<AudioListener>();
        var server = camGo.AddComponent<UnityHTTPServer>();
        server.serverPort = 8080;
        // Straight down, height chosen so the square zone fills the view. Tweak to match your real rig.
        float camHeight = ZoneSize / (2f * Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad));
        camGo.transform.SetPositionAndRotation(new Vector3(0f, camHeight, 0f), Quaternion.Euler(90f, 0f, 0f));

        // --- Light.
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // --- Keyboard + hands.
        var root = new GameObject("PianoRoot");
        var keys = BuildKeys(root.transform);

        float total = (KeyCount - 1) * (KeyWidth + KeyGap);
        var leftHand = BuildHand(root.transform, "LeftHand", new Color(0.4f, 0.85f, 0.5f), new Color(0.95f, 0.8f, 0.6f), -total * 0.25f);
        var rightHand = BuildHand(root.transform, "RightHand", new Color(1f, 0.6f, 0.3f), new Color(0.95f, 0.8f, 0.6f), total * 0.25f);

        // --- Controller.
        var ctrlGo = new GameObject("ArucoKeyboardController");
        var ctrl = ctrlGo.AddComponent<ArucoKeyboardController>();
        ctrl.server = server;
        ctrl.viewCamera = cam;
        ctrl.pianoRoot = root.transform;
        ctrl.keys = keys;
        ctrl.markerKeyIndices = MarkerKeyIndices;
        ctrl.leftHand = leftHand;
        ctrl.rightHand = rightHand;
        // Square floor zone the piano moves within; rotation comes only from the real marker.
        ctrl.zoneSize = ZoneSize;
        ctrl.zoneCenter = Vector3.zero;
        ctrl.useMarkerRotation = true;

        Selection.activeGameObject = ctrlGo;

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Keys3D.unity");

        Debug.Log("[ArucoPiano] 3D keys+hands scene built (Assets/Scenes/Keys3D.unity). Press Play, " +
                  "open the phone camera, point at the piano + 3 key markers + 2 hand markers. " +
                  "Blue keys are the 3 marker-controlled keys.");
    }

    static List<PianoKey3D> BuildKeys(Transform root)
    {
        var keys = new List<PianoKey3D>(KeyCount);
        float pitch = KeyWidth + KeyGap;
        float total = (KeyCount - 1) * pitch;

        for (int i = 0; i < KeyCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Key_" + i;
            go.transform.SetParent(root, false);
            go.transform.localScale = new Vector3(KeyWidth, KeyHeight, KeyLength);
            go.transform.localPosition = new Vector3(i * pitch - total * 0.5f, 0f, KeyLength * 0.5f);
            RemoveCollider(go);

            var key = go.AddComponent<PianoKey3D>();
            key.keyIndex = i;
            key.hingeAxis = Vector3.right;
            key.hingeOffsetLocal = new Vector3(0f, 0f, -KeyLength * 0.5f);
            key.pressAngle = 11f;

            bool mapped = System.Array.IndexOf(MarkerKeyIndices, i) >= 0;
            key.restColor = mapped ? new Color(0.45f, 0.7f, 1f) : Color.white;
            key.pressedColor = new Color(1f, 0.45f, 0.1f);
            keys.Add(key);
        }
        return keys;
    }

    // Hand = unscaled empty parent (so the finger isn't distorted by the body cube's scale),
    // with a body cube and a straight finger as children.
    static Hand3D BuildHand(Transform root, string name, Color bodyColor, Color fingerColor, float localX)
    {
        var handGo = new GameObject(name);
        handGo.transform.SetParent(root, false);
        handGo.transform.localPosition = new Vector3(localX, HandHoverY, HandHoverZ);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(handGo.transform, false);
        body.transform.localScale = new Vector3(0.05f, 0.03f, 0.05f);
        RemoveCollider(body);
        body.GetComponent<Renderer>().sharedMaterial = Mat(bodyColor);

        var finger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finger.name = "Finger";
        finger.transform.SetParent(handGo.transform, false);
        finger.transform.localScale = new Vector3(0.012f, 0.05f, 0.012f);
        finger.transform.localPosition = new Vector3(0f, -0.04f, 0.012f); // hangs down toward the keys
        RemoveCollider(finger);
        finger.GetComponent<Renderer>().sharedMaterial = Mat(fingerColor);

        var hand = handGo.AddComponent<Hand3D>();
        hand.finger = finger.transform;
        hand.fingerPressDrop = 0.03f;
        return hand;
    }

    static void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    static Material Mat(Color c)
    {
        var s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var m = new Material(s);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }
}
#endif
