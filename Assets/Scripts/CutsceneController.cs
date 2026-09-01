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
/// Plays four CG pages automatically. A full-screen black overlay fades away before the first
/// dialogue, then each completed page remains visible for a configurable delay.
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
    [SerializeField] private Image blackOverlay;
    [SerializeField] private Sprite dialogueBoxSprite;
    [SerializeField] private Font dialogueFont;

    [Header("Playback")]
    [SerializeField, Min(0.005f)] private float secondsPerCharacter = 0.035f;
    [FormerlySerializedAs("finalPageDelay")]
    [SerializeField, Min(0f)] private float pageCompleteDelay = 3f;
    [SerializeField, Min(0f)] private float blackFadeDuration = 2f;
    [FormerlySerializedAs("emptySceneName")]
    [SerializeField] private string nextSceneName = "Happy_LivingRoom";
    [SerializeField] private Color missingCgColor = new Color(0.09f, 0.1f, 0.14f, 1f);

    public void Configure(Image image, Image box, Text nameText, Text bodyText, Text hintText)
    {
        Configure(image, box, nameText, bodyText, hintText, null);
    }

    public void Configure(
        Image image,
        Image box,
        Text nameText,
        Text bodyText,
        Text hintText,
        Image openingBlackOverlay)
    {
        cgImage = image;
        dialogueBox = box;
        speakerNameText = nameText;
        dialogueText = bodyText;
        continueHintText = hintText;
        blackOverlay = openingBlackOverlay;
        EnsureFourPages();
    }

    private void Awake()
    {
        EnsureFourPages();
        if (cgImage == null || dialogueText == null)
            BuildFallbackUi();

        PrepareOpeningBlackOverlay();
        StoryFadeTransition2D transition = FindAnyObjectByType<StoryFadeTransition2D>();
        if (transition != null)
            transition.ReleaseOverlayAfterSceneHandoff();

        if (continueHintText != null)
            continueHintText.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(PlayCutscene());
    }

    private IEnumerator PlayCutscene()
    {
        PreparePageVisual(0, false);
        yield return PlayOpeningBlackFade();

        for (int pageIndex = 0; pageIndex < RequiredPageCount; pageIndex++)
        {
            PreparePageVisual(pageIndex, true);
            yield return TypePage(pages[pageIndex].dialogue ?? string.Empty);
            yield return new WaitForSecondsRealtime(pageCompleteDelay);
        }

        LoadNextScene();
    }

    private IEnumerator PlayOpeningBlackFade()
    {
        if (blackOverlay == null)
            yield break;

        Color overlayColor = blackOverlay.color;

        if (blackFadeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < blackFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayColor.a = 1f - Mathf.Clamp01(elapsed / blackFadeDuration);
                blackOverlay.color = overlayColor;
                yield return null;
            }
        }

        overlayColor.a = 0f;
        blackOverlay.color = overlayColor;
        blackOverlay.raycastTarget = false;
        blackOverlay.gameObject.SetActive(false);
    }

    private void PrepareOpeningBlackOverlay()
    {
        if (blackOverlay == null)
            return;

        blackOverlay.gameObject.SetActive(true);
        blackOverlay.raycastTarget = true;
        blackOverlay.transform.SetAsLastSibling();

        Color overlayColor = blackOverlay.color;
        overlayColor.a = 1f;
        blackOverlay.color = overlayColor;
    }

    private void PreparePageVisual(int index, bool showDialogue)
    {
        int clampedIndex = Mathf.Clamp(index, 0, RequiredPageCount - 1);
        CgPage page = pages[clampedIndex] ?? new CgPage();
        pages[clampedIndex] = page;

        if (cgImage != null)
        {
            cgImage.sprite = page.cgSprite;
            cgImage.color = page.cgSprite == null ? missingCgColor : Color.white;
            cgImage.preserveAspect = true;
        }

        if (dialogueBox != null)
            dialogueBox.gameObject.SetActive(showDialogue);
        if (speakerNameText != null)
            speakerNameText.text = showDialogue ? page.speakerName ?? string.Empty : string.Empty;
        if (dialogueText != null)
            dialogueText.text = string.Empty;
    }

    private IEnumerator TypePage(string line)
    {
        for (int visibleCharacterCount = 1; visibleCharacterCount <= line.Length; visibleCharacterCount++)
        {
            if (dialogueText != null)
                dialogueText.text = line.Substring(0, visibleCharacterCount);
            yield return new WaitForSecondsRealtime(secondsPerCharacter);
        }
    }

    private void LoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("Cutscene finished, but Next Scene Name is empty. Set it in the Cutscene Controller Inspector.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogWarning($"Cutscene cannot load '{nextSceneName}'. Add and enable it in Build Profiles, or correct Next Scene Name.", this);
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void OnValidate()
    {
        EnsureFourPages();
        secondsPerCharacter = Mathf.Max(0.005f, secondsPerCharacter);
        pageCompleteDelay = Mathf.Max(0f, pageCompleteDelay);
        blackFadeDuration = Mathf.Max(0f, blackFadeDuration);
    }

    private void EnsureFourPages()
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
        continueHintText.gameObject.SetActive(false);
        blackOverlay = CreateImage(canvasObject.transform, "Opening Black Overlay", new Vector2(0.5f, 0.5f), new Vector2(1920f, 1080f), Color.black);
        blackOverlay.raycastTarget = true;
        blackOverlay.transform.SetAsLastSibling();
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
