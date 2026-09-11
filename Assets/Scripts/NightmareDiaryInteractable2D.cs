using System;
using UnityEngine;

/// <summary>Runs the one-time Parent Bedroom diary and key sequence.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(DiaryReaderOverlay2D))]
public sealed class NightmareDiaryInteractable2D : PlayerInteractable2D
{
    [Header("Interaction")]
    [SerializeField] private BoxCollider2D interactionTrigger;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Vector2 interactionSize = new(1.5f, 1.5f);
    [SerializeField] private Vector3 promptOffset = new(0f, 1.1f, 0f);

    [Header("Testing")]
    [Tooltip("For TESTING only. Opens the reader directly without task progress or dialogue.")]
    [SerializeField] private bool previewReaderImmediately;

    [Header("Diary Dialogue")]
    [SerializeField] private DialogueLine[] openingLines =
    {
        new DialogueLine { speakerName = "佳思", dialogue = "大门钥匙。" },
        new DialogueLine { speakerName = "佳思", dialogue = "这是什么？" },
        new DialogueLine { speakerName = "佳思", dialogue = "我不记得爸爸还是妈妈有写日记的习惯呢。" },
        new DialogueLine { speakerName = "佳思", dialogue = "打开来看看吧。" }
    };
    [SerializeField] private DialogueLine[] followUpLines =
    {
        new DialogueLine { speakerName = "佳思", dialogue = "这都是什么跟什么啊..." }
    };

    [Header("Diary Reader")]
    [Tooltip("Shown on the left side of the reading page. This does not change the world object sprite.")]
    [SerializeField] private Sprite diaryPageSprite;
    [TextArea(8, 20)]
    [SerializeField] private string diaryPageText = "请在 Inspector 填写日记正文。";
    [SerializeField] private Sprite optionPanelSprite;
    [Tooltip("Clickable image used to close the reading page. This does not change the world object sprite.")]
    [SerializeField] private Sprite closeButtonSprite;
    [SerializeField] private Font readerFont;

    [Header("Key Inventory Icon")]
    [Tooltip("Shown in inventory slot 4 after the diary sequence. This does not change the world object sprite.")]
    [SerializeField] private Sprite keyInventoryIcon;

    private DialogueController2D dialogueController;
    private DiaryReaderOverlay2D readerOverlay;
    private PlayerDoorInteractor2D activePlayer;
    private int activeRunVersion;
    private bool sequenceInProgress;
    private bool hasCompleted;

    public override Vector3 InteractionPosition => interactionTrigger != null
        ? interactionTrigger.bounds.center
        : transform.position;

    public override int InteractionPriority => 10;

    public override bool CanInteract
    {
        get
        {
            if (!isActiveAndEnabled || sequenceInProgress || hasCompleted)
                return false;

            if (previewReaderImmediately)
                return true;

            NightmareTaskController task = NightmareTaskController.Instance;
            return task != null
                && task.CurrentStage == NightmareTaskStage.GoToParentsBedroomForKey
                && (dialogueController == null || !dialogueController.IsPlaying);
        }
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

        if (previewReaderImmediately)
        {
            activePlayer = player;
            sequenceInProgress = true;
            SetFocused(false);
            LockPlayerControl();
            OpenReader();
            return sequenceInProgress;
        }

        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController2D>();
        if (dialogueController == null)
        {
            Debug.LogWarning($"Diary '{name}' cannot find a DialogueController2D.", this);
            return false;
        }

        NightmareTaskController task = NightmareTaskController.Instance;
        if (task == null)
            return false;

        activePlayer = player;
        activeRunVersion = task.RunVersion;
        sequenceInProgress = true;
        SetFocused(false);

        if (dialogueController.PlayKeepingPlayerLocked(openingLines, player, OpenReader))
            return true;

        CancelSequence();
        return false;
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

        if (sequenceInProgress)
            CancelSequence();
    }

    private void OnValidate()
    {
        if (interactionTrigger == null)
            interactionTrigger = GetComponent<BoxCollider2D>();

        if (interactionTrigger != null)
        {
            interactionTrigger.isTrigger = true;
            interactionTrigger.size = interactionSize;
        }

        Transform existingPrompt = transform.Find("E Prompt");
        if (existingPrompt != null)
            existingPrompt.localPosition = promptOffset;

        if (openingLines == null || openingLines.Length == 0)
            openingLines = new[] { new DialogueLine() };
        if (followUpLines == null || followUpLines.Length == 0)
            followUpLines = new[] { new DialogueLine() };
    }

    private void EnsureInteraction()
    {
        if (interactionTrigger == null)
            interactionTrigger = GetComponent<BoxCollider2D>();
        if (interactionTrigger == null)
        {
            Debug.LogWarning($"Diary '{name}' is missing its BoxCollider2D.", this);
            return;
        }

        interactionTrigger.isTrigger = true;
        interactionTrigger.size = interactionSize;
        promptObject = WorldEPrompt2D.GetOrCreate(transform, promptOffset);
        readerOverlay = GetComponent<DiaryReaderOverlay2D>();
        SetFocused(false);
    }

    private void Register(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null)
            player.RegisterInteractable(this);
    }

    private void OpenReader()
    {
        if (!sequenceInProgress)
            return;

        if (readerOverlay == null)
            readerOverlay = GetComponent<DiaryReaderOverlay2D>();

        if (readerOverlay != null && readerOverlay.Show(
                diaryPageSprite,
                diaryPageText,
                optionPanelSprite,
                closeButtonSprite,
                readerFont,
                previewReaderImmediately ? CompletePreview : PlayFollowUpDialogue))
        {
            return;
        }

        Debug.LogWarning($"Diary '{name}' could not open its reader overlay.", this);
        if (previewReaderImmediately)
            CompletePreview();
        else
            PlayFollowUpDialogue();
    }

    private void PlayFollowUpDialogue()
    {
        if (!sequenceInProgress || dialogueController == null || activePlayer == null)
        {
            CancelSequence();
            return;
        }

        if (!dialogueController.Play(followUpLines, activePlayer, CompleteSequence))
            CancelSequence();
    }

    private void CompleteSequence()
    {
        NightmareTaskController task = NightmareTaskController.Instance;
        bool collectedKey = task != null
            && task.TryCollectFrontDoorKey(keyInventoryIcon, activeRunVersion);
        if (!collectedKey)
        {
            Debug.LogWarning($"Diary '{name}' could not add the front door key to the current run.", this);
            sequenceInProgress = false;
            activePlayer = null;
            return;
        }

        hasCompleted = true;
        sequenceInProgress = false;
        activePlayer = null;
        SetFocused(false);
    }

    private void CompletePreview()
    {
        sequenceInProgress = false;
        RestorePlayerControl();
        activePlayer = null;
    }

    private void CancelSequence()
    {
        if (readerOverlay != null)
            readerOverlay.HideWithoutCallback();

        sequenceInProgress = false;
        RestorePlayerControl();
        activePlayer = null;
    }

    private void RestorePlayerControl()
    {
        if (activePlayer == null)
            return;

        MCControllers movement = activePlayer.GetComponent<MCControllers>();
        if (movement != null)
            movement.SetMovementEnabled(true);
        activePlayer.SetInteractionEnabled(true);
    }

    private void LockPlayerControl()
    {
        if (activePlayer == null)
            return;

        MCControllers movement = activePlayer.GetComponent<MCControllers>();
        if (movement != null)
            movement.SetMovementEnabled(false);
        activePlayer.SetInteractionEnabled(false);
    }
}
