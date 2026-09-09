using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// An Inspector-configurable door that loads another scene when the player presses E.
/// 
/// Normal Door:
///     E → Door Animation → Scene Transition
///
/// VoidDoor:
///     E → Fade Image to 100% → Door Animation → Scene Transition
/// </summary>
public sealed class DoorTransition2D : PlayerInteractable2D
{
    [Header("Door References")]
    [SerializeField] private BoxCollider2D barrierCollider;
    [SerializeField] private Collider2D interactionTrigger;
    [SerializeField] private GameObject promptObject;

    [Header("Scene Transition")]
    [Tooltip("Enter the exact scene name, without .unity. The scene must be enabled in Build Settings.")]
    [SerializeField] private string targetSceneName = string.Empty;

    [Tooltip("Enter a Spawn ID from the target Scene. Leave empty to keep the Player's saved Scene position.")]
    [SerializeField] private string targetSpawnId = string.Empty;

    [SerializeField, Min(0f)]
    private float interactionCooldown = 0.25f;

    [Header("Door Animation")]
    [Tooltip("Automatically uses an Animator on this door when left empty.")]
    [SerializeField] private Animator doorAnimator;

    [SerializeField] private string interactionAnimationState = "Door";

    [SerializeField, Min(0f)]
    private float fallbackAnimationDuration = 0.25f;

    [Header("Void Door Fade")]
    [Tooltip("Only used if this GameObject has the VoidDoor tag.")]
    [SerializeField] private Image voidDoorFadeImage;

    [Tooltip("How long it takes for the Image to fade from transparent to fully visible.")]
    [SerializeField, Min(0.01f)]
    private float voidDoorFadeDuration = 2f;

    [Tooltip("Final alpha of the Image. 1 = 100% visible.")]
    [SerializeField, Range(0f, 1f)]
    private float voidDoorTargetAlpha = 1f;

    [Tooltip("If enabled, the Image starts at 0 alpha every time the VoidDoor is used.")]
    [SerializeField]
    private bool resetFadeBeforeStart = true;

    private float nextInteractionTime;
    private bool transitionInProgress;
    private Coroutine transitionCoroutine;

    public override Vector3 InteractionPosition
    {
        get
        {
            return interactionTrigger != null
                ? interactionTrigger.bounds.center
                : transform.position;
        }
    }

    public override bool CanInteract =>
        isActiveAndEnabled &&
        !transitionInProgress &&
        Time.time >= nextInteractionTime;

    public void Configure(
        BoxCollider2D barrier,
        Collider2D trigger,
        GameObject prompt)
    {
        barrierCollider = barrier;
        interactionTrigger = trigger;
        promptObject = prompt;

        ValidateColliderRoles();
        SetFocused(false);
    }

    public override void SetFocused(bool focused)
    {
        if (promptObject != null)
        {
            promptObject.SetActive(
                focused && Time.time >= nextInteractionTime
            );
        }
    }

    public override bool Interact(PlayerDoorInteractor2D player)
    {
        if (player == null || Time.time < nextInteractionTime)
            return false;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning(
                $"Door '{name}' has no Target Scene Name.",
                this
            );

            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning(
                $"Door '{name}' cannot load scene '{targetSceneName}'. "
                + "Check the spelling and add the scene to Build Settings.",
                this
            );

            return false;
        }

        nextInteractionTime = Time.time + interactionCooldown;
        transitionInProgress = true;

        player.ForgetDoor(this);
        SetFocused(false);

        // Start the complete transition.
        transitionCoroutine = StartCoroutine(PlayTransition());

        return true;
    }

    private void Awake()
    {
        ResolveAnimator();

        if (doorAnimator != null)
            doorAnimator.enabled = false;

        ValidateColliderRoles();

        // Prepare the fade image if this is a VoidDoor.
        if (CompareTag("VoidDoor") && voidDoorFadeImage != null)
        {
            SetImageAlpha(0f);
        }

        SetFocused(false);
    }

    private void OnValidate()
    {
        ResolveAnimator();
        ValidateColliderRoles();
    }

    private void OnDisable()
    {
        if (promptObject != null)
            promptObject.SetActive(false);

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }
    }

    private void ValidateColliderRoles()
    {
        if (barrierCollider != null)
            barrierCollider.isTrigger = false;

        if (interactionTrigger != null)
            interactionTrigger.isTrigger = true;
    }

    private void ResolveAnimator()
    {
        if (doorAnimator == null)
            doorAnimator = GetComponent<Animator>();
    }

    // =========================================================
    // COMPLETE TRANSITION
    // =========================================================

    private IEnumerator PlayTransition()
    {
        // -----------------------------------------------------
        // VOID DOOR ONLY
        // -----------------------------------------------------

        if (CompareTag("VoidDoor"))
        {
            yield return StartCoroutine(FadeVoidDoorImage());
        }

        // -----------------------------------------------------
        // ORIGINAL DOOR FUNCTION
        // -----------------------------------------------------

        yield return StartCoroutine(PlayAnimationAndLoadScene());
    }

    // =========================================================
    // VOID DOOR FADE
    // =========================================================

    private IEnumerator FadeVoidDoorImage()
    {
        // No Image assigned?
        // Don't stop the door from working.
        if (voidDoorFadeImage == null)
        {
            Debug.LogWarning(
                $"VoidDoor '{name}' has no Fade Image assigned. "
                + "Continuing with normal door transition.",
                this
            );

            yield break;
        }

        Color color = voidDoorFadeImage.color;

        float startAlpha;

        if (resetFadeBeforeStart)
        {
            startAlpha = 0f;

            color.a = 0f;
            voidDoorFadeImage.color = color;
        }
        else
        {
            startAlpha = color.a;
        }

        float elapsed = 0f;

        while (elapsed < voidDoorFadeDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / voidDoorFadeDuration);

            color.a = Mathf.Lerp(
                startAlpha,
                voidDoorTargetAlpha,
                progress
            );

            voidDoorFadeImage.color = color;

            yield return null;
        }

        // Make absolutely sure it reaches 100%.
        color.a = voidDoorTargetAlpha;
        voidDoorFadeImage.color = color;
    }

    private void SetImageAlpha(float alpha)
    {
        if (voidDoorFadeImage == null)
            return;

        Color color = voidDoorFadeImage.color;
        color.a = alpha;
        voidDoorFadeImage.color = color;
    }

    // =========================================================
    // ORIGINAL DOOR ANIMATION + SCENE LOAD
    // =========================================================

    private IEnumerator PlayAnimationAndLoadScene()
    {
        float animationDuration = 0f;

        if (doorAnimator != null)
        {
            doorAnimator.enabled = true;

            if (!string.IsNullOrWhiteSpace(interactionAnimationState))
            {
                doorAnimator.Play(
                    interactionAnimationState,
                    0,
                    0f
                );
            }

            doorAnimator.Update(0f);

            animationDuration =
                doorAnimator.GetCurrentAnimatorStateInfo(0).length;

            if (animationDuration <= 0f)
                animationDuration = fallbackAnimationDuration;
        }

        if (animationDuration > 0f)
            yield return new WaitForSeconds(animationDuration);

        Debug.Log(
            $"DOOR '{name}' → Scene: '{targetSceneName}' | " +
            $"Spawn ID: '{targetSpawnId}'"
        );

        SceneSpawnManager2D.PrepareArrival(
            targetSceneName,
            targetSpawnId
        );

        SceneManager.LoadScene(targetSceneName);
    }
}