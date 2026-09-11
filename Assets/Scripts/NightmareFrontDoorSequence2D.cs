using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Stage-gated Front Door monologue, camera pan, and RUN choice flow.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class NightmareFrontDoorSequence2D : MonoBehaviour
{
    [Header("Sequence Trigger")]
    [SerializeField] private BoxCollider2D sequenceTrigger;
    [SerializeField] private Vector2 triggerSize = new(2.8f, 3.2f);

    [Header("Dialogue")]
    [SerializeField] private DialogueLine[] openingLines =
    {
        new DialogueLine
        {
            speakerName = "思佳",
            dialogue = "我真的要这么做吗？或许我还有..."
        }
    };

    [Header("Camera Focus")]
    [Tooltip("Move this scene object to choose where the camera looks during the pause.")]
    [SerializeField] private Transform cameraFocusPoint;
    [SerializeField] private CinemachineCamera sceneCamera;
    [SerializeField, Min(0f)] private float cameraPanDuration = 1f;
    [SerializeField, Min(0f)] private float cameraHoldDuration = 2f;
    [SerializeField, Min(0f)] private float cameraReturnDuration = 1f;

    [Header("Event Parents")]
    [SerializeField] private GameObject dadPrefab;
    [SerializeField] private GameObject momPrefab;
    [SerializeField] private Transform dadSpawnPoint;
    [SerializeField] private Transform momSpawnPoint;

    [Header("RUN Exit")]
    [SerializeField] private NightmareRunExitDoor2D runExitDoor;

    private PlayerDoorInteractor2D activePlayer;
    private Coroutine cameraRoutine;
    private Transform originalCameraFollow;
    private GameObject panTarget;
    private NightmareFrontDoorChoiceUI choiceUi;
    private bool sequenceInProgress;
    private bool runPathSelected;
    private bool choiceResolved;
    private bool sequenceFinished;
    private MCControllers activeMovement;
    private EnemyAI eventDad;
    private EnemyAI eventMom;
    private GameObject parentsRoot;
    private DialogueController2D activeDialogue;

    public void Configure(
        BoxCollider2D trigger,
        Transform focusPoint,
        CinemachineCamera camera,
        NightmareRunExitDoor2D exitDoor)
    {
        sequenceTrigger = trigger;
        cameraFocusPoint = focusPoint;
        sceneCamera = camera;
        runExitDoor = exitDoor;
        ValidateTrigger();
    }

    private void Awake()
    {
        if (sequenceTrigger == null)
            sequenceTrigger = GetComponent<BoxCollider2D>();
        ValidateTrigger();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryStartSequence(other);
    private void OnTriggerStay2D(Collider2D other) => TryStartSequence(other);

    private void OnDisable()
    {
        CancelSequence(true);
    }

    private void CancelSequence(bool restorePlayer)
    {
        sequenceFinished = true;
        if (cameraRoutine != null)
            StopCoroutine(cameraRoutine);
        cameraRoutine = null;

        if (choiceUi != null)
            choiceUi.Cancel();
        choiceUi = null;

        bool wasInProgress = sequenceInProgress;
        sequenceInProgress = false;
        if (activeDialogue != null)
            activeDialogue.CancelPlayback();
        activeDialogue = null;
        RestoreCameraFollow();
        DestroyEventParents();
        if (runExitDoor != null)
        {
            runExitDoor.TransitionStarted -= OnExitTransitionStarted;
            runExitDoor.DeactivateRunPath(activePlayer);
        }
        if (activeMovement != null)
            activeMovement.DeathStarted -= OnPlayerDeath;
        if (restorePlayer && wasInProgress && !runPathSelected)
            SetPlayerControl(activePlayer, true);
        activePlayer = null;
        activeMovement = null;
    }

    private void OnValidate()
    {
        if (sequenceTrigger == null)
            sequenceTrigger = GetComponent<BoxCollider2D>();
        ValidateTrigger();
        cameraPanDuration = Mathf.Max(0f, cameraPanDuration);
        cameraHoldDuration = Mathf.Max(0f, cameraHoldDuration);
        cameraReturnDuration = Mathf.Max(0f, cameraReturnDuration);

        if (openingLines == null || openingLines.Length == 0)
            openingLines = new[] { new DialogueLine() };
    }

    private void TryStartSequence(Collider2D other)
    {
        if (sequenceInProgress || runPathSelected || sequenceFinished || !IsFrontDoorTaskActive())
            return;

        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player == null)
            return;

        MCControllers movement = player.GetComponent<MCControllers>();
        if (movement == null || movement.IsDying || movement.IsSceneTransitioning)
            return;
        if (dadPrefab == null || momPrefab == null || dadSpawnPoint == null || momSpawnPoint == null
            || dadPrefab.GetComponent<EnemyAI>() == null || momPrefab.GetComponent<EnemyAI>() == null
            || runExitDoor == null)
        {
            Debug.LogError("Front Door needs both EnemyAI prefabs, both spawn points and its RUN exit.", this);
            sequenceFinished = true;
            return;
        }

        activePlayer = player;
        activeMovement = movement;
        activeMovement.DeathStarted += OnPlayerDeath;
        runExitDoor.TransitionStarted += OnExitTransitionStarted;
        sequenceInProgress = true;
        choiceResolved = false;
        SetPlayerControl(activePlayer, false);
        SpawnEventParents();

        DialogueController2D dialogue = FindDialogueController();
        activeDialogue = dialogue;
        if (dialogue != null
            && dialogue.PlayKeepingPlayerLocked(openingLines, activePlayer, StartCameraSequence))
        {
            return;
        }

        if (dialogue == null)
            Debug.LogWarning("Front Door sequence could not find DialogueController2D. Continuing safely.", this);
        StartCameraSequence();
    }

    private void StartCameraSequence()
    {
        if (!sequenceInProgress || activePlayer == null)
            return;
        activeDialogue = null;

        if (cameraRoutine != null)
            StopCoroutine(cameraRoutine);
        cameraRoutine = StartCoroutine(PlayCameraSequence());
    }

    private IEnumerator PlayCameraSequence()
    {
        ResolveSceneCamera();

        if (sceneCamera != null && cameraFocusPoint != null && activePlayer != null)
        {
            originalCameraFollow = sceneCamera.Follow != null
                ? sceneCamera.Follow
                : activePlayer.transform;

            panTarget = new GameObject("Front Door Camera Pan Target");
            panTarget.transform.position = activePlayer.transform.position;
            sceneCamera.Follow = panTarget.transform;

            yield return MovePanTarget(cameraFocusPoint.position, cameraPanDuration);
            if (cameraHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(cameraHoldDuration);
            if (activePlayer != null)
                yield return MovePanTarget(activePlayer.transform.position, cameraReturnDuration);
        }

        RestoreCameraFollow();
        cameraRoutine = null;
        ShowChoice();
    }

    private IEnumerator MovePanTarget(Vector3 destination, float duration)
    {
        if (panTarget == null)
            yield break;

        Vector3 start = panTarget.transform.position;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = safeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / safeDuration);
            panTarget.transform.position = Vector3.Lerp(start, destination, progress);
            yield return null;
        }

        if (panTarget != null)
            panTarget.transform.position = destination;
    }

    private void ShowChoice()
    {
        if (!sequenceInProgress || activePlayer == null)
            return;

        choiceUi = NightmareFrontDoorChoiceUI.Show(ChooseRun, OnChoiceTimeout);
        if (choiceUi != null)
            return;

        // Missing UI should never strand the player. Treat it like choosing RUN.
        ChooseRun();
    }

    private void ChooseRun()
    {
        choiceUi = null;
        if (!sequenceInProgress || activePlayer == null || choiceResolved)
            return;

        choiceResolved = true;
        runPathSelected = true;
        if (runExitDoor != null)
            runExitDoor.ActivateRunPath(activePlayer);
        else
            Debug.LogWarning("Front Door sequence is missing its RUN exit interaction.", this);

        SetPlayerControl(activePlayer, true);
        StartParentsChase();
    }

    private void OnChoiceTimeout()
    {
        choiceUi = null;
        if (!sequenceInProgress || activePlayer == null || choiceResolved)
            return;
        choiceResolved = true;
        SetPlayerControl(activePlayer, false);
        StartParentsChase();
    }

    private void SpawnEventParents()
    {
        parentsRoot = new GameObject("Front Door Event Parents");
        parentsRoot.SetActive(false);
        SceneManager.MoveGameObjectToScene(parentsRoot, gameObject.scene);
        eventDad = SpawnParent(dadPrefab, dadSpawnPoint, EnemyAI.EnemyType.Dad);
        eventMom = SpawnParent(momPrefab, momSpawnPoint, EnemyAI.EnemyType.Mom);
        parentsRoot.SetActive(true);
        eventDad.SetEventIdle();
        eventMom.SetEventIdle();

        // The two actors share a route; they must not block each other on the way to the player.
        foreach (Collider2D dadCollider in eventDad.GetComponentsInChildren<Collider2D>())
            foreach (Collider2D momCollider in eventMom.GetComponentsInChildren<Collider2D>())
                Physics2D.IgnoreCollision(dadCollider, momCollider);
    }

    private EnemyAI SpawnParent(GameObject prefab, Transform spawnPoint, EnemyAI.EnemyType type)
    {
        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parentsRoot.transform);
        instance.name = "Front Door " + type;
        instance.SetActive(true);
        EnemyAI enemy = instance.GetComponent<EnemyAI>();
        enemy.ConfigureEnemyType(type);
        enemy.SetEventIdle();
        SpriteRenderer visual = instance.GetComponent<SpriteRenderer>();
        if (visual != null)
            visual.sortingLayerName = "Enemy";
        return enemy;
    }

    private void StartParentsChase()
    {
        if (activeMovement == null || activeMovement.IsDying || activeMovement.IsSceneTransitioning)
            return;
        if (eventDad != null)
            eventDad.ChaseForEvent(activePlayer.transform);
        if (eventMom != null)
            eventMom.ChaseForEvent(activePlayer.transform);
        if (eventDad != null && eventMom != null)
            foreach (Collider2D dadCollider in eventDad.GetComponentsInChildren<Collider2D>())
                foreach (Collider2D momCollider in eventMom.GetComponentsInChildren<Collider2D>())
                    Physics2D.IgnoreCollision(dadCollider, momCollider);
    }

    private void OnPlayerDeath() => CancelSequence(false);

    private void OnExitTransitionStarted() => CancelSequence(false);

    private void DestroyEventParents()
    {
        if (parentsRoot != null)
        {
            parentsRoot.SetActive(false);
            Destroy(parentsRoot);
        }
        parentsRoot = null;
        eventDad = null;
        eventMom = null;
    }

    private bool IsFrontDoorTaskActive()
    {
        NightmareTaskController task = NightmareTaskController.Instance;
        return task != null && task.CurrentStage == NightmareTaskStage.GoToFrontDoor;
    }

    private DialogueController2D FindDialogueController()
    {
        DialogueController2D[] controllers = FindObjectsByType<DialogueController2D>(
            FindObjectsInactive.Include);
        foreach (DialogueController2D controller in controllers)
        {
            if (controller.gameObject.scene == gameObject.scene)
                return controller;
        }

        return null;
    }

    private void ResolveSceneCamera()
    {
        if (sceneCamera != null)
            return;

        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include);
        foreach (CinemachineCamera camera in cameras)
        {
            if (camera.gameObject.scene == gameObject.scene)
            {
                sceneCamera = camera;
                return;
            }
        }
    }

    private void RestoreCameraFollow()
    {
        if (sceneCamera != null)
        {
            if (activePlayer != null)
                sceneCamera.Follow = activePlayer.transform;
            else if (originalCameraFollow != null)
                sceneCamera.Follow = originalCameraFollow;
        }

        if (panTarget != null)
            Destroy(panTarget);
        panTarget = null;
        originalCameraFollow = null;
    }

    private void ValidateTrigger()
    {
        if (sequenceTrigger == null)
            return;

        sequenceTrigger.isTrigger = true;
        sequenceTrigger.size = triggerSize;
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
