using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows the editable modal diary-reader UI and closes it from its button.</summary>
[DisallowMultipleComponent]
public sealed class DiaryReaderOverlay2D : MonoBehaviour
{
    private const string OverlayPrefabResourceName = "DiaryReaderOverlayUI";
    private const string InspectorTextPlaceholder = "Enter the Diary Page Text in the Dairy Inspector.";
    private const string LegacyInspectorTextPlaceholder = "Enter the diary text in the Inspector.";

    private GameObject overlayRoot;
    private Image panelImage;
    private Image diaryImage;
    private Text diaryText;
    private RectTransform diaryTextRect;
    private RectTransform textViewport;
    private ScrollRect textScrollRect;
    private Image closeButtonImage;
    private Button closeButton;
    private Sprite defaultPanelSprite;
    private Sprite defaultDiaryPageSprite;
    private Sprite defaultCloseButtonSprite;
    private string defaultDiaryText;
    private Action closeCallback;
    private bool isVisible;

    public bool Show(
        Sprite pageSprite,
        string pageText,
        Sprite panelSprite,
        Sprite closeButtonSprite,
        Font font,
        Action onClosed)
    {
        if (!EnsureUi())
            return false;

        Font activeFont = font != null
            ? font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        panelImage.sprite = panelSprite != null ? panelSprite : defaultPanelSprite;
        panelImage.enabled = panelImage.sprite != null;

        if (diaryImage != null)
        {
            Sprite visiblePageSprite = pageSprite != null ? pageSprite : defaultDiaryPageSprite;
            diaryImage.sprite = visiblePageSprite;
            diaryImage.enabled = visiblePageSprite != null;
        }

        diaryText.font = activeFont;
        string visibleText = UsesPrefabDiaryText(pageText)
            ? defaultDiaryText
            : pageText;
        diaryText.text = string.IsNullOrWhiteSpace(visibleText)
            ? InspectorTextPlaceholder
            : visibleText;

        closeButtonImage.sprite = closeButtonSprite != null
            ? closeButtonSprite
            : defaultCloseButtonSprite;
        bool hasCloseButtonSprite = closeButtonImage.sprite != null;
        closeButtonImage.enabled = hasCloseButtonSprite;
        closeButtonImage.raycastTarget = hasCloseButtonSprite;
        closeButton.interactable = hasCloseButtonSprite;

        closeCallback = onClosed;
        isVisible = true;
        overlayRoot.SetActive(true);
        RefreshTextScroll();
        return true;
    }

    public void HideWithoutCallback()
    {
        closeCallback = null;
        isVisible = false;
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (overlayRoot != null)
            Destroy(overlayRoot);
    }

    private void Close()
    {
        if (!isVisible)
            return;

        Action callback = closeCallback;
        HideWithoutCallback();
        callback?.Invoke();
    }

    private bool EnsureUi()
    {
        if (overlayRoot != null)
            return true;

        GameObject overlayPrefab = Resources.Load<GameObject>(OverlayPrefabResourceName);
        if (overlayPrefab == null)
        {
            Debug.LogWarning(
                $"Diary reader cannot load Resources/{OverlayPrefabResourceName}.prefab.",
                this);
            return false;
        }

        overlayRoot = Instantiate(overlayPrefab);
        overlayRoot.name = "Diary Reader Overlay";

        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("DiaryReaderOverlayUI is missing its Canvas component.", this);
            Destroy(overlayRoot);
            overlayRoot = null;
            return false;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        Transform inputBlocker = overlayRoot.transform.Find("Input Blocker");
        // Older versions wrapped content in Diary Panel. The current editable layout
        // keeps the same objects directly under Input Blocker, so support both forms.
        Transform contentRoot = inputBlocker != null
            ? inputBlocker.Find("Diary Panel") ?? inputBlocker
            : null;

        panelImage = contentRoot != null ? contentRoot.GetComponent<Image>() : null;
        diaryImage = FindChildComponent<Image>(contentRoot, "Diary Page Image");
        diaryText = FindChildComponent<Text>(contentRoot, "Diary Text");
        closeButtonImage = FindChildComponent<Image>(contentRoot, "Close Button");
        Transform scrollView = FindChildTransform(contentRoot, "Text Scroll View");
        textViewport = FindChildComponent<RectTransform>(scrollView, "Viewport");
        diaryTextRect = diaryText != null ? diaryText.rectTransform : null;
        textScrollRect = scrollView != null ? scrollView.GetComponent<ScrollRect>() : null;
        if (textScrollRect == null && scrollView != null)
            textScrollRect = scrollView.gameObject.AddComponent<ScrollRect>();

        if (panelImage == null || diaryText == null || diaryTextRect == null
            || textViewport == null || textScrollRect == null || closeButtonImage == null)
        {
            Debug.LogWarning(
                "DiaryReaderOverlayUI is missing a required named UI child. " +
                "Keep Input Blocker, Diary Text, Text Scroll View/Viewport, and Close Button.",
                this);
            Destroy(overlayRoot);
            overlayRoot = null;
            return false;
        }

        closeButton = closeButtonImage.GetComponent<Button>();
        if (closeButton == null)
            closeButton = closeButtonImage.gameObject.AddComponent<Button>();

        closeButton.targetGraphic = closeButtonImage;
        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);

        RectMask2D viewportMask = textViewport.GetComponent<RectMask2D>();
        if (viewportMask == null)
            textViewport.gameObject.AddComponent<RectMask2D>();

        textScrollRect.content = diaryTextRect;
        textScrollRect.viewport = textViewport;
        textScrollRect.horizontal = false;
        textScrollRect.vertical = true;
        textScrollRect.movementType = ScrollRect.MovementType.Clamped;
        textScrollRect.inertia = true;
        textScrollRect.scrollSensitivity = 36f;

        defaultPanelSprite = panelImage.sprite;
        defaultDiaryPageSprite = diaryImage != null ? diaryImage.sprite : null;
        defaultCloseButtonSprite = closeButtonImage.sprite;
        defaultDiaryText = diaryText.text;
        overlayRoot.SetActive(false);
        return true;
    }

    private void RefreshTextScroll()
    {
        Canvas.ForceUpdateCanvases();

        float contentHeight = Mathf.Max(diaryText.preferredHeight, textViewport.rect.height);
        diaryTextRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        diaryTextRect.anchoredPosition = Vector2.zero;

        Canvas.ForceUpdateCanvases();
        textScrollRect.StopMovement();
        textScrollRect.verticalNormalizedPosition = 1f;
    }

    private static bool UsesPrefabDiaryText(string pageText)
    {
        return string.IsNullOrWhiteSpace(pageText)
            || string.Equals(pageText, InspectorTextPlaceholder, StringComparison.Ordinal)
            || string.Equals(pageText, LegacyInspectorTextPlaceholder, StringComparison.Ordinal);
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform nestedChild = FindChildTransform(child, childName);
            if (nestedChild != null)
                return nestedChild;
        }

        return null;
    }

    private static T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        Transform child = FindChildTransform(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }
}
