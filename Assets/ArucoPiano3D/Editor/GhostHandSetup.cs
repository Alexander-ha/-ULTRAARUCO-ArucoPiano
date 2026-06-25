#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds two Ultraleap Ghost Hands to the CURRENTLY OPEN piano scene and wires them to the
/// ArucoKeyboardController. It instantiates the GhostHands prefab, removes the Leap driver
/// components (HandBinder + HandEnableDisable — we have no Leap hardware), FORCE-ENABLES the
/// renderers (Ultraleap ships them disabled, normally enabled by HandEnableDisable when a hand is
/// tracked), re-parents each hand under a sliding wrapper with a GhostHandRig, and points the
/// controller's hands at them.
///
/// Open Assets/Scenes/PianoModel.unity first. Menu: Tools ▸ ArucoPiano ▸ Add Ghost Hands To Scene.
/// Re-running removes the previously-added hands first (idempotent).
///
/// TUNE after running: rotate GhostHand_L / GhostHand_R's child rig to palm-down over the keys,
/// and set GhostHandRig.bendAxis / curl so the index finger presses DOWN.
/// </summary>
public static class GhostHandSetup
{
    const string PrefabPath =
        "Packages/com.ultraleap.tracking/Hands/Runtime/Prefabs/Built In Render Pipeline (Dynamically Upgradable)/GhostHands.prefab";

    const float HandScale = 0.5f;
    const float HoverGap = 0.03f;

    [MenuItem("Tools/ArucoPiano/Add Ghost Hands To Scene")]
    public static void AddHands()
    {
        var ctrl = Object.FindAnyObjectByType<ArucoKeyboardController>();
        if (ctrl == null || ctrl.pianoRoot == null)
        {
            EditorUtility.DisplayDialog("ArucoPiano",
                "Open the piano scene (Assets/Scenes/PianoModel.unity) first — no ArucoKeyboardController found.", "OK");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("ArucoPiano", $"GhostHands prefab not found:\n{PrefabPath}", "OK");
            return;
        }

        var pianoRoot = ctrl.pianoRoot;

        // Idempotent: remove previously-added wrappers.
        foreach (var n in new[] { "GhostHand_L", "GhostHand_R" })
        {
            var old = pianoRoot.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "GhostHands_temp";
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // Find the two hands robustly: direct children of the instance that contain a "Wrist" bone.
        var handRoots = new List<Transform>();
        foreach (Transform child in instance.transform)
            if (child.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Wrist"))
                handRoots.Add(child);

        if (handRoots.Count == 0)
        {
            Debug.LogError("[ArucoPiano] Could not find hand roots (no 'Wrist' under the prefab's children). " +
                           "Children were: " + string.Join(", ", instance.transform.Cast<Transform>().Select(t => t.name)));
            Object.DestroyImmediate(instance);
            return;
        }

        Vector3 keyTopFront = KeyTopFrontLocal(ctrl, pianoRoot);
        float hoverY = keyTopFront.y + HoverGap;
        float frontZ = keyTopFront.z;
        var xs = InteractiveKeyXs(ctrl, pianoRoot);

        var rigs = new List<GhostHandRig>();
        for (int i = 0; i < handRoots.Count && i < 2; i++)
        {
            var handRoot = handRoots[i];

            // Strip the Leap drivers.
            foreach (var c in handRoot.GetComponentsInChildren<MonoBehaviour>(true).ToList())
                if (c != null && (c.GetType().Name == "HandBinder" || c.GetType().Name == "HandEnableDisable"))
                    Object.DestroyImmediate(c);

            // Make everything visible: activate all objects and enable all renderers.
            foreach (var t in handRoot.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);
            var renderers = handRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = true;

            var wrapper = new GameObject(i == 0 ? "GhostHand_L" : "GhostHand_R");
            wrapper.transform.SetParent(pianoRoot, false);
            float x = xs.Count > 0 ? xs[Mathf.Min(i * (xs.Count - 1), xs.Count - 1)] : 0f;
            wrapper.transform.localPosition = new Vector3(x, hoverY, frontZ);

            // Preserve the scale SIGNS — the right hand is a mirror of the left via negative X
            // scale; replacing it with a uniform positive scale turns it into a second left hand.
            Vector3 origScale = handRoot.localScale;
            handRoot.SetParent(wrapper.transform, true);   // keep world while re-parenting
            handRoot.localPosition = Vector3.zero;
            handRoot.localRotation = Quaternion.identity;   // TUNE: palm-down
            handRoot.localScale = new Vector3(
                Mathf.Sign(origScale.x), Mathf.Sign(origScale.y), Mathf.Sign(origScale.z)) * HandScale;

            rigs.Add(wrapper.AddComponent<GhostHandRig>());
            ApplySkin(wrapper);

            Debug.Log($"[ArucoPiano] Hand {i}: renderers={renderers.Length}" +
                      (renderers.Length > 0 ? $", first='{renderers[0].GetType().Name}:{renderers[0].name}'" : "") +
                      $", wrapper worldPos={wrapper.transform.position}");
        }

        Object.DestroyImmediate(instance);

        if (rigs.Count > 0) ctrl.leftHand = rigs[0];
        if (rigs.Count > 1) ctrl.rightHand = rigs[1];

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Selection.activeGameObject = rigs.Count > 0 ? rigs[0].gameObject : ctrl.gameObject;

        Debug.Log($"[ArucoPiano] Added {rigs.Count} Ghost hand(s). If still invisible, check the Console line " +
                  "above for renderer count and worldPos, then tell me. TUNE: rotate GhostHand_L/R child to " +
                  "palm-down; set GhostHandRig bendAxis/curl for the index press.");
    }

    static Vector3 KeyTopFrontLocal(ArucoKeyboardController ctrl, Transform pianoRoot)
    {
        if (ctrl.keys != null)
            foreach (var k in ctrl.keys)
            {
                if (k == null) continue;
                var r = k.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                Bounds b = r.bounds;
                return pianoRoot.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.max.z));
            }
        return new Vector3(0f, 0.02f, 0f);
    }

    static List<float> InteractiveKeyXs(ArucoKeyboardController ctrl, Transform pianoRoot)
    {
        var xs = new List<float>();
        if (ctrl.keys != null)
            foreach (var k in ctrl.keys)
                if (k != null) xs.Add(pianoRoot.InverseTransformPoint(k.transform.position).x);
        xs.Sort();
        return xs;
    }

    // ---- Skin recolour ---------------------------------------------------------------
    [MenuItem("Tools/ArucoPiano/Recolor Hands (Skin)")]
    public static void RecolorMenu()
    {
        var ctrl = Object.FindAnyObjectByType<ArucoKeyboardController>();
        if (ctrl == null || ctrl.pianoRoot == null)
        {
            EditorUtility.DisplayDialog("ArucoPiano", "Open the piano scene with the hands first.", "OK");
            return;
        }
        int n = 0;
        foreach (var name in new[] { "GhostHand_L", "GhostHand_R" })
        {
            var t = ctrl.pianoRoot.Find(name);
            if (t != null) { ApplySkin(t.gameObject); n++; }
        }
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log($"[ArucoPiano] Recolored {n} hand(s) to skin. Tweak the colour on Assets/ArucoPiano3D/HandSkin.mat.");
    }

    static void ApplySkin(GameObject root)
    {
        var mat = SkinMaterial();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }

    static Material SkinMaterial()
    {
        const string path = "Assets/ArucoPiano3D/HandSkin.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        mat = new Material(sh) { name = "HandSkin" };
        var skin = new Color(0.86f, 0.64f, 0.52f); // human skin tone
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", skin);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", skin);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
#endif
