using UnityEngine;

/// <summary>A single Inspector-configurable door with solid collision and explicit exits.</summary>
public sealed class DoorTransition2D : PlayerInteractable2D
{
    [Header("Door References")]
    [SerializeField] private BoxCollider2D barrierCollider;
    [SerializeField] private Collider2D interactionTrigger;
    [SerializeField] private Transform leftExit;
    [SerializeField] private Transform rightExit;
    [SerializeField] private GameObject promptObject;

    [Header("Interaction")]
    [SerializeField, Min(0f)] private float interactionCooldown = 0.25f;

    private float nextInteractionTime;

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
        isActiveAndEnabled && Time.time >= nextInteractionTime;

    public void Configure(
        BoxCollider2D barrier,
        Collider2D trigger,
        Transform leftDestination,
        Transform rightDestination,
        GameObject prompt)
    {
        barrierCollider = barrier;
        interactionTrigger = trigger;
        leftExit = leftDestination;
        rightExit = rightDestination;
        promptObject = prompt;
        ValidateColliderRoles();
        SetFocused(false);
    }

    public override void SetFocused(bool focused)
    {
        if (promptObject != null)
            promptObject.SetActive(focused && Time.time >= nextInteractionTime);
    }

    public override bool Interact(PlayerDoorInteractor2D player)
    {
        if (player == null || Time.time < nextInteractionTime)
            return false;

        if (barrierCollider == null || leftExit == null || rightExit == null)
        {
            Debug.LogWarning($"Door '{name}' is missing its barrier or exit references.", this);
            return false;
        }

        bool enteredFromLeft = player.transform.position.x < barrierCollider.bounds.center.x;
        Transform destination = enteredFromLeft ? rightExit : leftExit;

        nextInteractionTime = Time.time + interactionCooldown;
        player.ForgetDoor(this);
        player.TeleportTo(destination.position);
        SetFocused(false);
        return true;
    }

    private void Awake()
    {
        ValidateColliderRoles();
        SetFocused(false);
    }

    private void OnValidate()
    {
        ValidateColliderRoles();
    }

    private void OnDisable()
    {
        if (promptObject != null)
            promptObject.SetActive(false);
    }

    private void ValidateColliderRoles()
    {
        if (barrierCollider != null)
            barrierCollider.isTrigger = false;
        if (interactionTrigger != null)
            interactionTrigger.isTrigger = true;
    }
}
