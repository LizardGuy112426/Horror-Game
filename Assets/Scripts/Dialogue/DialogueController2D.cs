using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-wide modal dialogue UI with CG-style typewriter input.</summary>
public sealed class DialogueController2D : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private Image nameBackground;
    [SerializeField] private Text nameText;
    [SerializeField] private Image contentBackground;
    [SerializeField] private Text contentText;
    [SerializeField] private Text continueHintText;

    [Header("Player References")]
    [SerializeField] private MCController playerMovement;
    [SerializeField] private PlayerDoorInteractor2D playerInteraction;

    [SerializeField, HideInInspector] private SimplePlayer2D legacySimplePlayerMovement;

    [Header("Playback")]
    [SerializeField, Min(0.005f)] private float secondsPerCharacter = 0.035f;

    private DialogueLine[] activeLines;
    private int currentLineIndex;
    private int visibleCharacterCount;
    private bool isTyping;
    private bool isPlaying;
    private Coroutine typingCoroutine;
    private Action completionCallback;

    public bool IsPlaying => isPlaying;

    public void Configure(
        GameObject root,
        Image speakerBackground,
        Text speakerText,
        Image dialogueBackground,
        Text dialogueText,
        Text hintText,
        MCController movement,
        PlayerDoorInteractor2D interaction)
    {
        ConfigureUiReferences(
            root,
            speakerBackground,
            speakerText,
            dialogueBackground,
            dialogueText,
            hintText,
            interaction);
        playerMovement = movement;
        legacySimplePlayerMovement = null;
    }

    public void Configure(
        GameObject root,
        Image speakerBackground,
        Text speakerText,
        Image dialogueBackground,
        Text dialogueText,
        Text hintText,
        SimplePlayer2D movement,
        PlayerDoorInteractor2D interaction)
    {
        ConfigureUiReferences(
            root,
            speakerBackground,
            speakerText,
            dialogueBackground,
            dialogueText,
            hintText,
            interaction);
        playerMovement = null;
        legacySimplePlayerMovement = movement;
    }

    private void ConfigureUiReferences(
        GameObject root,
        Image speakerBackground,
        Text speakerText,
        Image dialogueBackground,
        Text dialogueText,
        Text hintText,
        PlayerDoorInteractor2D interaction)
    {
        dialogueRoot = root;
        nameBackground = speakerBackground;
        nameText = speakerText;
        contentBackground = dialogueBackground;
        contentText = dialogueText;
        continueHintText = hintText;
        playerInteraction = interaction;

        if (!Application.isPlaying && dialogueRoot != null)
            dialogueRoot.SetActive(false);
    }

    public bool Play(
        DialogueLine[] lines,
        PlayerDoorInteractor2D player,
        Action onComplete = null)
    {
        if (isPlaying || lines == null || lines.Length == 0)
            return false;

        activeLines = lines;
        playerInteraction = player != null ? player : playerInteraction;
        if (playerInteraction != null)
        {
            playerMovement = playerInteraction.GetComponent<MCController>();
            legacySimplePlayerMovement = playerMovement == null
                ? playerInteraction.GetComponent<SimplePlayer2D>()
                : null;
        }
        completionCallback = onComplete;
        currentLineIndex = 0;
        isPlaying = true;

        SetPlayerControl(false);
        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);
        ShowLine(0);
        return true;
    }

    private void Awake()
    {
        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
    }

    private void Update()
    {
        if (!isPlaying || !WasAdvancePressed())
            return;

        if (isTyping)
        {
            CompleteCurrentLine();
            return;
        }

        if (currentLineIndex < activeLines.Length - 1)
            ShowLine(currentLineIndex + 1);
        else
            CloseDialogue();
    }

    private void ShowLine(int index)
    {
        currentLineIndex = index;
        DialogueLine line = activeLines[currentLineIndex] ?? new DialogueLine();
        activeLines[currentLineIndex] = line;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        if (nameBackground != null)
            nameBackground.gameObject.SetActive(true);
        if (contentBackground != null)
            contentBackground.gameObject.SetActive(true);
        if (nameText != null)
            nameText.text = line.speakerName ?? string.Empty;
        if (contentText != null)
            contentText.text = string.Empty;

        visibleCharacterCount = 0;
        isTyping = true;
        UpdateHint();
        typingCoroutine = StartCoroutine(TypeLine(line.dialogue ?? string.Empty));
    }

    private IEnumerator TypeLine(string line)
    {
        while (visibleCharacterCount < line.Length)
        {
            visibleCharacterCount++;
            if (contentText != null)
                contentText.text = line.Substring(0, visibleCharacterCount);
            yield return new WaitForSeconds(secondsPerCharacter);
        }

        isTyping = false;
        typingCoroutine = null;
        UpdateHint();
    }

    private void CompleteCurrentLine()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        DialogueLine line = activeLines[currentLineIndex] ?? new DialogueLine();
        activeLines[currentLineIndex] = line;
        if (contentText != null)
            contentText.text = line.dialogue ?? string.Empty;
        visibleCharacterCount = (line.dialogue ?? string.Empty).Length;
        isTyping = false;
        typingCoroutine = null;
        UpdateHint();
    }

    private void CloseDialogue()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        typingCoroutine = null;
        isTyping = false;
        isPlaying = false;
        activeLines = null;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
        SetPlayerControl(true);

        Action callback = completionCallback;
        completionCallback = null;
        callback?.Invoke();
    }

    private void SetPlayerControl(bool enabled)
    {
        if (playerMovement != null)
            playerMovement.SetMovementEnabled(enabled);
        else if (legacySimplePlayerMovement != null)
            legacySimplePlayerMovement.SetMovementEnabled(enabled);
        if (playerInteraction != null)
            playerInteraction.SetInteractionEnabled(enabled);
    }

    private void UpdateHint()
    {
        if (continueHintText != null)
            continueHintText.text = isTyping
                ? "Click / Space to complete"
                : currentLineIndex < activeLines.Length - 1
                    ? "Click / Space to continue"
                    : "Click / Space to close";
    }

    private static bool WasAdvancePressed()
    {
        return Input.GetMouseButtonDown(0)
            || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private void OnDisable()
    {
        if (isPlaying)
        {
            isPlaying = false;
            SetPlayerControl(true);
        }
    }

    private void OnValidate()
    {
        secondsPerCharacter = Mathf.Max(0.005f, secondsPerCharacter);
    }
}
