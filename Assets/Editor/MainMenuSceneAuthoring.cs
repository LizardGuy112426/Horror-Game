using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates a persistent, editable 2D Main Menu hierarchy.</summary>
public static class MainMenuSceneAuthoring
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Horror Game/Setup Editable Main Menu")]
    public static void EnsureEditableMainMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool closeAfterSetup = !scene.IsValid() || !scene.isLoaded;
        if (closeAfterSetup)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        bool changed = ConfigureScene(scene);
        if (changed)
            EditorSceneManager.SaveScene(scene);

        if (closeAfterSetup)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static bool ConfigureScene(Scene scene)
    {
        MainMenuController controller = FindComponent<MainMenuController>(scene);
        if (controller == null)
        {
            GameObject controllerObject = new GameObject("MainMenuController");
            SceneManager.MoveGameObjectToScene(controllerObject, scene);
            controller = controllerObject.AddComponent<MainMenuController>();
        }

        GameObject existingCanvas = FindGameObject(scene, "Main Menu Canvas");
        if (existingCanvas != null)
            return LinkExistingButtons(controller, existingCanvas);

        GameObject canvasObject = new GameObject(
            "Main Menu Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateImage(
            canvasObject.transform,
            "Background",
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Color(0.025f, 0.025f, 0.04f, 1f));

        CreateText(
            canvasObject.transform,
            "Game Title",
            "HORROR GAME",
            new Vector2(0.5f, 0.68f),
            new Vector2(850f, 110f),
            48,
            Color.white);
        CreateText(
            canvasObject.transform,
            "Subtitle",
            "A 2D narrative prototype",
            new Vector2(0.5f, 0.59f),
            new Vector2(700f, 45f),
            20,
            new Color(0.72f, 0.72f, 0.78f, 1f));

        Button startButton = CreateButton(
            canvasObject.transform,
            "Start Game Button",
            "START GAME",
            new Vector2(0.5f, 0.43f));
        Button exitButton = CreateButton(
            canvasObject.transform,
            "Exit Button",
            "EXIT",
            new Vector2(0.5f, 0.32f));
        Button optionButton = CreateButton(canvasObject.transform, "OptionButton", "OPTION", new Vector2(0.5f, 0.22f));

        if (FindComponent<EventSystem>(scene) == null)
        {
            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        controller.Configure(startButton, optionButton, exitButton);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static bool LinkExistingButtons(MainMenuController controller, GameObject canvasObject)
    {
        Button startButton = FindChildButton(canvasObject.transform, "Start Game Button");
        Button exitButton = FindChildButton(canvasObject.transform, "Exit Button");
        if (startButton == null || exitButton == null)
            return false;

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty startProperty = serializedController.FindProperty("startGameButton");
        SerializedProperty exitProperty = serializedController.FindProperty("exitButton");
        bool needsUpdate = startProperty.objectReferenceValue != startButton
            || exitProperty.objectReferenceValue != exitButton;
        if (!needsUpdate)
            return false;

        startProperty.objectReferenceValue = startButton;
        exitProperty.objectReferenceValue = exitButton;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static Image CreateImage(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color color)
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

    private static Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchor,
        Vector2 size,
        int fontSize,
        Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchor)
    {
        Image background = CreateImage(
            parent,
            name,
            anchor,
            anchor,
            Vector2.zero,
            Vector2.zero,
            new Color(0.23f, 0.11f, 0.13f, 1f));
        background.rectTransform.sizeDelta = new Vector2(350f, 74f);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        CreateText(
            background.transform,
            "Label",
            label,
            new Vector2(0.5f, 0.5f),
            new Vector2(320f, 58f),
            26,
            Color.white);
        return button;
    }

    private static Button FindChildButton(Transform parent, string name)
    {
        foreach (Button button in parent.GetComponentsInChildren<Button>(true))
        {
            if (button.name == name)
                return button;
        }
        return null;
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }

    private static GameObject FindGameObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child.gameObject;
            }
        }
        return null;
    }
}
