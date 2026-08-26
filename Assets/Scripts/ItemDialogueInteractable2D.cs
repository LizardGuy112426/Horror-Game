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

    private bool hasCompleted;

    public override Vector3 InteractionPosition => interactionTrigger != null
        ? interactionTrigger.bounds.center
        : transform.position;

    public override bool CanInteract =>
        isActiveAndEnabled && (repeatable || !hasCompleted)
        && (dialogueController == null || !dialogueController.IsPlaying);

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

        SetFocused(false);
        return dialogueController.Play(dialogueLines, player, HandleDialogueCompleted);
    }

    private void HandleDialogueCompleted()
    {
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
