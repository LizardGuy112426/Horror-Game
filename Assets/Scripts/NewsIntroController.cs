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
    public string titleContent = "Middle School Girl Missing for a Month; Police Appeal to Public for Information";

    [TextArea(5, 15)]
    public string bodyContent =
        "【News Report】A female middle school student was reported missing by her family one month ago. Her family subsequently filed a police report. The girl remains missing, and police are continuing their investigation and search efforts while appealing to the public for any relevant information.\n\n" +
        "According to reports, the missing girl is a middle school student. She is approximately 150 cm tall, has a slim build, a relatively round face, and short hair in a bowl-cut style. She has now been missing for one month. Her family has been anxiously waiting for any news and hopes that she will return home safely.\n\n" +
        "Police are urging anyone who has seen the girl, or who has any information regarding her whereabouts, to contact the authorities as soon as possible to assist with the investigation and search.\n\n" +
        "The family is also asking the public to pay attention to the missing-person notice. Anyone who spots a girl matching the description above should contact the police immediately. Her family says they are still waiting for her to come home and hope she will return safely as soon as possible.";

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
