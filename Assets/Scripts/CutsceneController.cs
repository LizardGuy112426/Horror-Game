using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

[Serializable]
public sealed class CgPage
{
    [Tooltip("Drag the CG sprite for this page here. Leaving it empty shows the neutral placeholder.")]
    public Sprite cgSprite;

    [Tooltip("Optional name shown in the dialogue name bar.")]
    public string speakerName;

    [TextArea(3, 8)]
    [Tooltip("Dialogue is revealed one character at a time.")]
    public string dialogue;
}

/// <summary>
/// A four-page CG player. While text is typing, click/Space/Enter completes it; on prior pages,
/// the next input advances. The final page proceeds to the empty scene after its text completes.
/// </summary>
public sealed class CutsceneController : MonoBehaviour
{
    public const int RequiredPageCount = 4;

    [Header("Four CG pages")]
    [SerializeField] private CgPage[] pages = new CgPage[RequiredPageCount];

    [Header("UI references")]
    [SerializeField] private Image cgImage;
    [SerializeField] private Image dialogueBox;
    [SerializeField] private Text speakerNameText;
    [SerializeField] private Text dialogueText;
    [SerializeField] private Text continueHintText;
    [SerializeField] private Sprite dialogueBoxSprite;
    [SerializeField] private Font dialogueFont;

    [Header("Playback")]
    [SerializeField, Min(0.005f)] private float secondsPerCharacter = 0.035f;
    [SerializeField, Min(0f)] private float finalPageDelay = 1f;
    [FormerlySerializedAs("emptySceneName")]
    [SerializeField] private string nextSceneName = "Happy_LivingRoom";
    [SerializeField] private Color missingCgColor = new Color(0.09f, 0.1f, 0.14f, 1f);

    private int currentPageIndex;
    private int visibleCharacterCount;
    private bool isTyping;
    private bool isLoadingEnding;
    private Coroutine typingCoroutine;

    public void Configure(Image image, Image box, Text nameText, Text bodyText, Text hintText)
    {
        cgImage = image;
        dialogueBox = box;
        speakerNameText = nameText;
        dialogueText = bodyText;
        continueHintText = hintText;
        EnsureNinePages();
    }

    private void Awake()
    {
        EnsureNinePages();
        if (cgImage == null || dialogueText == null)
            BuildFallbackUi();
    }

    private void Start()
    {
        ShowPage(0);
    }

    private void Update()
    {
        if (isLoadingEnding || !WasAdvancePressed())
            return;

        if (isTyping)
        {
            CompleteCurrentLine();
            return;
        }

        if (currentPageIndex < RequiredPageCount - 1)
            ShowPage(currentPageIndex + 1);
    }

    private bool WasAdvancePressed()
    {
        return Input.GetMouseButtonDown(0)
            || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private void ShowPage(int index)
    {
        currentPageIndex = Mathf.Clamp(index, 0, RequiredPageCount - 1);
        CgPage page = pages[currentPageIndex];
        page ??= new CgPage();
        pages[currentPageIndex] = page;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        if (cgImage != null)
        {
            cgImage.sprite = page.cgSprite;
            cgImage.color = page.cgSprite == null ? missingCgColor : Color.white;
            cgImage.preserveAspect = true;
        }

        if (dialogueBox != null)
            dialogueBox.gameObject.SetActive(true);
        if (speakerNameText != null)
            speakerNameText.text = page.speakerName ?? string.Empty;
        if (dialogueText != null)
            dialogueText.text = string.Empty;

        visibleCharacterCount = 0;
        isTyping = true;
        UpdateHint();
        typingCoroutine = StartCoroutine(TypePage(page.dialogue ?? string.Empty));
    }

    private IEnumerator TypePage(string line)
    {
        while (visibleCharacterCount < line.Length)
        {
            visibleCharacterCount++;
            if (dialogueText != null)
                dialogueText.text = line.Substring(0, visibleCharacterCount);
            yield return new WaitForSeconds(secondsPerCharacter);
        }

        isTyping = false;
        typingCoroutine = null;
        UpdateHint();

        if (currentPageIndex == RequiredPageCount - 1)
        {
            isLoadingEnding = true;
            yield return new WaitForSeconds(finalPageDelay);
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private void CompleteCurrentLine()
    {
        CgPage page = pages[currentPageIndex] ?? new CgPage();
        pages[currentPageIndex] = page;
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        visibleCharacterCount = (page.dialogue ?? string.Empty).Length;
        if (dialogueText != null)
            dialogueText.text = page.dialogue ?? string.Empty;
        isTyping = false;
        typingCoroutine = null;
        UpdateHint();

        if (currentPageIndex == RequiredPageCount - 1)
        {
            isLoadingEnding = true;
            StartCoroutine(LoadEmptySceneAfterDelay());
        }
    }

    private IEnumerator LoadEmptySceneAfterDelay()
    {
        yield return new WaitForSeconds(finalPageDelay);
        SceneManager.LoadScene(nextSceneName);
    }

    private void UpdateHint()
    {
        if (continueHintText == null)
            return;

        continueHintText.text = isTyping
            ? "Click / Space to complete"
            : currentPageIndex == RequiredPageCount - 1
                ? "The story will continue..."
                : "Click / Space to continue";
    }

    private void OnValidate()
    {
        EnsureNinePages();
        secondsPerCharacter = Mathf.Max(0.005f, secondsPerCharacter);
        finalPageDelay = Mathf.Max(0f, finalPageDelay);
    }

    private void EnsureNinePages()
    {
        if (pages == null)
            pages = new CgPage[RequiredPageCount];
        else if (pages.Length != RequiredPageCount)
            Array.Resize(ref pages, RequiredPageCount);

        for (int i = 0; i < RequiredPageCount; i++)
        {
            pages[i] ??= new CgPage
            {
                speakerName = string.Empty,
                dialogue = $"CG {i + 1} dialogue — replace this text in the Inspector."
            };
        }
    }

    private void BuildFallbackUi()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject canvasObject = new GameObject("Cutscene Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        CreateImage(canvasObject.transform, "Background", new Vector2(0.5f, 0.5f), new Vector2(1920f, 1080f), Color.black);
        cgImage = CreateImage(canvasObject.transform, "CG Image", new Vector2(0.5f, 0.57f), new Vector2(1760f, 800f), missingCgColor);
        cgImage.preserveAspect = true;
        dialogueBox = CreateImage(canvasObject.transform, "Dialogue Box", new Vector2(0.5f, 0.15f), new Vector2(1660f, 180f), new Color(0.025f, 0.025f, 0.04f, 0.95f));
        dialogueBox.sprite = dialogueBoxSprite;
        dialogueBox.type = dialogueBoxSprite == null ? Image.Type.Simple : Image.Type.Sliced;
        speakerNameText = CreateText(dialogueBox.transform, "Speaker Name", new Vector2(0.19f, 0.76f), new Vector2(560f, 42f), 24, TextAnchor.MiddleLeft, new Color(0.96f, 0.85f, 0.62f));
        dialogueText = CreateText(dialogueBox.transform, "Dialogue", new Vector2(0.5f, 0.42f), new Vector2(1520f, 100f), 25, TextAnchor.UpperLeft, Color.white);
        continueHintText = CreateText(canvasObject.transform, "Continue Hint", new Vector2(0.5f, 0.025f), new Vector2(600f, 28f), 16, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.78f));
    }

    private Image CreateImage(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        Text text = textObject.GetComponent<Text>();
        text.font = dialogueFont != null ? dialogueFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
