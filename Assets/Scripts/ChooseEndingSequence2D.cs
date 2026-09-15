using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Runs the locked five-second RUN/STAY decision in ChooseEnding.</summary>
[DisallowMultipleComponent]
public sealed class ChooseEndingSequence2D : MonoBehaviour
{
    private const float ChoiceDuration = 5f;
    private static readonly int SceneIdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int SceneWalkingState = Animator.StringToHash("Base Layer.Walking");

    private enum Branch
    {
        Pending,
        Run,
        Stay,
        Timeout
    }

    [Header("Scene References")]
    [SerializeField] private BoxCollider2D frontDoorTrigger;
    [SerializeField] private CinemachineCamera sceneCamera;

    [Header("Event Parents")]
    [SerializeField] private GameObject dadPrefab;
    [SerializeField] private GameObject momPrefab;
    [SerializeField] private Transform dadSpawnPoint;
    [SerializeField] private Transform momSpawnPoint;
    [SerializeField] private Transform dadDoorStop;
    [SerializeField] private Transform momDoorStop;
    [SerializeField, Min(0.1f)] private float runParentSpeed = 3.5f;

    [Header("Event Parent Rendering")]
    [SerializeField] private string parentSortingLayer = "Player";
    [SerializeField] private int dadSortingOrder = 21;
    [SerializeField] private int momSortingOrder = 20;

    [Header("RUN Ending")]
    [SerializeField, Min(0.1f)] private float playerAutoWalkSpeed = 3f;
    [SerializeField, Min(0.01f)] private float playerArriveDistance = 0.05f;
    [SerializeField] private string runSceneName = "NewsIntro";
    [SerializeField, Min(0f)] private float runFadeDuration = 2f;

    [Header("STAY Ending")]
    [SerializeField] private string staySceneName = "Cutscene3";
    [SerializeField, Min(0f)] private float stayFadeDuration = 2f;
    [SerializeField] private DialogueLine[] stayLines =
    {
        new DialogueLine { speakerName = "Parent", dialogue = "...My child. Are you sure this is what you want?..." },
        new DialogueLine { speakerName = "SiJia", dialogue = "..." },
        new DialogueLine { speakerName = "SiJia", dialogue = "...I know I was never trying to compete with my brother for anything..." },
        new DialogueLine { speakerName = "SiJia", dialogue = "I just..." },
        new DialogueLine { speakerName = "SiJia", dialogue = "wanted you to truly see me." },
        new DialogueLine { speakerName = "Dad", dialogue = "..." },
        new DialogueLine { speakerName = "Dad", dialogue = "We tried so many times to talk to you, but we never knew how to begin. As your father, I..." },
        new DialogueLine { speakerName = "Dad", dialogue = "I'm so sorry. We left you to carry all of that alone." },
        new DialogueLine { speakerName = "Mom", dialogue = "No... We never meant to make you feel this way. I failed to notice how much you were hurting." },
        new DialogueLine { speakerName = "Mom", dialogue = "Please don't keep suffering in silence... I'm so sorry..." },
        new DialogueLine { speakerName = "SiJia", dialogue = "..." }
    };

    private MCControllers playerMovement;
    private PlayerDoorInteractor2D playerInteraction;
    private Transform scenePlayerVisual;
    private Animator scenePlayerAnimator;
    private SpriteRenderer scenePlayerRenderer;
    private SpriteRenderer[] persistentPlayerRenderers;
    private bool[] persistentPlayerRendererStates;
    private EnemyAI dad;
    private EnemyAI mom;
    private GameObject parentsRoot;
    private Transform dadChoiceTarget;
    private Transform momChoiceTarget;
    private NightmareFrontDoorChoiceUI choiceUi;
    private Coroutine choiceRoutine;
    private Coroutine runRoutine;
    private Coroutine parentStopRoutine;
    private Branch branch;
    private bool transitionStarted;

    private IEnumerator Start()
    {
        yield return null;
        yield return WaitForPlayer();

        if (playerMovement == null || !ValidateConfiguration())
            yield break;

        PlaceAndLockPlayer();
        BindCamera();
        SpawnParents();
        choiceUi = NightmareFrontDoorChoiceUI.Show(ChooseRun, ChooseTimeout, ChooseStay);
        if (choiceUi == null)
        {
            ChooseRun();
            yield break;
        }

        StartChoiceChase();
    }

    private IEnumerator WaitForPlayer()
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        while (MCControllers.Instance == null && Time.realtimeSinceStartup < deadline)
            yield return null;

