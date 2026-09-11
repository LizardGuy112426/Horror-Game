using System;
using UnityEngine;

/// <summary>Front Door E interaction that becomes available only after the RUN choice.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class NightmareRunExitDoor2D : PlayerInteractable2D
{
    [Header("Interaction")]
    [SerializeField] private BoxCollider2D interactionTrigger;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Vector2 interactionSize = new(2.8f, 3.2f);
    [SerializeField] private Vector3 promptOffset = new(0f, 1.7f, 0f);

    [Header("Scene Transition")]
    [SerializeField] private string targetSceneName = "NewsIntro";
    [SerializeField, Min(0f)] private float fadeOutDuration = 2f;

    private bool runPathEnabled;
    private bool transitionInProgress;
    public event Action TransitionStarted;

    public override Vector3 InteractionPosition => interactionTrigger != null
        ? interactionTrigger.bounds.center
        : transform.position;

    public override int InteractionPriority => 100;

    public override bool CanInteract => isActiveAndEnabled
        && runPathEnabled
        && !transitionInProgress;

    public void Configure(BoxCollider2D trigger)
    {
        interactionTrigger = trigger;
        ValidateTrigger();
    }

    public void ActivateRunPath(PlayerDoorInteractor2D player)
    {
        runPathEnabled = true;
        transitionInProgress = false;
        SetFocused(false);

        if (player != null)
            player.RegisterInteractable(this);
    }

    public void DeactivateRunPath(PlayerDoorInteractor2D player)
    {
        runPathEnabled = false;
        SetFocused(false);
        if (player != null)
            player.UnregisterInteractable(this);
    }

    public override void SetFocused(bool focused)
    {
        if (promptObject != null)
            promptObject.SetActive(focused && CanInteract);
    }

    public override bool Interact(PlayerDoorInteractor2D player)
    {
        if (!CanInteract || player == null)
            return false;

        MCControllers movement = player.GetComponent<MCControllers>();
        if (movement != null && (movement.IsDying || movement.IsSceneTransitioning))
            return false;

        if (string.IsNullOrWhiteSpace(targetSceneName)
            || !Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning(
                $"Nightmare Front Door '{name}' cannot load '{targetSceneName}'. "
                + "Add the scene to Build Settings and check its spelling.", this);
            return false;
        }

        if (movement != null && !movement.BeginSceneTransition())
            return false;
        transitionInProgress = true;
        player.UnregisterInteractable(this);
        SetFocused(false);
        SetPlayerControl(player, false);

        if (!NightmareScreenFade2D.FadeToScene(targetSceneName, fadeOutDuration))
        {
            transitionInProgress = false;
            if (movement != null)
                movement.CancelSceneTransition();
            SetPlayerControl(player, true);
            return false;
        }

        TransitionStarted?.Invoke();
        return true;
    }

    private void Awake()
    {
        EnsureInteraction();
    }

    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null)
            player.UnregisterInteractable(this);
    }

    private void OnDisable()
    {
        if (promptObject != null)
            promptObject.SetActive(false);
    }

    private void OnValidate()
    {
        if (interactionTrigger == null)
            interactionTrigger = GetComponent<BoxCollider2D>();

        ValidateTrigger();
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

        Transform existingPrompt = transform.Find("E Prompt");
        if (existingPrompt != null)
            existingPrompt.localPosition = promptOffset;
    }

    private void EnsureInteraction()
    {
        if (interactionTrigger == null)
            interactionTrigger = GetComponent<BoxCollider2D>();

        ValidateTrigger();
        promptObject = WorldEPrompt2D.GetOrCreate(transform, promptOffset);
        SetFocused(false);
    }

    private void ValidateTrigger()
    {
        if (interactionTrigger == null)
            return;

        interactionTrigger.isTrigger = true;
        interactionTrigger.size = interactionSize;
    }

    private void Register(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null)
            player.RegisterInteractable(this);
    }

    private static void SetPlayerControl(PlayerDoorInteractor2D player, bool enabled)
    {
        MCControllers movement = player != null
            ? player.GetComponent<MCControllers>()
            : MCControllers.Instance;
        if (movement != null)
            movement.SetMovementEnabled(enabled);
        if (player != null)
            player.SetInteractionEnabled(enabled);
    }
}
