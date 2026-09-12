using UnityEngine;

/// <summary>Inspector-configurable item that plays a sound when interacted with,
/// then briefly disables itself for the sound's duration before becoming interactable again.</summary>
public sealed class SpidermanInteract : PlayerInteractable2D
{
    [Header("Interaction References")]
    [SerializeField] private Collider2D SpiderTrigger;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private bool repeatable = true;

    [Header("Interaction Cooldown")]
    [Tooltip("Leave at 0 to automatically match the SpidermanSFX clip length from SoundEffectManager.")]
    [SerializeField, Min(0f)] private float interactionCooldown = 0f;

    [Header("Optional Nightmare Objective")]
    [Tooltip("Leave as None for ordinary furniture, including all Happy scenes. Use only Clothes, Identity Card, or Wallet here; Front Door Key is granted by Dairy.")]
    [SerializeField] private NightmareObjectiveItem nightmareObjective = NightmareObjectiveItem.None;
    [Tooltip("Shown in the next bottom inventory slot after interacting. This never changes the world SpriteRenderer.")]
    [SerializeField] private Sprite inventoryIcon;

    private bool hasCompleted;
    private float nextInteractionTime;

    public override Vector3 InteractionPosition => SpiderTrigger != null
        ? SpiderTrigger.bounds.center
        : transform.position;

    public override bool CanInteract
    {
        get
        {
            if (!isActiveAndEnabled)
                return false;

            if (Time.time < nextInteractionTime)
                return false;

            if (nightmareObjective != NightmareObjectiveItem.None)
            {
                NightmareTaskController task = NightmareTaskController.Instance;
                return task != null && task.CanCollectObjective(nightmareObjective);
            }

            return repeatable || !hasCompleted;
        }
    }

    public void Configure(Collider2D trigger, GameObject prompt)
    {
        SpiderTrigger = trigger;
        promptObject = prompt;
        if (SpiderTrigger != null)
            SpiderTrigger.isTrigger = true;
        SetFocused(false);
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

        float cooldown = interactionCooldown;

        if (SoundEffectManager.instance != null)
        {
            SoundEffectManager.instance.SpiderSFX();

            if (cooldown <= 0f)
                cooldown = SoundEffectManager.instance.GetSpiderSFXDuration();
        }

        nextInteractionTime = Time.time + cooldown;

        int runVersionAtStart = NightmareTaskController.Instance != null
            ? NightmareTaskController.Instance.RunVersion
            : -1;

        HandleInteractionCompleted(runVersionAtStart);
        SetFocused(false);
        return true;
    }

    /// <summary>Lets reusable Nightmare furniture prefabs choose their objective and icon.</summary>
    public void ConfigureNightmareObjective(NightmareObjectiveItem objective, Sprite icon)
    {
        nightmareObjective = objective;
        inventoryIcon = icon;
        SetFocused(false);
    }

    private void HandleInteractionCompleted(int runVersionAtStart)
    {
        if (nightmareObjective != NightmareObjectiveItem.None)
        {
            NightmareTaskController task = NightmareTaskController.Instance;
            if (task != null)
                task.TryCollectObjective(nightmareObjective, inventoryIcon, runVersionAtStart);
            return;
        }

        if (!repeatable)
            hasCompleted = true;
    }

    private void Awake()
    {
        SetFocused(false);
    }

    private void OnDisable()
    {
        if (promptObject != null)
            promptObject.SetActive(false);
    }

    private void OnValidate()
    {
        if (SpiderTrigger != null)
            SpiderTrigger.isTrigger = true;
    }
}