        playerMovement = MCControllers.Instance;
        if (playerMovement != null)
            playerInteraction = playerMovement.GetComponent<PlayerDoorInteractor2D>();
    }

    private bool ValidateConfiguration()
    {
        bool valid = frontDoorTrigger != null && dadPrefab != null && momPrefab != null
            && dadSpawnPoint != null && momSpawnPoint != null
            && dadDoorStop != null && momDoorStop != null;
        if (!valid)
            Debug.LogError("ChooseEnding is missing one or more sequence references.", this);
        return valid;
    }

    private void PlaceAndLockPlayer()
    {
        // Clear the one-shot handoff without moving the MC already staged here.
        ChooseEndingArrivalState.TryConsume(out _);
        scenePlayerVisual = FindScenePlayerVisual();
        HidePersistentPlayerVisuals();

        playerMovement.SetMovementEnabled(false);

        if (playerInteraction != null)
            playerInteraction.SetInteractionEnabled(false);
    }

    private Transform FindScenePlayerVisual()
    {
        foreach (GameObject candidate in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (candidate.scene != gameObject.scene
                || candidate.GetComponentInParent<MCControllers>() != null)
                continue;

            SpriteRenderer renderer = candidate.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                ConfigureScenePlayerAnimator(candidate, renderer);
                return renderer.transform;
            }
        }

        Debug.LogWarning("ChooseEnding could not find its staged MC visual; using the persistent player as fallback.", this);
        return playerMovement.transform;
    }

    private void ConfigureScenePlayerAnimator(GameObject scenePlayer, SpriteRenderer renderer)
    {
        Animator rootAnimator = scenePlayer.GetComponent<Animator>();
        scenePlayerRenderer = renderer;
        scenePlayerAnimator = renderer.GetComponent<Animator>();
        if (scenePlayerAnimator == null)
            return;

        // The child already has the full MC controller. The root only carries
        // an Idle-only controller, so disable it without replacing the child.
        if (rootAnimator != null && rootAnimator != scenePlayerAnimator)
            rootAnimator.enabled = false;

        scenePlayerAnimator.enabled = true;
        scenePlayerAnimator.Rebind();
        scenePlayerAnimator.Update(0f);
        SetScenePlayerWalking(false);
    }

    private void SetScenePlayerWalking(bool walking)
    {
        if (scenePlayerAnimator == null)
            return;

        scenePlayerAnimator.speed = 1f;
        SetAnimatorBoolIfPresent(scenePlayerAnimator, "onGround", true);
        SetAnimatorBoolIfPresent(scenePlayerAnimator, "isCrouching", false);
        SetAnimatorBoolIfPresent(scenePlayerAnimator, "isWalking", walking);

        int state = walking ? SceneWalkingState : SceneIdleState;
        if (scenePlayerAnimator.HasState(0, state))
        {
            scenePlayerAnimator.Play(state, 0, 0f);
            scenePlayerAnimator.Update(0f);
        }
    }

    private static void SetAnimatorBoolIfPresent(Animator target, string parameterName, bool value)
    {
        foreach (AnimatorControllerParameter parameter in target.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool
                && parameter.name == parameterName)
            {
                target.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void HidePersistentPlayerVisuals()
    {
        if (scenePlayerVisual == playerMovement.transform)
            return;

        persistentPlayerRenderers = playerMovement.GetComponentsInChildren<SpriteRenderer>(true);
        persistentPlayerRendererStates = new bool[persistentPlayerRenderers.Length];
        for (int i = 0; i < persistentPlayerRenderers.Length; i++)
        {
            persistentPlayerRendererStates[i] = persistentPlayerRenderers[i].enabled;
            persistentPlayerRenderers[i].enabled = false;
        }
    }

    private void RestorePersistentPlayerVisuals()
    {
        if (persistentPlayerRenderers == null || persistentPlayerRendererStates == null)
            return;

        for (int i = 0; i < persistentPlayerRenderers.Length; i++)
        {
            if (persistentPlayerRenderers[i] != null)
                persistentPlayerRenderers[i].enabled = persistentPlayerRendererStates[i];
        }

        persistentPlayerRenderers = null;
        persistentPlayerRendererStates = null;
    }


    private void BindCamera()
    {
        if (sceneCamera == null)
        {
            foreach (CinemachineCamera camera in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include))
            {
                if (camera.gameObject.scene == gameObject.scene)
                {
                    sceneCamera = camera;
                    break;
                }
            }
        }

        if (sceneCamera != null)
            sceneCamera.Follow = scenePlayerVisual;
    }

    private void SpawnParents()
    {
        parentsRoot = new GameObject("Choose Ending Parents");
        dad = SpawnParent(dadPrefab, dadSpawnPoint, EnemyAI.EnemyType.Dad);
        mom = SpawnParent(momPrefab, momSpawnPoint, EnemyAI.EnemyType.Mom);
        if (dad == null || mom == null)
            return;

        IgnoreCollisionsBetween(dad.gameObject, mom.gameObject, true);
        IgnoreCollisionsBetween(dad.gameObject, playerMovement.gameObject, true);
        IgnoreCollisionsBetween(mom.gameObject, playerMovement.gameObject, true);
    }

    private EnemyAI SpawnParent(GameObject prefab, Transform point, EnemyAI.EnemyType type)
    {
        GameObject instance = Instantiate(prefab, point.position, point.rotation, parentsRoot.transform);
        instance.name = "Choose Ending " + type;
        EnemyAI enemy = instance.GetComponent<EnemyAI>();
        if (enemy == null)
        {
            Debug.LogError($"{prefab.name} requires EnemyAI.", this);
            return null;
        }

        enemy.ConfigureEnemyType(type);
        enemy.SetEventIdle();
        ApplyParentRendering(instance, type);
        return enemy;
    }

    private void ApplyParentRendering(GameObject instance, EnemyAI.EnemyType type)
    {
        int sortingOrder = type == EnemyAI.EnemyType.Dad
            ? dadSortingOrder
            : momSortingOrder;

        foreach (SpriteRenderer renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingLayerName = parentSortingLayer;
            renderer.sortingOrder = sortingOrder;
        }
    }

    private void StartChoiceChase()
    {
        if (dad == null || mom == null)
            return;

        dadChoiceTarget = CreateRuntimeTarget("Dad Choice Target");
        momChoiceTarget = CreateRuntimeTarget("Mom Choice Target");
        float playerX = scenePlayerVisual.position.x;
        dadChoiceTarget.position = new Vector3(playerX + 1.2f, dad.transform.position.y, 0f);
        momChoiceTarget.position = new Vector3(playerX + 2.1f, mom.transform.position.y, 0f);

        dad.ChaseForEvent(dadChoiceTarget, RequiredSpeed(dad.transform, dadChoiceTarget));
        mom.ChaseForEvent(momChoiceTarget, RequiredSpeed(mom.transform, momChoiceTarget));
        choiceRoutine = StartCoroutine(ChoiceDeadlineRoutine());
    }

    private static float RequiredSpeed(Transform actor, Transform target)
    {
        return Mathf.Abs(actor.position.x - target.position.x) / ChoiceDuration;
    }

    private IEnumerator ChoiceDeadlineRoutine()
    {
        float elapsed = 0f;
        while (elapsed < ChoiceDuration && branch != Branch.Run && !transitionStarted)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        choiceRoutine = null;
        if (transitionStarted || branch == Branch.Run)
            yield break;

        StopParentsAtChoiceTarget();
        if (branch == Branch.Stay)
            BeginStayDialogue();
        else if (branch == Branch.Pending)
            ChooseTimeout();
    }

    private void ChooseRun()
    {
        if (branch != Branch.Pending || transitionStarted)
            return;

        branch = Branch.Run;
        choiceUi = null;
        if (choiceRoutine != null)
            StopCoroutine(choiceRoutine);
        choiceRoutine = null;
        SetScenePlayerWalking(true);
        RedirectParentsToDoor();
        runRoutine = StartCoroutine(AutoWalkToDoor());
    }

    private void ChooseStay()
    {
        if (branch != Branch.Pending || transitionStarted)
            return;

        branch = Branch.Stay;
        choiceUi = null;
    }

    private void ChooseTimeout()
    {
        if ((branch != Branch.Pending && branch != Branch.Timeout) || transitionStarted)
            return;

        branch = Branch.Timeout;
        if (choiceUi != null)
            choiceUi.Cancel();
        choiceUi = null;
        StopParentsAtChoiceTarget();
        playerMovement.transform.position = scenePlayerVisual.position;
        RestorePersistentPlayerVisuals();
        playerMovement.BeginNightmareDeath(EnemyAI.EnemyType.Dad);
    }

    private IEnumerator AutoWalkToDoor()
    {
        float targetX = frontDoorTrigger.bounds.center.x;
        while (scenePlayerVisual != null
            && Mathf.Abs(scenePlayerVisual.position.x - targetX) > playerArriveDistance)
        {
            float direction = Mathf.Sign(targetX - scenePlayerVisual.position.x);
            if (scenePlayerRenderer != null)
                scenePlayerRenderer.flipX = direction < 0f;
            scenePlayerVisual.position += Vector3.right
                * (direction * playerAutoWalkSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        runRoutine = null;
        if (scenePlayerVisual == null)
            yield break;

        scenePlayerVisual.position = new Vector3(
            targetX, scenePlayerVisual.position.y, scenePlayerVisual.position.z);
        SetScenePlayerWalking(false);

        if (!Application.CanStreamedLevelBeLoaded(runSceneName))
        {
            Debug.LogError($"ChooseEnding cannot load RUN scene '{runSceneName}'.", this);
            yield break;
        }

        transitionStarted = NightmareScreenFade2D.FadeToScene(runSceneName, runFadeDuration);
    }

    private void RedirectParentsToDoor()
    {
        if (dad != null)
            dad.ChaseForEvent(dadDoorStop, runParentSpeed);
        if (mom != null)
            mom.ChaseForEvent(momDoorStop, runParentSpeed);
        parentStopRoutine = StartCoroutine(StopParentsAtDoorRoutine());
    }

    private IEnumerator StopParentsAtDoorRoutine()
    {
        bool dadStopped = dad == null;
        bool momStopped = mom == null;
        while (!dadStopped || !momStopped)
        {
            if (!dadStopped && Mathf.Abs(dad.transform.position.x - dadDoorStop.position.x) <= 0.12f)
            {
                dad.SetEventIdle();
                dadStopped = true;
            }
            if (!momStopped && Mathf.Abs(mom.transform.position.x - momDoorStop.position.x) <= 0.12f)
            {
                mom.SetEventIdle();
                momStopped = true;
            }
            yield return null;
        }
        parentStopRoutine = null;
    }

    private void StopParentsAtChoiceTarget()
    {
        if (dad != null) dad.SetEventIdle();
        if (mom != null) mom.SetEventIdle();
    }

    private void BeginStayDialogue()
    {
        DialogueController2D dialogue = FindSceneDialogue();
        if (dialogue == null || stayLines == null || stayLines.Length == 0)
        {
            Debug.LogError("ChooseEnding STAY requires DialogueCanvas and dialogue lines.", this);
            return;
        }

        if (!dialogue.PlayKeepingPlayerLocked(stayLines, playerInteraction, FinishStayDialogue))
            Debug.LogError("ChooseEnding could not start the STAY dialogue.", this);
    }

    private void FinishStayDialogue()
    {
        if (transitionStarted || playerMovement == null)
            return;
        if (!Application.CanStreamedLevelBeLoaded(staySceneName))
        {
            Debug.LogError($"ChooseEnding cannot load STAY scene '{staySceneName}'.", this);
            return;
        }
        if (!playerMovement.BeginSceneTransition())
            return;

        transitionStarted = NightmareScreenFade2D.FadeToScene(staySceneName, stayFadeDuration);
        if (!transitionStarted)
            playerMovement.CancelSceneTransition();
    }

    private DialogueController2D FindSceneDialogue()
    {
        foreach (DialogueController2D dialogue in FindObjectsByType<DialogueController2D>(FindObjectsInactive.Include))
            if (dialogue.gameObject.scene == gameObject.scene)
                return dialogue;
        return null;
    }

    private Transform CreateRuntimeTarget(string targetName)
    {
        GameObject target = new GameObject(targetName);
        target.transform.SetParent(transform, false);
        return target.transform;
    }

    private static void IgnoreCollisionsBetween(GameObject first, GameObject second, bool ignored)
    {
        foreach (Collider2D firstCollider in first.GetComponentsInChildren<Collider2D>())
            foreach (Collider2D secondCollider in second.GetComponentsInChildren<Collider2D>())
                Physics2D.IgnoreCollision(firstCollider, secondCollider, ignored);
    }

    private void OnDisable()
    {
        if (choiceUi != null)
            choiceUi.Cancel();
        RestorePersistentPlayerVisuals();
        if (playerMovement != null && !transitionStarted && !playerMovement.IsDying)
        {
            playerMovement.StopScriptedMovement();
            playerMovement.SetMovementEnabled(true);
            if (playerInteraction != null)
                playerInteraction.SetInteractionEnabled(true);
        }
    }
}
