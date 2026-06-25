#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only helper for the Ghost Hands integration (item 3). Logs the GhostHands prefab's rig
/// (bones, the index finger's rest rotations), its driver components, and the material shader
/// (to check it renders in URP). Menu: Tools ▸ ArucoPiano ▸ Inspect Ghost Hand. Changes nothing.
/// </summary>
public static class GhostHandInspector
{
    const string PrefabPath =
        "Packages/com.ultraleap.tracking/Hands/Runtime/Prefabs/Built In Render Pipeline (Dynamically Upgradable)/GhostHands.prefab";

    [MenuItem("Tools/ArucoPiano/Inspect Ghost Hand")]
    public static void Inspect()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[ArucoPiano] GhostHands prefab not found at:\n{PrefabPath}");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== GhostHands prefab ===");

        sb.AppendLine("Components on root:");
        foreach (var c in root.GetComponents<Component>())
            sb.AppendLine("  - " + (c != null ? c.GetType().Name : "<missing>"));

        // Driver components anywhere in the prefab.
        var allComps = root.GetComponentsInChildren<Component>(true)
            .Where(c => c != null)
            .Select(c => c.GetType().Name)
            .Where(n => n.Contains("HandBinder") || n.Contains("EnableDisable") || n.Contains("HandModel") || n.Contains("PostProcess"))
            .Distinct();
        sb.AppendLine("Driver-ish components found: " + string.Join(", ", allComps));

        // Key bones.
        LogBone(sb, root.transform, "Wrist");
        LogBone(sb, root.transform, "index_a");
        LogBone(sb, root.transform, "index_b");
        LogBone(sb, root.transform, "index_c");
        LogBone(sb, root.transform, "index_end");

        // How many "Wrist" roots (one per hand expected = 2).
        int wrists = root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Wrist");
        sb.AppendLine($"Wrist count (hands): {wrists}");

        // Material / shader (URP check).
        var r = root.GetComponentInChildren<Renderer>(true);
        if (r != null && r.sharedMaterial != null)
            sb.AppendLine($"First renderer material '{r.sharedMaterial.name}' shader = '{r.sharedMaterial.shader.name}'");
        else
            sb.AppendLine("No renderer/material found on prefab (mesh may be assigned at runtime).");

        Debug.Log(sb.ToString());
        Selection.activeObject = root;
    }

    static void LogBone(StringBuilder sb, Transform root, string name)
    {
        var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
        if (t == null) { sb.AppendLine($"{name}: NOT FOUND"); return; }
        sb.AppendLine($"{name}: localPos={t.localPosition}  localEuler={t.localEulerAngles}  parent='{(t.parent ? t.parent.name : "-")}'");
    }
}
#endif
