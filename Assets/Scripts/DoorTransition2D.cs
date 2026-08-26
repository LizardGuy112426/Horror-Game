using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>An Inspector-configurable door that loads another scene when the player presses F.</summary>
public sealed class DoorTransition2D : PlayerInteractable2D
{
    [Header("Door References")]
    [SerializeField] private BoxCollider2D barrierCollider;
    [SerializeField] private Collider2D interactionTrigger;
    [SerializeField] private GameObject promptObject;

    [Header("Scene Transition")]
    [Tooltip("Enter the exact scene name, without .unity. The scene must be enabled in Build Settings.")]
    [SerializeField] private string targetSceneName = string.Empty;
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
            promptObject.SetActive(focused && Time.time >= nextInteractionTime);
    }

    public override bool Interact(PlayerDoorInteractor2D player)
    {
        if (player == null || Time.time < nextInteractionTime)
            return false;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"Door '{name}' has no Target Scene Name.", this);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning(
                $"Door '{name}' cannot load scene '{targetSceneName}'. "
                + "Check the spelling and add the scene to Build Settings.",
                this);
            return false;
        }

        nextInteractionTime = Time.time + interactionCooldown;
        player.ForgetDoor(this);
        SetFocused(false);
        SceneManager.LoadScene(targetSceneName);
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
