#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts the piano model's built-in "Standard" materials to URP/Lit so they stop rendering
/// magenta in this URP project. Menu: Tools ▸ ArucoPiano ▸ Convert Piano Materials to URP.
/// Also called automatically by the piano-model scene builder.
/// </summary>
public static class PianoMaterialConverter
{
    [MenuItem("Tools/ArucoPiano/Convert Piano Materials to URP")]
    public static void ConvertMenu()
    {
        int n = ConvertFolder("Assets/Piano");
        Debug.Log($"[ArucoPiano] Converted {n} piano material(s) to URP/Lit.");
    }

    public static int ConvertFolder(string folder)
    {
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null)
        {
            Debug.LogWarning("[ArucoPiano] URP/Lit shader not found (is this a URP project?). Skipping conversion.");
            return 0;
        }

        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == urp) continue;
            Convert(mat, urp);
            count++;
        }
        AssetDatabase.SaveAssets();
        return count;
    }

    static void Convert(Material mat, Shader urp)
    {
        // Capture the built-in properties before swapping the shader.
        Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color")
                  : mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
        Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
        float smooth = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness")
                     : mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;

        mat.shader = urp;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(mat);
    }
}
#endif
