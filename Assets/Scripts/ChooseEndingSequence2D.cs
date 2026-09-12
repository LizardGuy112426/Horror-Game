using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Runs the locked five-second RUN/STAY decision in ChooseEnding.</summary>
[DisallowMultipleComponent]
public sealed class ChooseEndingSequence2D : MonoBehaviour
{
    private const float ChoiceDuration = 5f;

    private enum Branch
    {
        Pending,
        Run,
        Stay,
        Timeout
    }

    [Header("Scene References")]
    [SerializeField] private Transform fallbackPlayerStart;
    [SerializeField] private Transform runTarget;
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
        new DialogueLine { speakerName = "父母", dialogue = "...孩子。你真的要那样做吗？..." },
        new DialogueLine { speakerName = "思佳", dialogue = "..." },
        new DialogueLine { speakerName = "思佳", dialogue = "...我知道我从来都不是想要和弟弟挣什么..." },
        new DialogueLine { speakerName = "思佳", dialogue = "我只是..." },
        new DialogueLine { speakerName = "思佳", dialogue = "想要被你们认真对待。" },
        new DialogueLine { speakerName = "爸爸", dialogue = "..." },
        new DialogueLine { speakerName = "爸爸", dialogue = "我们，也很多次尝试过和你聊聊，只是，也不知道怎么开口，我身为父亲，真的。" },
        new DialogueLine { speakerName = "爸爸", dialogue = "很对不起你。让你独自承受了那么多。" },
        new DialogueLine { speakerName = "妈妈", dialogue = "妈妈，不，我们从来没有想这样对待你。是我疏忽了你。" },
        new DialogueLine { speakerName = "妈妈", dialogue = "不要再自己忍耐了...对不起..." },
        new DialogueLine { speakerName = "思佳", dialogue = "..." }
    };

    private MCControllers playerMovement;
    private PlayerDoorInteractor2D playerInteraction;
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
        bool valid = fallbackPlayerStart != null && runTarget != null
            && frontDoorTrigger != null && dadPrefab != null && momPrefab != null
            && dadSpawnPoint != null && momSpawnPoint != null
            && dadDoorStop != null && momDoorStop != null;
        if (!valid)
            Debug.LogError("ChooseEnding is missing one or more sequence references.", this);
        return valid;
    }

    private void PlaceAndLockPlayer()
    {
        Vector3 position = ChooseEndingArrivalState.TryConsume(out Vector3 arrival)
            ? arrival
            : fallbackPlayerStart.position;
        playerMovement.transform.position = position;
        playerMovement.SetMovementEnabled(false);
        playerMovement.PlayScriptedWalkingAnimation();
        if (playerInteraction != null)
            playerInteraction.SetInteractionEnabled(false);
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
            sceneCamera.Follow = playerMovement.transform;
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
        return enemy;
    }

    private void StartChoiceChase()
    {
        if (dad == null || mom == null)
            return;

        dadChoiceTarget = CreateRuntimeTarget("Dad Choice Target");
        momChoiceTarget = CreateRuntimeTarget("Mom Choice Target");
        float playerX = playerMovement.transform.position.x;
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
        playerMovement.BeginNightmareDeath(EnemyAI.EnemyType.Dad);
    }

    private IEnumerator AutoWalkToDoor()
    {
        float targetX = runTarget.position.x;
        while (playerMovement != null
            && Mathf.Abs(playerMovement.transform.position.x - targetX) > playerArriveDistance)
        {
            float direction = Mathf.Sign(targetX - playerMovement.transform.position.x);
            playerMovement.SetScriptedHorizontalVelocity(direction * playerAutoWalkSpeed);
            yield return null;
        }

        runRoutine = null;
        if (playerMovement == null)
            yield break;

        playerMovement.transform.position = new Vector3(
            targetX, runTarget.position.y, playerMovement.transform.position.z);
        playerMovement.StopScriptedMovement();

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
        if (playerMovement != null && !transitionStarted && !playerMovement.IsDying)
        {
            playerMovement.StopScriptedMovement();
            playerMovement.SetMovementEnabled(true);
            if (playerInteraction != null)
                playerInteraction.SetInteractionEnabled(true);
        }
    }
}
