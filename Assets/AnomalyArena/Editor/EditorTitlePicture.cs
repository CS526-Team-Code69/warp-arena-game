// The picture of the start screen that the scene shows in the editor while not playing (2026-11-10; see EditorTitlePreview). Menu:
// Anomaly Arena/Editor title picture/Refresh, in Play mode with the title showing: it takes the Game view as it is (the language and
// the art the game opened in) into Assets/AnomalyArena/Editor/EditorTitlePicture.png, and once Play mode ends puts that picture
// into the open scene's EditorTitlePreview object (made the first time) and saves the scene. Run it again whenever the title changes.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena.EditorTools
{
public static class EditorTitlePicture
{
    public const string PicturePath = "Assets/AnomalyArena/Editor/EditorTitlePicture.png";
    public const string ObjectName = "EditorTitlePreview";
    const string PendingKey = "AnomalyArena.EditorTitlePicture.Pending";
    const string Tag = "[EditorTitlePicture] ";

    [MenuItem("Anomaly Arena/Editor title picture/Refresh (in Play mode, on the title)")]
    public static void Refresh()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + "enter Play mode and stay on the title first"); return; }
        var game = GameManager.Instance;
        if (!game || game.RunPhase != ArenaUiState.Title || !ArenaText.Ready) { Debug.LogWarning(Tag + "the title is not showing yet"); return; }
        ScreenCapture.CaptureScreenshot(PicturePath);
        SessionState.SetBool(PendingKey, true);
        Debug.Log(Tag + "picture taken; it goes into the scene when Play mode ends");
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged -= Changed;
        EditorApplication.playModeStateChanged += Changed;
    }

    static void Changed(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false);
        Install();
    }

    /// <summary>Puts the saved picture into the open scene's preview object (made when the scene has none) and saves the scene.</summary>
    [MenuItem("Anomaly Arena/Editor title picture/Put the saved picture into the open scene")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + "leave Play mode first"); return; }
        AssetDatabase.ImportAsset(PicturePath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(PicturePath) is TextureImporter importer
            && (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.npotScale != TextureImporterNPOTScale.None))
        {
            // A picture of an interface: as it is, no smaller copies, no compression, its own size.
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }
        var picture = AssetDatabase.LoadAssetAtPath<Texture2D>(PicturePath);
        if (!picture) { Debug.LogWarning(Tag + "no picture at " + PicturePath + ": take one in Play mode first"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        GameObject root = null;
        foreach (var candidate in scene.GetRootGameObjects())
            if (candidate.name == ObjectName) root = candidate;
        if (!root) root = Build();
        var raw = root.GetComponentInChildren<RawImage>(true);
        raw.texture = picture;
        if (raw.TryGetComponent(out AspectRatioFitter fitter)) fitter.aspectRatio = picture.width / (float)Mathf.Max(1, picture.height);
        EditorUtility.SetDirty(raw);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(Tag + $"the scene {scene.name} shows the start screen's picture ({picture.width} x {picture.height}) while not playing");
    }

    /// <summary>The preview object: its own canvas over every other one, black behind a picture that keeps its shape.</summary>
    static GameObject Build()
    {
        var root = new GameObject(ObjectName, typeof(RectTransform), typeof(Canvas), typeof(EditorTitlePreview));
        root.tag = "EditorOnly";
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var back = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        back.rectTransform.SetParent(root.transform, false);
        Stretch(back.rectTransform);
        back.color = Color.black;
        back.raycastTarget = false;
        var raw = new GameObject("Picture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
        raw.rectTransform.SetParent(root.transform, false);
        Stretch(raw.rectTransform);
        raw.raycastTarget = false;
        raw.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        Undo.RegisterCreatedObjectUndo(root, "Editor title picture");
        return root;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
}
