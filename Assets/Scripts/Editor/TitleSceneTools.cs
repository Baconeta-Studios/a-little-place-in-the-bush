using System.IO;
using System.Linq;
using TMPro;
using UI.Popups;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools > Bush > Build Title Scene: makes Assets/Scenes/Title.unity from the default layout and puts it
/// first in the build. After that it's an ordinary scene to edit by hand, and rebuilding loses those edits.
/// </summary>
public static class TitleSceneTools
{
    private const string ScenePath = "Assets/Scenes/Title.unity";
    private const string CafePath = "Assets/Scenes/Cafe.unity";
    private const string PopupCanvasPath = "Assets/Prefabs/UI/Popups/PopupCanvas.prefab";

    [MenuItem("Tools/Bush/Build Title Scene", priority = 40)]
    private static void BuildFromMenu()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Build Title Scene",
                "Title.unity already exists. Rebuilding it loses your edits.", "Rebuild", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = new Color(0.27f, 0.22f, 0.16f);

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        // The same input setup as the cafe, rather than a copy of it saved in this scene.
        Scene cafe = EditorSceneManager.OpenScene(CafePath, OpenSceneMode.Additive);
        InputSystemUIInputModule cafeInput = cafe.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<InputSystemUIInputModule>(true)).First();
        EditorUtility.CopySerialized(cafeInput, events.GetComponent<InputSystemUIInputModule>());
        EditorSceneManager.CloseScene(cafe, true);

        var root = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var screen = root.AddComponent<TitleScreen>();

        Sprite background = Art("Assets/Images/Backgrounds/Background.png");
        Image backdrop = Picture("Background", root.transform, background);
        GameUi.Stretch(backdrop.rectTransform);
        var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = background.rect.width / background.rect.height;

        RectTransform safe = GameUi.NewRect("SafeArea", root.transform);
        GameUi.Stretch(safe);

        Image shadow = Picture("GeckoShadow", safe, Art("Assets/Images/UI/Soft-shadow.png"));
        shadow.color = new Color(0.1f, 0.07f, 0.04f, 0.45f);
        GameUi.Place(shadow.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(350f, 212f), new Vector2(430f, 66f));

        Image gecko = Picture("Gecko", safe, Art("Assets/Images/Characters/Gecko.png"));
        gecko.preserveAspect = true;
        GameUi.Place(gecko.rectTransform, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(330f, 196f), new Vector2(660f, 660f));

        TextMeshProUGUI title = GameUi.AddText(safe, "A Little Place\nin the Bush", 104f, GameUi.Cream, TextAlignmentOptions.TopLeft, "GameTitle");
        UiFonts.UseTitle(title);
        title.lineSpacing = -14f;
        GameUi.Place(title.rectTransform, Vector2.one, Vector2.one, new Vector2(-60f, -54f), new Vector2(500f, 280f));
        title.rectTransform.localEulerAngles = new Vector3(0f, 0f, 2f);

        RectTransform row = GameUi.NewRect("Buttons", safe);
        GameUi.Place(row, Vector2.zero, Vector2.zero, new Vector2(70f, 46f), new Vector2(1140f, 100f));
        GameUi.AddLayout<HorizontalLayoutGroup>(row, 20f).childAlignment = TextAnchor.MiddleLeft;
        var rowSize = new Vector2(270f, 100f);
        Button newGame = NewButton(row, "New game", rowSize, GameUi.Apron, GameUi.Cream, 38f);
        Button resume = NewButton(row, "Continue", rowSize, GameUi.Cream, GameUi.Ink, 38f);
        Button settings = NewButton(row, "Settings", rowSize, GameUi.Cream, GameUi.Ink, 38f);
        Button credits = NewButton(row, "Credits", rowSize, GameUi.Cream, GameUi.Ink, 38f);

        // Asking before a new game replaces a save.
        SimplePopup confirm = Sheet(root.transform, "ConfirmNew", new Vector2(980f, 440f), "Start a new game?",
            "The cafe you have now will be gone for good.", out RectTransform confirmPaper);
        Button startAgain = NewButton(confirmPaper, "Start again", new Vector2(300f, 84f), GameUi.Apron, GameUi.Cream, 34f);
        startAgain.GetComponent<LayoutElement>().ignoreLayout = true;
        GameUi.Place((RectTransform)startAgain.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-284f, 76f), new Vector2(300f, 84f));
        Button confirmBack = BackButton(confirmPaper);

        // The credits.
        SimplePopup creditsSheet = Sheet(root.transform, "Credits", new Vector2(1100f, 680f), "Credits",
            "Made by Baconeta Studios\n\nVoices: (Josh: add the kids' names here)\n\nFonts: Caveat Brush and Pangolin", out RectTransform creditsPaper);
        Button creditsBack = BackButton(creditsPaper);

        // Only its settings are used here: no pausing, and so no cog.
        var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupCanvasPath);
        var popupCanvas = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
        var pauseInput = popupCanvas.GetComponentInChildren<PauseInput>(true);
        pauseInput.AllowPause = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(pauseInput);
        Popup settingsPopup = popupCanvas.GetComponentsInChildren<Popup>(true).First(p => p.name == "SettingsPopup");

        var so = new SerializedObject(screen);
        Set(so, "newGameButton", newGame);
        Set(so, "continueButton", resume);
        Set(so, "settingsButton", settings);
        Set(so, "creditsButton", credits);
        Set(so, "confirmNew", confirm);
        Set(so, "startAgainButton", startAgain);
        Set(so, "confirmBackButton", confirmBack);
        Set(so, "creditsPopup", creditsSheet);
        Set(so, "creditsBackButton", creditsBack);
        Set(so, "settingsPopup", settingsPopup);
        Set(so, "safeArea", safe);
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("UI");

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInBuild();
        Debug.Log("[Bush] Built the title scene.");
    }

    private static SimplePopup Sheet(Transform parent, string name, Vector2 size, string heading, string body, out RectTransform paper)
    {
        RectTransform sheet = GameUi.NewRect(name, parent);
        GameUi.Stretch(sheet);
        var popup = sheet.gameObject.AddComponent<SimplePopup>();

        RectTransform dim = GameUi.NewRect("Dim", sheet);
        GameUi.Stretch(dim);
        GameUi.AddPanel(dim, new Color(0.08f, 0.07f, 0.05f, 0.5f), false);

        paper = GameUi.NewRect("Paper", sheet);
        GameUi.Place(paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var image = paper.gameObject.AddComponent<Image>();
        image.sprite = Art("Assets/Images/UI/Panel.png");
        image.type = Image.Type.Tiled;
        image.color = GameUi.Panel2;

        TextMeshProUGUI title = GameUi.AddText(paper, heading, 72f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        UiFonts.UseTitle(title);
        GameUi.Anchor(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(84f, -180f), new Vector2(-84f, -70f));

        TextMeshProUGUI text = GameUi.AddText(paper, body, 38f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Text");
        GameUi.Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(84f, 190f), new Vector2(-84f, -180f));

        sheet.gameObject.SetActive(false);
        return popup;
    }

    private static Button BackButton(RectTransform paper)
    {
        Button back = GameUi.AddBack(paper);
        Rounded(back);
        ((RectTransform)back.transform).anchoredPosition = new Vector2(-70f, 76f);
        UiFonts.UseBold(back.GetComponentInChildren<TextMeshProUGUI>());
        return back;
    }

    private static Button NewButton(Transform parent, string label, Vector2 size, Color background, Color text, float fontSize)
    {
        Button button = GameUi.AddButton(parent, label, null, size, background, text, fontSize);
        Rounded(button);
        UiFonts.UseBold(button.GetComponentInChildren<TextMeshProUGUI>());
        return button;
    }

    // The saved sprite, so the scene keeps it.
    private static void Rounded(Button button)
    {
        ((Image)button.targetGraphic).sprite = Art("Assets/Images/UI/Generated/RoundedPanel.png");
    }

    private static Image Picture(string name, Transform parent, Sprite sprite)
    {
        var image = GameUi.NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite Art(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!sprite) Debug.LogError($"[Bush] Couldn't find {path}.");
        return sprite;
    }

    private static void Set(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;

    private static void PutFirstInBuild()
    {
        var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
    }
}
