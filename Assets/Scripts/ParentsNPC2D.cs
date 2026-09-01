using UnityEngine;

/// <summary>Reusable animated parents NPC that exists only during its required story stage.</summary>
public sealed class ParentsNPC2D : PlayerInteractable2D
{
    [Header("Task Requirement")]
    [SerializeField] private StoryTaskStage requiredTaskStage = StoryTaskStage.TalkToParentsInKitchen;

    [Header("Replaceable Visual")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Interaction References")]
    [SerializeField] private Collider2D interactionTrigger;
    [SerializeField] private GameObject ePrompt;
    [SerializeField] private DialogueController2D dialogueController;

    [Header("Dialogue Lines")]
    [SerializeField] private DialogueLine[] dialogueLines =
    {
        new DialogueLine
        {
            speakerName = "父母",
            dialogue = "请在 Inspector 里替换这段对话。"
        }
    };

    private StoryTaskController subscribedController;
    private bool stageActive;

    public StoryTaskStage RequiredTaskStage => requiredTaskStage;
    public override Vector3 InteractionPosition => interactionTrigger != null
        ? interactionTrigger.bounds.center
        : transform.position;
    public override bool CanInteract =>
        isActiveAndEnabled && stageActive
        && (dialogueController == null || !dialogueController.IsPlaying);

    public void Configure(
        StoryTaskStage requiredStage,
        GameObject content,
        GameObject visual,
        SpriteRenderer renderer,
        Animator visualAnimator,
        Collider2D trigger,
        GameObject prompt)
    {
        requiredTaskStage = requiredStage;
        contentRoot = content;
        visualRoot = visual;
        spriteRenderer = renderer;
        animator = visualAnimator;
        interactionTrigger = trigger;
        ePrompt = prompt;
        if (interactionTrigger != null)
            interactionTrigger.isTrigger = true;
        RefreshForCurrentStage();
    }

    public void SetStoryConfiguration(
        StoryTaskStage requiredStage,
        DialogueLine[] lines)
    {
        requiredTaskStage = requiredStage;
        if (lines != null && lines.Length > 0)
            dialogueLines = lines;
        RefreshForCurrentStage();
    }

    public override void SetFocused(bool focused)
    {
        if (ePrompt != null)
            ePrompt.SetActive(focused && CanInteract);
    }

    public override bool Interact(PlayerDoorInteractor2D player)
    {
        if (!CanInteract || player == null)
            return false;

        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController2D>();
        if (dialogueController == null)
        {
            Debug.LogWarning($"Parents NPC '{name}' cannot find a DialogueController2D.", this);
            return false;
        }

        SetFocused(false);
        return dialogueController.Play(dialogueLines, player, HandleDialogueCompleted);
    }

    private void Start()
    {
        BindTaskController();
        RefreshForCurrentStage();
    }

    private void OnEnable()
    {
        BindTaskController();
        RefreshForCurrentStage();
    }

    private void Update()
    {
        // A persistent StoryTaskController and a scene-local NPC can be enabled in
        // either order after a scene change. Keep the NPC synchronized even when
        // the sceneLoaded event happened before this component subscribed.
        if (subscribedController != StoryTaskController.Instance)
            BindTaskController();

        StoryTaskController controller = StoryTaskController.Instance;
        bool shouldBeActive = controller != null
            && controller.CurrentStage == requiredTaskStage;
        bool contentMatches = contentRoot == null
            || contentRoot.activeSelf == shouldBeActive;
        bool triggerMatches = interactionTrigger == null
            || interactionTrigger.enabled == shouldBeActive;

        if (stageActive != shouldBeActive || !contentMatches || !triggerMatches)
            ApplyStageState(shouldBeActive);
    }

    private void OnDisable()
    {
        UnbindTaskController();
        SetFocused(false);
    }

    private void BindTaskController()
    {
        StoryTaskController controller = StoryTaskController.Instance;
        if (controller == null || controller == subscribedController)
            return;

        UnbindTaskController();
        subscribedController = controller;
        subscribedController.StageChanged += HandleStageChanged;
    }

    private void UnbindTaskController()
    {
        if (subscribedController != null)
            subscribedController.StageChanged -= HandleStageChanged;
        subscribedController = null;
    }

    private void HandleStageChanged(StoryTaskStage stage)
    {
        RefreshForCurrentStage();
    }

    private void RefreshForCurrentStage()
    {
        StoryTaskController controller = StoryTaskController.Instance;
        ApplyStageState(controller != null && controller.CurrentStage == requiredTaskStage);
    }

    private void ApplyStageState(bool active)
    {
        stageActive = active;
        if (contentRoot != null)
            contentRoot.SetActive(stageActive);
        if (interactionTrigger != null)
            interactionTrigger.enabled = stageActive;
        if (!stageActive)
            SetFocused(false);
    }

    private void HandleDialogueCompleted()
    {
        StoryTaskController controller = StoryTaskController.Instance;
        if (controller != null)
            controller.AdvanceAfterDialogue(requiredTaskStage);
    }

    private void OnValidate()
    {
        if (interactionTrigger != null)
            interactionTrigger.isTrigger = true;
        if (dialogueLines == null || dialogueLines.Length == 0)
            dialogueLines = new[] { new DialogueLine() };
    }
}
