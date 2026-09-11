using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 新闻播报式开场：黑屏 -> 淡入显示标题+正文 -> 停留 -> 淡出黑屏 -> 跳转到指定场景
/// 挂载在场景中一个空物体上即可，所有引用和参数都在 Inspector 里配置
/// </summary>
public class NewsIntroController : MonoBehaviour
{
    [Header("=== UI 引用 ===")]
    [Tooltip("覆盖全屏的黑色 Image，用来做淡入淡出")]
    public CanvasGroup fadeGroup;

    [Tooltip("标题文字（粗体大字号）")]
    public TextMeshProUGUI titleText;

    [Tooltip("正文文字")]
    public TextMeshProUGUI bodyText;

    [Header("=== 内容 ===")]
    [TextArea(1, 3)]
    public string titleContent = "中学生女生失踪一个月　警方呼吁公众提供线索";

    [TextArea(5, 15)]
    public string bodyContent =
        "【本报讯】一名中学生女生于一个月前被家人发现失踪，家属随后向警方报案。至今女童仍下落不明，警方目前持续展开调查及搜寻行动，并呼吁公众提供相关线索。\n\n" +
        "据了解，失踪女童为一名中学生，女性，身高约150厘米，身形偏瘦，脸型偏圆，留有一头短发，发型为蘑菇头。女童失踪至今已有一个月，家属一直焦急等待她的消息，并希望她能够平安回家。\n\n" +
        "警方呼吁，任何曾见过该名女童，或掌握其行踪及相关信息的公众，应尽快向警方提供线索，以协助调查及搜寻工作。\n\n" +
        "家属也呼吁社会大众关注寻人消息，如发现符合上述特征的失踪女童，请立即联系警方。家人表示，他们至今仍在等待女童回家，希望她能够早日平安归来。";

    [Header("=== 时间控制（秒） ===")]
    public float fadeInDuration = 2f;
    public float holdDuration = 3f;
    public float fadeOutDuration = 2f;

    [Header("=== 自动滚动 ===")]
    [Min(1f), Tooltip("每秒向上移动的 Canvas 单位")]
    public float scrollSpeed = 40f;
    [Min(0f), Tooltip("正文末尾与画面底部之间的 Canvas 单位")]
    public float bottomPadding = 80f;

    [Header("=== 跳转设置 ===")]
    [Tooltip("结束后要加载的场景名称，必须已加入 Build Settings")]
    public string sceneToLoad = "MainMenu";

    private void Awake()
    {
        if (fadeGroup == null || titleText == null || bodyText == null)
        {
            Debug.LogError("NewsIntroController: assign Fade Group, Title Text and Body Text.", this);
            enabled = false;
            return;
        }

        fadeGroup.alpha = 1f;
        fadeGroup.transform.SetAsLastSibling();
        NightmareScreenFade2D.ReleaseOverlayAfterSceneHandoff();
        MCControllers player = MCControllers.Instance;
        if (player != null)
        {
            player.gameObject.SetActive(false);
            Destroy(player.gameObject);
        }
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        scrollSpeed = Mathf.Max(1f, scrollSpeed);
        bottomPadding = Mathf.Max(0f, bottomPadding);
    }

    void Start()
    {
        // 初始化文字内容
        if (titleText != null) titleText.text = titleContent;
        if (bodyText != null) bodyText.text = bodyContent;

        // 开场先全黑
        if (fadeGroup != null) fadeGroup.alpha = 1f;

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // 1. 从黑渐显
        yield return StartCoroutine(Fade(1f, 0f, fadeInDuration));

        yield return ScrollArticle();

        // 正文完整显示后再停留阅读。
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdDuration));

        // 3. 渐黑
        yield return StartCoroutine(Fade(0f, 1f, fadeOutDuration));

        // 4. 跳转场景
        if (!string.IsNullOrWhiteSpace(sceneToLoad) && Application.CanStreamedLevelBeLoaded(sceneToLoad.Trim()))
        {
            SceneManager.LoadScene(sceneToLoad.Trim());
        }
        else
        {
            Debug.LogWarning("NewsIntroController: Scene To Load 为空或未加入 Build Settings，无法跳转场景。");
        }
    }

    private IEnumerator ScrollArticle()
    {
        Canvas canvas = bodyText.GetComponentInParent<Canvas>();
        if (canvas == null) yield break;
        RectTransform viewport = canvas.rootCanvas.transform as RectTransform;
        if (viewport == null) yield break;

        while (true)
        {
            Canvas.ForceUpdateCanvases();
            bodyText.ForceMeshUpdate();
            if (bodyText.textInfo.characterCount == 0) yield break;

            // Measure rendered glyphs rather than the fixed text box, including overflow.
            Bounds bounds = bodyText.textBounds;
            float bottom = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, 0f);
                Vector3 world = bodyText.rectTransform.TransformPoint(corner);
                bottom = Mathf.Min(bottom, viewport.InverseTransformPoint(world).y);
            }

            float target = viewport.rect.yMin + Mathf.Clamp(bottomPadding, 0f, viewport.rect.height);
            float remaining = target - bottom;
            if (remaining <= 0.01f) yield break;

            float step = Mathf.Min(remaining, Mathf.Max(1f, scrollSpeed) * Time.unscaledDeltaTime);
            Vector3 movement = viewport.TransformVector(Vector3.up * step);
            titleText.rectTransform.position += movement;
            bodyText.rectTransform.position += movement;
            yield return null;
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeGroup == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fadeGroup.alpha = to;
    }
}
