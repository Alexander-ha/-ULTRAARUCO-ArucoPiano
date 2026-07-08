#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only helper for integrating the real piano model (item 4). It logs the hierarchy of
/// Assets/Piano/Piano.fbx so we can see how the keys are named/grouped and wire them precisely.
/// Menu: Tools ▸ ArucoPiano ▸ Inspect Piano Model. Changes nothing in the scene.
/// </summary>
public static class PianoModelInspector
{
    const string ModelPath = "Assets/Piano/Piano.fbx";

    [MenuItem("Tools/ArucoPiano/Inspect Piano Model")]
    public static void Inspect()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (root == null)
        {
            Debug.LogError($"[ArucoPiano] Piano model not found at {ModelPath}. Adjust the path if you moved it.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== {ModelPath} hierarchy (top levels) ===");

        Transform keysParent = null;
        int bestMeshChildren = 0;
        Walk(root.transform, 0, sb, ref keysParent, ref bestMeshChildren, maxDepth: 2);

        sb.AppendLine();
        sb.AppendLine($"LIKELY KEYS PARENT: '{(keysParent ? GetPath(keysParent, root.transform) : "?")}' " +
                      $"with {bestMeshChildren} direct mesh children.");

        if (keysParent != null)
        {
            var kids = keysParent.Cast<Transform>()
                .Where(t => t.GetComponentInChildren<MeshRenderer>() != null)
                .OrderBy(t => t.localPosition.x).ToList();
            sb.AppendLine($"Key children: {kids.Count}");
            if (kids.Count > 0)
            {
                sb.AppendLine($"  first: '{kids[0].name}'  localPos={kids[0].localPosition}");
                sb.AppendLine($"  last:  '{kids[kids.Count - 1].name}'  localPos={kids[kids.Count - 1].localPosition}");
                float minX = kids.Min(t => t.localPosition.x), maxX = kids.Max(t => t.localPosition.x);
                float minZ = kids.Min(t => t.localPosition.z), maxZ = kids.Max(t => t.localPosition.z);
                sb.AppendLine($"  X range: {minX:F3}..{maxX:F3}   Z range: {minZ:F3}..{maxZ:F3}");
                sb.AppendLine("  sample names: " + string.Join(", ", kids.Take(12).Select(t => t.name)));
            }
        }

        sb.AppendLine();
        sb.AppendLine("Model import scale / bounds: " +
                      $"rootScale={root.transform.localScale}");
        var mr = root.GetComponentInChildren<MeshRenderer>();
        if (mr != null) sb.AppendLine($"  a mesh bounds size (world-ish): {mr.bounds.size}");

        Debug.Log(sb.ToString());
        Selection.activeObject = root;
    }

    static void Walk(Transform t, int depth, StringBuilder sb, ref Transform keysParent, ref int best, int maxDepth)
    {
        int meshChildren = 0;
        foreach (Transform c in t)
            if (c.GetComponent<MeshRenderer>() != null) meshChildren++;
        if (meshChildren > best) { best = meshChildren; keysParent = t; }

        if (depth <= maxDepth)
        {
            string indent = new string(' ', depth * 2);
            bool hasMesh = t.GetComponent<MeshRenderer>() != null;
            sb.AppendLine($"{indent}{t.name}  (children={t.childCount}{(hasMesh ? ", mesh" : "")})");
        }

        foreach (Transform c in t)
            Walk(c, depth + 1, sb, ref keysParent, ref best, maxDepth);
    }

    static string GetPath(Transform t, Transform root)
    {
        if (t == root) return t.name;
        var stack = new System.Collections.Generic.List<string>();
        while (t != null && t != root) { stack.Insert(0, t.name); t = t.parent; }
        return string.Join("/", stack);
    }
}
#endif
