using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Temporary main-menu behaviour. Replace the button artwork in the scene whenever ready.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string cutsceneSceneName = "Cutscene";

    [Header("UI")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button exitButton;

    public void Configure(Button startButton, Button exitGameButton)
    {
        startGameButton = startButton;
        exitButton = exitGameButton;
    }

    private void Start()
    {
        if (startGameButton == null || exitButton == null)
            BuildFallbackMenu();

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(StartGame);
            startGameButton.onClick.AddListener(StartGame);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGame);
            exitButton.onClick.AddListener(ExitGame);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(cutsceneSceneName);
    }

    public void ExitGame()
    {
        // Intentionally empty for now. This preserves the menu interface without closing the game.
    }

    private void BuildFallbackMenu()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject canvasObject = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        CreatePanel(canvasObject.transform, "Background", new Vector2(0.5f, 0.5f), new Vector2(1920f, 1080f), new Color(0.025f, 0.025f, 0.04f));
        CreateText(canvasObject.transform, "Title", "HORROR GAME", new Vector2(0.5f, 0.68f), new Vector2(850f, 110f), 48, Color.white);
        CreateText(canvasObject.transform, "Subtitle", "A 2D narrative prototype", new Vector2(0.5f, 0.59f), new Vector2(700f, 45f), 20, new Color(0.72f, 0.72f, 0.78f));
        startGameButton = CreateButton(canvasObject.transform, "StartGameButton", "START GAME", new Vector2(0.5f, 0.43f));
        exitButton = CreateButton(canvasObject.transform, "ExitButton", "EXIT", new Vector2(0.5f, 0.32f));
    }

    private static Image CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string name, string value, Vector2 anchor, Vector2 size, int fontSize, Color color)
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

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor)
    {
        Image background = CreatePanel(parent, name, anchor, new Vector2(350f, 74f), new Color(0.23f, 0.11f, 0.13f));
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        CreateText(background.transform, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(320f, 58f), 26, Color.white);
        return button;
    }
}
