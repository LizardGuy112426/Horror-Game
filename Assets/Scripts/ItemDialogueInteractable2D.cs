using UnityEngine;

/// <summary>Inspector-configurable item that starts a repeatable dialogue.</summary>
public sealed class ItemDialogueInteractable2D : PlayerInteractable2D
{
    [Header("Interaction References")]
    [SerializeField] private Collider2D interactionTrigger;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private DialogueController2D dialogueController;

    [Header("Dialogue")]
    [SerializeField] private bool repeatable = true;
    [SerializeField] private DialogueLine[] dialogueLines =
    {
        new DialogueLine
        {
            speakerName = "???",
            dialogue = "Replace this item dialogue in the Inspector."
        }
    };

    [Header("Optional Nightmare Objective")]
    [Tooltip("Leave as None for ordinary dialogue furniture, including all Happy scenes.")]
    [SerializeField] private NightmareObjectiveItem nightmareObjective = NightmareObjectiveItem.None;
    [Tooltip("Shown in the next bottom inventory slot after this dialogue finishes.")]
    [SerializeField] private Sprite inventoryIcon;

    private bool hasCompleted;

    public override Vector3 InteractionPosition => interactionTrigger != null
        ? interactionTrigger.bounds.center
        : transform.position;

    public override bool CanInteract
    {
        get
        {
            if (!isActiveAndEnabled || (dialogueController != null && dialogueController.IsPlaying))
                return false;

            if (nightmareObjective != NightmareObjectiveItem.None)
            {
                NightmareTaskController task = NightmareTaskController.Instance;
                return task != null && task.CanCollectObjective(nightmareObjective);
            }

            return repeatable || !hasCompleted;
        }
    }

    public void Configure(
        Collider2D trigger,
        GameObject prompt,
        DialogueController2D controller)
    {
        interactionTrigger = trigger;
        promptObject = prompt;
        dialogueController = controller;
        if (interactionTrigger != null)
            interactionTrigger.isTrigger = true;
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

        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController2D>();
        if (dialogueController == null)
        {
            Debug.LogWarning($"Item '{name}' cannot find a DialogueController2D.", this);
            return false;
        }

        int runVersionAtStart = NightmareTaskController.Instance != null
            ? NightmareTaskController.Instance.RunVersion
            : -1;

        SetFocused(false);
        return dialogueController.Play(
            dialogueLines,
            player,
            () => HandleDialogueCompleted(runVersionAtStart));
    }

    /// <summary>Lets reusable Nightmare furniture prefabs choose their objective and icon.</summary>
    public void ConfigureNightmareObjective(
        NightmareObjectiveItem objective,
        Sprite icon)
    {
        nightmareObjective = objective;
        inventoryIcon = icon;
        SetFocused(false);
    }

    /// <summary>Lets reusable Nightmare furniture prefabs also replace their dialogue.</summary>
    public void ConfigureNightmareObjective(
        NightmareObjectiveItem objective,
        Sprite icon,
        DialogueLine[] lines)
    {
        ConfigureNightmareObjective(objective, icon);
        if (lines != null && lines.Length > 0)
            dialogueLines = lines;
    }

    private void HandleDialogueCompleted(int runVersionAtStart)
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
        if (interactionTrigger != null)
            interactionTrigger.isTrigger = true;
        if (dialogueLines == null || dialogueLines.Length == 0)
            dialogueLines = new[] { new DialogueLine() };
    }
}
