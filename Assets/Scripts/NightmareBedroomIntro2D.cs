using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs the locked, automatic bedroom dialogue only after Cutscene2 hands off to this scene.</summary>
public sealed class NightmareBedroomIntro2D : MonoBehaviour
{
    public const string BedroomSceneName = "NM_Bedroom1";

    private static string pendingCutsceneSceneName;

    public static bool HasPendingCutsceneArrival => string.Equals(
        pendingCutsceneSceneName,
        BedroomSceneName,
        StringComparison.Ordinal);

    [Header("Opening Fade")]
    [SerializeField, Min(0f)] private float sceneRevealDuration = 2f;

    [Header("Automatic Dialogue")]
    [SerializeField, Min(0f)] private float lineCompleteDelay = 2f;
    [SerializeField] private DialogueLine[] openingLines =
    {
        new DialogueLine
        {
            speakerName = "",
            dialogue = "Opening line 1 - replace this text in the Inspector."
        },
        new DialogueLine
        {
            speakerName = "",
            dialogue = "Opening line 2 - replace this text in the Inspector."
        }
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHandoff()
    {
        pendingCutsceneSceneName = string.Empty;
    }

    public static void PrepareCutsceneArrival(string sceneName)
    {
        pendingCutsceneSceneName = string.Equals(sceneName, BedroomSceneName, StringComparison.Ordinal)
            ? BedroomSceneName
            : string.Empty;
    }

    private IEnumerator Start()
    {
        if (!ConsumeCutsceneArrival())
            yield break;

        MCControllers movement = MCControllers.Instance;
        PlayerDoorInteractor2D interaction = movement != null
            ? movement.GetComponent<PlayerDoorInteractor2D>()
            : FindAnyObjectByType<PlayerDoorInteractor2D>();
        SetPlayerControl(movement, interaction, false);

        Image blackOverlay = CreateBlackOverlay();
        yield return RevealScene(blackOverlay);

        if (blackOverlay != null)
            Destroy(blackOverlay.canvas.gameObject);

        DialogueController2D dialogue = FindDialogueController();
        if (dialogue == null)
        {
            Debug.LogWarning("NM Bedroom intro could not find DialogueController2D. Player control was restored.", this);
            FinishIntro(movement, interaction);
            yield break;
        }

        if (!dialogue.PlayAutomatically(openingLines, interaction, lineCompleteDelay,
                () => FinishIntro(movement, interaction)))
        {
            Debug.LogWarning("NM Bedroom intro could not start automatic dialogue. Player control was restored.", this);
            FinishIntro(movement, interaction);
        }
    }

    private bool ConsumeCutsceneArrival()
    {
        bool shouldPlay = string.Equals(pendingCutsceneSceneName, BedroomSceneName, StringComparison.Ordinal)
            && string.Equals(gameObject.scene.name, BedroomSceneName, StringComparison.Ordinal);
        pendingCutsceneSceneName = string.Empty;
        return shouldPlay;
    }

    private IEnumerator RevealScene(Image blackOverlay)
    {
        if (blackOverlay == null)
            yield break;

        Color color = blackOverlay.color;
        if (sceneRevealDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < sceneRevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = 1f - Mathf.Clamp01(elapsed / sceneRevealDuration);
                blackOverlay.color = color;
                yield return null;
            }
        }

        color.a = 0f;
        blackOverlay.color = color;
    }

    private Image CreateBlackOverlay()
    {
        GameObject canvasObject = new GameObject(
            "NM Bedroom Intro Black Overlay",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        return image;
    }

    private DialogueController2D FindDialogueController()
    {
        DialogueController2D[] controllers = FindObjectsByType<DialogueController2D>(
            FindObjectsInactive.Include);
        foreach (DialogueController2D controller in controllers)
        {
            if (controller.gameObject.scene == gameObject.scene)
                return controller;
        }

        return null;
    }

    private static void SetPlayerControl(
        MCControllers movement,
        PlayerDoorInteractor2D interaction,
        bool enabled)
    {
        if (movement != null)
            movement.SetMovementEnabled(enabled);
        if (interaction != null)
            interaction.SetInteractionEnabled(enabled);
    }

    private static void FinishIntro(
        MCControllers movement,
        PlayerDoorInteractor2D interaction)
    {
        SetPlayerControl(movement, interaction, true);
        NightmareTaskController.Instance?.ShowHud();
    }

    private void OnValidate()
    {
        sceneRevealDuration = Mathf.Max(0f, sceneRevealDuration);
        lineCompleteDelay = Mathf.Max(0f, lineCompleteDelay);
        if (openingLines == null || openingLines.Length != 2)
            Array.Resize(ref openingLines, 2);

        for (int index = 0; index < openingLines.Length; index++)
            openingLines[index] ??= new DialogueLine();
    }
}
