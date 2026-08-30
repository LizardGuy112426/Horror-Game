using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the editable scenes once, and remains available at Tools/Horror Game/Build CG Framework.</summary>
public static class StoryFrameworkSceneBuilder
{
    private const string ScenesDirectory = "Assets/Scenes";
    private const string MainMenuPath = ScenesDirectory + "/MainMenu.unity";
    private const string CutscenePath = ScenesDirectory + "/Cutscene.unity";
    private const string EmptyScenePath = ScenesDirectory + "/EmptyScene.unity";

    [InitializeOnLoadMethod]
    private static void BuildMissingFrameworkAfterCompile()
    {
        if (!File.Exists(MainMenuPath) || !File.Exists(CutscenePath) || !File.Exists(EmptyScenePath))
            EditorApplication.delayCall += BuildFramework;
    }

    [MenuItem("Tools/Horror Game/Build CG Framework")]
    public static void BuildFramework()
    {
        EnsureAiFolders();
        BuildMainMenuScene();
        BuildCutsceneScene();
        BuildEmptyScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CG framework created: MainMenu -> Cutscene -> EmptyScene.");
    }

    private static void EnsureAiFolders()
    {
        Directory.CreateDirectory("Assets/0822/CG");
        Directory.CreateDirectory("Assets/0822/UI");
        Directory.CreateDirectory("Assets/0822/Audio");
        AssetDatabase.Refresh();
    }

    private static void BuildMainMenuScene()
    {
        Scene scene = NewEmptyScene();
        CreateCamera();
        Transform canvas = CreateCanvas();
        CreateEventSystem();

        CreateImage(canvas, "Background", new Color(0.025f, 0.025f, 0.04f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateText(canvas, "GameTitle", "HORROR GAME", 48, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), new Vector2(800f, 100f));
        CreateText(canvas, "Subtitle", "A 2D narrative prototype", 20, TextAnchor.MiddleCenter, new Color(0.72f, 0.72f, 0.78f), new Vector2(0.5f, 0.59f), new Vector2(0.5f, 0.59f), new Vector2(620f, 42f));
        Button startButton = CreateButton(canvas, "StartGameButton", "START GAME", new Vector2(0.5f, 0.43f));
        Button exitButton = CreateButton(canvas, "ExitButton", "EXIT", new Vector2(0.5f, 0.32f));
        Button optionButton = CreateButton(canvas, "OptionButton", "OPTION", new Vector2(0.5f, 0.22f));

        MainMenuController controller = new GameObject("MainMenuController").AddComponent<MainMenuController>();
        controller.Configure(startButton, optionButton, exitButton);
        SaveScene(scene, MainMenuPath);
    }

    private static void BuildCutsceneScene()
    {
        Scene scene = NewEmptyScene();
        CreateCamera();
        Transform canvas = CreateCanvas();
        CreateEventSystem();

        CreateImage(canvas, "Background", Color.black, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image cgImage = CreateImage(canvas, "CGImage", new Color(0.09f, 0.1f, 0.14f, 1f), new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);
        cgImage.preserveAspect = true;

        Image dialogueBox = CreateImage(canvas, "DialogueBox", new Color(0.025f, 0.025f, 0.04f, 0.95f), new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.2f), Vector2.zero, Vector2.zero);
        Text nameText = CreateText(dialogueBox.transform, "SpeakerName", "", 24, TextAnchor.MiddleLeft, new Color(0.96f, 0.85f, 0.62f), new Vector2(0.045f, 0.68f), new Vector2(0.55f, 0.95f), Vector2.zero);
        Text bodyText = CreateText(dialogueBox.transform, "DialogueText", "", 25, TextAnchor.UpperLeft, Color.white, new Vector2(0.045f, 0.13f), new Vector2(0.955f, 0.68f), Vector2.zero);
        Text hintText = CreateText(canvas, "ContinueHint", "", 16, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.78f), new Vector2(0.5f, 0.015f), new Vector2(0.5f, 0.015f), new Vector2(500f, 28f));
        hintText.gameObject.SetActive(false);
        Image blackOverlay = CreateImage(canvas, "Opening Black Overlay", Color.black, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        blackOverlay.raycastTarget = true;
        blackOverlay.transform.SetAsLastSibling();

        CutsceneController controller = new GameObject("CutsceneController").AddComponent<CutsceneController>();
        controller.Configure(cgImage, dialogueBox, nameText, bodyText, hintText, blackOverlay);
        SaveScene(scene, CutscenePath);
    }

    private static void BuildEmptyScene()
    {
        Scene scene = NewEmptyScene();
        CreateCamera();
        SaveScene(scene, EmptyScenePath);
    }

    private static Scene NewEmptyScene()
    {
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static Transform CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasObject.transform;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        if (anchorMin == anchorMax)
            rect.sizeDelta = size;
        else
        {
            rect.offsetMin = new Vector2(16f, 8f);
            rect.offsetMax = new Vector2(-16f, -8f);
        }

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor)
    {
        Image image = CreateImage(parent, name, new Color(0.23f, 0.11f, 0.13f, 1f), anchor, anchor, Vector2.zero, Vector2.zero);
        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(350f, 74f);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        CreateText(image.transform, "Label", label, 26, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320f, 58f));
        return button;
    }

    private static void SaveScene(Scene scene, string path)
    {
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuPath, true),
            new EditorBuildSettingsScene(CutscenePath, true),
            new EditorBuildSettingsScene(EmptyScenePath, true)
        };
    }
}
