using System;
using System.Collections;
using System.Collections.Generic;
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

    [Tooltip("Optional image sequence for this page. When assigned, it replaces the single CG Sprite.")]
    public Sprite[] cgSequence = Array.Empty<Sprite>();

    [Min(0f)]
    [Tooltip("How long each sequence image remains fully visible before the next crossfade.")]
    public float sequenceHoldDuration = 0.35f;

    [Min(0f)]
    [Tooltip("How long each crossfade to the next sequence image takes.")]
    public float sequenceCrossfadeDuration = 0.45f;

    [Tooltip("Switch to this page immediately instead of crossfading from the previous page.")]
    public bool skipCrossfadeFromPrevious;

    [Tooltip("Optional name shown in the dialogue name bar.")]
    public string speakerName;

    [TextArea(3, 8)]
    [Tooltip("Dialogue is revealed one character at a time.")]
    public string dialogue;
}

/// <summary>
/// Plays configured CG pages automatically. A full-screen black overlay fades away before the first
/// dialogue, then each completed page remains visible for a configurable delay.
/// </summary>
public sealed class CutsceneController : MonoBehaviour
{
    public const int RequiredPageCount = 4;

    [Header("CG Pages")]
    [SerializeField, Min(1)] private int pageCount = RequiredPageCount;
    [SerializeField] private CgPage[] pages = new CgPage[RequiredPageCount];
    [SerializeField] private bool clearGameplayPlayerOnStart;

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
    [Tooltip("Use the scene music's scheduled start as the CG clock. Other scenes retain legacy playback by default.")]
    [SerializeField] private bool synchronizeWithMusic;
    [SerializeField, Min(0.005f)] private float secondsPerCharacter = 0.035f;
    [FormerlySerializedAs("finalPageDelay")]
    [SerializeField, Min(0f)] private float pageCompleteDelay = 3f;
    [SerializeField, Min(0f)] private float pageCrossfadeDuration = 0.45f;
    [SerializeField, Min(0f)] private float blackFadeDuration = 2f;
    [SerializeField, Min(0f)] private float finalBlackFadeDuration = 2f;
    [FormerlySerializedAs("emptySceneName")]
    [SerializeField] private string nextSceneName = "Happy_LivingRoom";
    [SerializeField] private Color missingCgColor = new Color(0.09f, 0.1f, 0.14f, 1f);

