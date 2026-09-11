using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps a blocking black overlay alive across a scene handoff so ending scenes
/// can reveal smoothly without a one-frame flash.
/// </summary>
public sealed class NightmareScreenFade2D : MonoBehaviour
{
    private const int SortingOrder = 5000;

    private static NightmareScreenFade2D instance;

    private Image overlay;
    private Coroutine activeRoutine;
    private string pendingScene;

    public static bool CancelPendingScene(string sceneName)
    {
        if (instance == null || instance.pendingScene != sceneName || instance.activeRoutine == null)
            return false;
        ReleaseOverlayAfterSceneHandoff();
        return true;
    }

    public static void ReleaseOverlayAfterSceneHandoff()
    {
        if (instance == null) return;
        instance.pendingScene = null;
        Destroy(instance.gameObject);
        instance.gameObject.SetActive(false);
        instance = null;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAllCoroutines();
        activeRoutine = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(pendingScene)) return;
        bool interrupted = scene.name != pendingScene;
        pendingScene = null;
        if (interrupted) ReleaseOverlayAfterSceneHandoff();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static bool FadeToScene(string targetSceneName, float duration)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName)
            || !Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            return false;
        }

        NightmareScreenFade2D fade = EnsureInstance();
        fade.Begin(fade.FadeAndLoadRoutine(targetSceneName, duration));
        return true;
    }

    public static void Reveal(float duration, Action onComplete = null)
    {
        NightmareScreenFade2D fade = EnsureInstance();
        fade.Begin(fade.RevealRoutine(duration, onComplete));
    }

    private static NightmareScreenFade2D EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject root = new GameObject(
            "Nightmare Screen Fade",
            typeof(RectTransform),
            typeof(Canvas));
        instance = root.AddComponent<NightmareScreenFade2D>();
        DontDestroyOnLoad(root);
        instance.CreateOverlay();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void CreateOverlay()
    {
        if (overlay != null)
            return;

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        if (gameObject.GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        Transform existing = transform.Find("Black Overlay");
        GameObject overlayObject = existing != null
            ? existing.gameObject
            : new GameObject("Black Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (overlayObject.transform.parent != transform)
            overlayObject.transform.SetParent(transform, false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlay = overlayObject.GetComponent<Image>();
        overlay.color = Color.black;
        overlay.raycastTarget = true;
        overlayObject.SetActive(false);
    }

    private void Begin(IEnumerator routine)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(routine);
    }

    private IEnumerator FadeAndLoadRoutine(string targetSceneName, float duration)
    {
        pendingScene = targetSceneName;
        PrepareOverlay(overlay != null && overlay.gameObject.activeSelf ? overlay.color.a : 0f);
        yield return FadeTo(1f, duration);
        activeRoutine = null;
        SceneManager.LoadScene(targetSceneName);
    }

    private IEnumerator RevealRoutine(float duration, Action onComplete)
    {
        PrepareOverlay(1f);
        yield return FadeTo(0f, duration);

        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.gameObject.SetActive(false);
        }

        activeRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (overlay == null)
            yield break;

        float startAlpha = overlay.color.a;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha,
                safeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / safeDuration)));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void PrepareOverlay(float alpha)
    {
        if (overlay == null)
            return;

        overlay.gameObject.SetActive(true);
        overlay.raycastTarget = true;
        SetAlpha(alpha);
    }

    private void SetAlpha(float alpha)
    {
        if (overlay == null)
            return;

        Color color = overlay.color;
        color.a = Mathf.Clamp01(alpha);
        overlay.color = color;
    }
}
