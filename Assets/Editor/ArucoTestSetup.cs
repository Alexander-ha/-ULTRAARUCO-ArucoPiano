#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup for the visual smoke test. Adds a <see cref="MinimalArucoTest"/> to the
/// open scene and wires it to the existing <see cref="UnityHTTPServer"/> (on the Main Camera).
/// Menu: Tools ▸ ArucoPiano ▸ Setup Minimal Visual Test.
/// </summary>
public static class ArucoTestSetup
{
    [MenuItem("Tools/ArucoPiano/Setup Minimal Visual Test")]
    public static void Setup()
    {
        var server = Object.FindAnyObjectByType<UnityHTTPServer>();
        if (server == null)
        {
            EditorUtility.DisplayDialog("ArucoPiano",
                "No UnityHTTPServer found in the open scene.\n\nOpen Assets/Scenes/SampleScene first " +
                "(it has UnityHTTPServer on the Main Camera), then run this again.", "OK");
            return;
        }

        var existing = Object.FindAnyObjectByType<MinimalArucoTest>();
        if (existing == null)
        {
            var go = new GameObject("ArucoMinimalTest");
            existing = go.AddComponent<MinimalArucoTest>();
        }

        existing.server = server;
        existing.viewCamera = server.GetComponent<Camera>();

        EditorUtility.SetDirty(existing);
        EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
        Selection.activeGameObject = existing.gameObject;

        Debug.Log("[ArucoPiano] Minimal visual test is set up and wired. Press Play and point the phone at the markers.");
    }
}
#endif
