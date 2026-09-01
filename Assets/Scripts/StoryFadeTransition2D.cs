using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Locks the persistent player, fades to black, then loads the next story scene.</summary>
public sealed class StoryFadeTransition2D : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private Image blackOverlay;
    [SerializeField, Min(0f)] private float fadeDuration = 1.5f;
    [SerializeField] private string targetSceneName = "Cutscene2";

    private bool isTransitioning;

    public void Configure(Image overlay, float duration, string targetScene)
    {
        blackOverlay = overlay;
        fadeDuration = Mathf.Max(0f, duration);
        targetSceneName = targetScene;
        PrepareOverlay();
    }

    public void FadeToTargetScene()
    {
        if (!isTransitioning)
            StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        isTransitioning = true;
        SetPlayerControl(false);

        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.raycastTarget = true;
            Color color = blackOverlay.color;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
                blackOverlay.color = color;
                yield return null;
            }

            color.a = 1f;
            blackOverlay.color = color;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("Story Fade Transition has no Target Scene.", this);
            isTransitioning = false;
            SetPlayerControl(true);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning(
                $"Story Fade Transition cannot load '{targetSceneName}'. Add it to Build Profiles > Scene List.",
                this);
            isTransitioning = false;
            SetPlayerControl(true);
            yield break;
        }

        SceneManager.LoadScene(targetSceneName);

        // Cutscene2 owns its own opening black overlay. Remove this persistent
        // overlay after the synchronous load so it cannot cover the CG forever.
        if (blackOverlay != null)
            blackOverlay.gameObject.SetActive(false);
    }

    private void Awake()
    {
        PrepareOverlay();
    }

    private void PrepareOverlay()
    {
        if (blackOverlay == null)
            return;

        Color color = blackOverlay.color;
        color.a = 0f;
        blackOverlay.color = color;
        blackOverlay.raycastTarget = false;
        blackOverlay.gameObject.SetActive(false);
    }

    private static void SetPlayerControl(bool enabled)
    {
        MCControllers movement = MCControllers.Instance;
        if (movement != null)
            movement.SetMovementEnabled(enabled);

        PlayerDoorInteractor2D interaction = movement != null
            ? movement.GetComponent<PlayerDoorInteractor2D>()
            : Object.FindAnyObjectByType<PlayerDoorInteractor2D>();
        if (interaction != null)
            interaction.SetInteractionEnabled(enabled);
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
    }
}