    private Image sequenceOverlayImage;
    private PersistentMusicController2D synchronizedMusic;
    private PersistentMusicController2D.ScheduledPlayback synchronizedStart;
    private double silentClockStart;
    public bool SynchronizeWithMusic => synchronizeWithMusic;
    public bool SynchronizedPlaybackReady { get; private set; }
    public double SynchronizedCompletionTime { get; private set; } = double.NaN;
    public double SynchronizedElapsedSeconds => synchronizedStart != null
        ? synchronizedStart.Elapsed : Time.realtimeSinceStartupAsDouble - silentClockStart;

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
        EnsurePages();
    }

    private void Awake()
    {
        EnsurePages();
        if (cgImage == null || dialogueText == null)
            BuildFallbackUi();

        PrepareOpeningBlackOverlay();
        NightmareScreenFade2D.ReleaseOverlayAfterSceneHandoff();
        if (clearGameplayPlayerOnStart)
        {
            MCControllers player = MCControllers.Instance;
            if (player != null)
            {
                player.gameObject.SetActive(false);
                Destroy(player.gameObject);
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;
        }
        StoryFadeTransition2D transition = FindAnyObjectByType<StoryFadeTransition2D>();
        if (transition != null)
            transition.ReleaseOverlayAfterSceneHandoff();

        if (continueHintText != null)
            continueHintText.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(synchronizeWithMusic ? PlaySynchronizedCutscene() : PlayCutscene());
    }

    private void OnDisable()
    {
        if (!synchronizeWithMusic) return;
        StopAllCoroutines();
        if (synchronizedMusic != null) synchronizedMusic.CancelSynchronizedMusic(synchronizedStart);
        SynchronizedPlaybackReady = false;
    }

    private IEnumerator PlaySynchronizedCutscene()
    {
        PreparePageVisual(0, false);
        SceneMusicCue2D cue = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            var candidate = root.GetComponentInChildren<SceneMusicCue2D>();
            if (candidate != null && candidate.isActiveAndEnabled) { cue = candidate; break; }
        }
        synchronizedMusic = PersistentMusicController2D.Instance;
        if (synchronizedMusic != null)
        {
            synchronizedStart = synchronizedMusic.PrepareSynchronizedMusic(cue);
            while (!synchronizedStart.IsReady && !synchronizedStart.IsCancelled) yield return null;
            if (synchronizedStart.IsCancelled) yield break;
        }
        else
        {
            Debug.LogWarning("CG music system is missing. Continuing with a realtime clock.", this);
            silentClockStart = Time.realtimeSinceStartupAsDouble;
        }
        SynchronizedPlaybackReady = true;

        // All deadlines are offsets from ONE start, never from the frame that resumed a coroutine.
        double cursor = blackOverlay != null ? blackFadeDuration : 0d;
        yield return SynchronizedBlackFade(0d, cursor, true);
        for (int i = 0; i < pages.Length; i++)
        {
            CgPage page = pages[i];
            PreparePageDialogue(page, true);
            double transitionDuration = i > 0 && !page.skipCrossfadeFromPrevious ? pageCrossfadeDuration : 0d;
            Sprite next = GetPageInitialSprite(page);
            Sprite previous = i > 0 ? GetFinalSprite(pages[i - 1]) : next;
            double bodyStart = cursor + transitionDuration;
            while (SynchronizedElapsedSeconds < bodyStart)
            {
                RenderSynchronizedBlend(previous, next, transitionDuration > 0d
                    ? (float)((SynchronizedElapsedSeconds - cursor) / transitionDuration) : 1f);
                yield return null;
            }
            RenderSynchronizedBlend(next, next, 1f);
            string line = page.dialogue ?? string.Empty;
            var sequence = GetValidSequence(page);
            double sequenceDuration = Math.Max(0, sequence.Count - 1)
                * ((double)page.sequenceHoldDuration + page.sequenceCrossfadeDuration);
            double bodyDuration = Math.Max(line.Length * (double)secondsPerCharacter, sequenceDuration);
            double pageEnd = bodyStart + bodyDuration + pageCompleteDelay;
            int previousCount = -1;
            while (SynchronizedElapsedSeconds < pageEnd)
            {
                double elapsed = Math.Max(0d, SynchronizedElapsedSeconds - bodyStart);
                int count = (int)Math.Min(line.Length, 1d + Math.Floor(elapsed / secondsPerCharacter));
                if (count != previousCount && dialogueText != null)
                    dialogueText.text = line.Substring(0, count);
                previousCount = count;
                RenderSynchronizedSequence(page, sequence, elapsed);
                yield return null;
            }
            if (dialogueText != null) dialogueText.text = line;
            RenderSynchronizedSequence(page, sequence, bodyDuration);
            cursor = pageEnd;
        }
        yield return SynchronizedBlackFade(cursor, blackOverlay != null ? finalBlackFadeDuration : 0d, false);
        SynchronizedCompletionTime = SynchronizedElapsedSeconds;
        LoadNextScene();
    }

    private IEnumerator SynchronizedBlackFade(double start, double duration, bool opening)
    {
        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.raycastTarget = true;
            blackOverlay.transform.SetAsLastSibling();
        }
        while (SynchronizedElapsedSeconds < start + duration)
        {
            if (blackOverlay != null)
            {
                float fraction = duration > 0d ? Mathf.Clamp01((float)((SynchronizedElapsedSeconds - start) / duration)) : 1f;
                Color color = blackOverlay.color;
                color.a = opening ? 1f - fraction : fraction;
                blackOverlay.color = color;
            }
            yield return null;
        }
        if (blackOverlay != null)
        {
            Color color = blackOverlay.color;
            color.a = opening ? 0f : 1f;
            blackOverlay.color = color;
            blackOverlay.raycastTarget = !opening;
            blackOverlay.gameObject.SetActive(!opening);
        }
    }

    private static List<Sprite> GetValidSequence(CgPage page)
    {
        var result = new List<Sprite>();
        if (page.cgSequence != null)
            foreach (var sprite in page.cgSequence) if (sprite != null) result.Add(sprite);
        return result;
    }

    private static Sprite GetFinalSprite(CgPage page)
    {
        if (page.cgSequence != null)
            for (int i = page.cgSequence.Length - 1; i >= 0; i--)
                if (page.cgSequence[i] != null) return page.cgSequence[i];
        return page.cgSprite;
    }

    private void RenderSynchronizedSequence(CgPage page, List<Sprite> sequence, double elapsed)
    {
        if (sequence.Count < 2) return;
        double period = (double)page.sequenceHoldDuration + page.sequenceCrossfadeDuration;
        int index = period > 0d ? (int)Math.Min(sequence.Count - 1, Math.Floor(elapsed / period)) : sequence.Count - 1;
        if (index >= sequence.Count - 1)
        {
            RenderSynchronizedBlend(sequence[index], sequence[index], 1f);
            return;
        }
        double fadeElapsed = elapsed - index * period - page.sequenceHoldDuration;
        float fraction = page.sequenceCrossfadeDuration > 0f
            ? Mathf.Clamp01((float)(fadeElapsed / page.sequenceCrossfadeDuration)) : (fadeElapsed >= 0d ? 1f : 0f);
        RenderSynchronizedBlend(sequence[index], sequence[index + 1], fraction);
    }

    private void RenderSynchronizedBlend(Sprite from, Sprite to, float fraction)
    {
        if (cgImage == null) return;
        fraction = Mathf.Clamp01(fraction);
        cgImage.preserveAspect = true;
        if (fraction <= 0f || fraction >= 1f)
        {
            cgImage.sprite = fraction <= 0f ? from : to;
            cgImage.color = cgImage.sprite == null ? missingCgColor : Color.white;
            HideSequenceOverlay();
            return;
        }
        Image overlay = EnsureSequenceOverlay();
        cgImage.sprite = from;
        Color fromColor = from == null ? missingCgColor : Color.white;
        fromColor.a = 1f - fraction;
        cgImage.color = fromColor;
        overlay.gameObject.SetActive(true);
        overlay.sprite = to;
        Color toColor = to == null ? missingCgColor : Color.white;
        toColor.a = fraction;
        overlay.color = toColor;
    }

    private IEnumerator PlayCutscene()
    {
        PreparePageVisual(0, false);
        yield return PlayOpeningBlackFade();

        for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            CgPage page = pages[pageIndex];
            if (pageIndex == 0)
            {
                PreparePageVisual(pageIndex, true);
            }
            else
            {
                PreparePageDialogue(page, true);
                if (page.skipCrossfadeFromPrevious)
                {
                    SetPageImage(page);
                }
                else
                {
                    Sprite nextSprite = GetPageInitialSprite(page);
                    Color nextColor = nextSprite == null ? missingCgColor : Color.white;
                    yield return CrossfadeTo(nextSprite, pageCrossfadeDuration, nextColor);
                }
            }

            Coroutine imageSequence = HasImageSequence(page)
                ? StartCoroutine(PlayImageSequence(page))
                : null;
            yield return TypePage(page.dialogue ?? string.Empty);
            if (imageSequence != null)
                yield return imageSequence;

            yield return new WaitForSecondsRealtime(pageCompleteDelay);
        }

        yield return PlayFinalBlackFade();
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

    private IEnumerator PlayFinalBlackFade()
    {
        if (blackOverlay == null)
            yield break;

        blackOverlay.gameObject.SetActive(true);
        blackOverlay.raycastTarget = true;
        blackOverlay.transform.SetAsLastSibling();

        Color overlayColor = blackOverlay.color;
        overlayColor.a = 0f;
        blackOverlay.color = overlayColor;

        if (finalBlackFadeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < finalBlackFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayColor.a = Mathf.Clamp01(elapsed / finalBlackFadeDuration);
                blackOverlay.color = overlayColor;
                yield return null;
            }
        }

        overlayColor.a = 1f;
        blackOverlay.color = overlayColor;
    }

    private void PreparePageVisual(int index, bool showDialogue)
    {
        int clampedIndex = Mathf.Clamp(index, 0, pages.Length - 1);
        CgPage page = pages[clampedIndex] ?? new CgPage();
        pages[clampedIndex] = page;

        if (cgImage != null)
            SetPageImage(page);

        PreparePageDialogue(page, showDialogue);
    }

    private void SetPageImage(CgPage page)
    {
        if (cgImage == null)
            return;

        HideSequenceOverlay();
        Sprite initialSprite = GetPageInitialSprite(page);
        cgImage.sprite = initialSprite;
        cgImage.color = initialSprite == null ? missingCgColor : Color.white;
        cgImage.preserveAspect = true;
    }

    private void PreparePageDialogue(CgPage page, bool showDialogue)
    {
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

    private IEnumerator PlayImageSequence(CgPage page)
    {
        int currentIndex = FindNextSequenceSprite(page, 0);
        if (currentIndex < 0 || cgImage == null)
            yield break;

        cgImage.sprite = page.cgSequence[currentIndex];
        cgImage.color = Color.white;

        while (true)
        {
            int nextIndex = FindNextSequenceSprite(page, currentIndex + 1);
            if (nextIndex < 0)
                yield break;

            float holdDuration = Mathf.Max(0f, page.sequenceHoldDuration);
            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            yield return CrossfadeTo(
                page.cgSequence[nextIndex], page.sequenceCrossfadeDuration, Color.white);
            currentIndex = nextIndex;
        }
    }

    private IEnumerator CrossfadeTo(Sprite nextSprite, float duration, Color targetColor)
    {
        Image overlay = EnsureSequenceOverlay();
        if (overlay == null)
            yield break;

        overlay.gameObject.SetActive(true);
        overlay.sprite = nextSprite;
        overlay.preserveAspect = true;
        targetColor.a = 1f;
        Color transparentTarget = targetColor;
        transparentTarget.a = 0f;
        overlay.color = transparentTarget;

        Color sourceColor = cgImage.color;
        sourceColor.a = 1f;

        float safeDuration = Mathf.Max(0f, duration);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = safeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / safeDuration);
            Color fadingSource = sourceColor;
            fadingSource.a = 1f - progress;
            Color fadingTarget = targetColor;
            fadingTarget.a = progress;
            cgImage.color = fadingSource;
            overlay.color = fadingTarget;
            yield return null;
        }

        cgImage.sprite = nextSprite;
        cgImage.color = targetColor;
        HideSequenceOverlay();
    }

    private Image EnsureSequenceOverlay()
    {
        if (sequenceOverlayImage != null || cgImage == null)
            return sequenceOverlayImage;

        GameObject overlayObject = new GameObject(
            "CG Sequence Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform sourceRect = cgImage.rectTransform;
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(sourceRect.parent, false);
        overlayRect.anchorMin = sourceRect.anchorMin;
        overlayRect.anchorMax = sourceRect.anchorMax;
        overlayRect.anchoredPosition = sourceRect.anchoredPosition;
        overlayRect.sizeDelta = sourceRect.sizeDelta;
        overlayRect.pivot = sourceRect.pivot;
        overlayRect.localRotation = sourceRect.localRotation;
        overlayRect.localScale = sourceRect.localScale;
        overlayRect.SetSiblingIndex(sourceRect.GetSiblingIndex() + 1);

        sequenceOverlayImage = overlayObject.GetComponent<Image>();
        sequenceOverlayImage.material = cgImage.material;
        sequenceOverlayImage.type = cgImage.type;
        sequenceOverlayImage.preserveAspect = true;
        sequenceOverlayImage.raycastTarget = false;
        sequenceOverlayImage.maskable = cgImage.maskable;
        sequenceOverlayImage.gameObject.SetActive(false);
        return sequenceOverlayImage;
    }

    private void HideSequenceOverlay()
    {
        if (sequenceOverlayImage == null)
            return;
        sequenceOverlayImage.color = new Color(1f, 1f, 1f, 0f);
        sequenceOverlayImage.gameObject.SetActive(false);
    }

    private static bool HasImageSequence(CgPage page)
    {
        return FindNextSequenceSprite(page, 0) >= 0;
    }

    private static Sprite GetPageInitialSprite(CgPage page)
    {
        int index = FindNextSequenceSprite(page, 0);
        return index >= 0 ? page.cgSequence[index] : page?.cgSprite;
    }

    private static int FindNextSequenceSprite(CgPage page, int startIndex)
    {
        if (page?.cgSequence == null)
            return -1;

        for (int index = Mathf.Max(0, startIndex); index < page.cgSequence.Length; index++)
            if (page.cgSequence[index] != null)
                return index;
        return -1;
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

        NightmareBedroomIntro2D.PrepareCutsceneArrival(nextSceneName);
        if (nextSceneName == NightmareBedroomIntro2D.BedroomSceneName)
            SceneSpawnManager2D.PrepareArrival(nextSceneName, NightmareBedroomIntro2D.BedroomStartSpawnId);
        SceneManager.LoadScene(nextSceneName);
    }

    private void OnValidate()
    {
        EnsurePages();
        secondsPerCharacter = Mathf.Max(0.005f, secondsPerCharacter);
        pageCompleteDelay = Mathf.Max(0f, pageCompleteDelay);
        pageCrossfadeDuration = Mathf.Max(0f, pageCrossfadeDuration);
        blackFadeDuration = Mathf.Max(0f, blackFadeDuration);
        finalBlackFadeDuration = Mathf.Max(0f, finalBlackFadeDuration);
    }

    private void EnsurePages()
    {
        pageCount = Mathf.Max(1, pageCount);
        if (pages == null)
            pages = new CgPage[pageCount];
        else if (pages.Length != pageCount)
            Array.Resize(ref pages, pageCount);

        for (int i = 0; i < pageCount; i++)
        {
            pages[i] ??= new CgPage
            {
                speakerName = string.Empty,
                dialogue = $"CG {i + 1} dialogue — replace this text in the Inspector."
            };
            pages[i].sequenceHoldDuration = Mathf.Max(0f, pages[i].sequenceHoldDuration);
            pages[i].sequenceCrossfadeDuration = Mathf.Max(0f, pages[i].sequenceCrossfadeDuration);
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
