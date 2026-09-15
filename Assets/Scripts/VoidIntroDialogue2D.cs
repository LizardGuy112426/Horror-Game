using System.Collections;
using UnityEngine;

/// <summary>Plays the locked automatic monologue whenever the player enters Void.</summary>
[DisallowMultipleComponent]
public sealed class VoidIntroDialogue2D : MonoBehaviour
{
    [SerializeField] private GameObject dialogueCanvasPrefab;
    [SerializeField, Min(0f)] private float lineCompleteDelay = 2f;
    [SerializeField] private DialogueLine[] openingLines =
    {
        new DialogueLine
        {
            speakerName = "SiJia",
            dialogue = "Do I really want to leave?"
        }
    };

    private MCControllers playerMovement;
    private PlayerDoorInteractor2D playerInteraction;
    private DialogueController2D dialogue;
    private bool playbackStarted;
    private bool playbackFinished;

    private IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        while (MCControllers.Instance == null && Time.realtimeSinceStartup < deadline)
            yield return null;

        playerMovement = MCControllers.Instance;
        playerInteraction = playerMovement != null
            ? playerMovement.GetComponent<PlayerDoorInteractor2D>()
            : FindAnyObjectByType<PlayerDoorInteractor2D>();

        SetPlayerControl(false);
        NightmareTaskController.Instance?.HideHud();

        dialogue = FindSceneDialogue();
        if (dialogue == null && dialogueCanvasPrefab != null)
        {
            GameObject instance = Instantiate(dialogueCanvasPrefab);
            instance.name = "Void Intro DialogueCanvas";
            instance.transform.localScale = Vector3.one;
            dialogue = instance.GetComponent<DialogueController2D>();
        }

        if (dialogue == null)
        {
            Debug.LogWarning("Void intro requires a DialogueCanvas prefab.", this);
            FinishIntro();
            yield break;
        }

        playbackStarted = dialogue.PlayAutomatically(
            openingLines, playerInteraction, lineCompleteDelay, FinishIntro);
        if (!playbackStarted)
        {
            Debug.LogWarning("Void intro dialogue could not start.", this);
            FinishIntro();
        }
    }

    private DialogueController2D FindSceneDialogue()
    {
        foreach (DialogueController2D candidate in FindObjectsByType<DialogueController2D>(
            FindObjectsInactive.Include))
        {
            if (candidate.gameObject.scene == gameObject.scene)
                return candidate;
        }

        return null;
    }

    private void FinishIntro()
    {
        if (playbackFinished)
            return;

        playbackFinished = true;
        playbackStarted = false;
        SetPlayerControl(true);
        NightmareTaskController.Instance?.ShowHud();
    }

    private void SetPlayerControl(bool enabled)
    {
        if (playerMovement != null)
            playerMovement.SetMovementEnabled(enabled);
        if (playerInteraction != null)
            playerInteraction.SetInteractionEnabled(enabled);
    }

    private void OnDisable()
    {
        if (!playbackStarted || playbackFinished)
            return;

        dialogue?.CancelPlayback();
        FinishIntro();
    }

    private void OnValidate()
    {
        lineCompleteDelay = Mathf.Max(0f, lineCompleteDelay);
        if (openingLines == null || openingLines.Length != 1)
            System.Array.Resize(ref openingLines, 1);
        openingLines[0] ??= new DialogueLine();
    }
}
