using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MCControllers : MonoBehaviour
{
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int WalkingState = Animator.StringToHash("Base Layer.Walking");
    public static MCControllers Instance { get; private set; }
    public bool IsDying => kill;
    public bool IsSceneTransitioning { get; private set; }
    public event Action DeathStarted;

    private const float MissingJumpscareDuration = 3f;
    private Coroutine deathRoutine;
    private bool simulationBeforeLock;
    private Vector3 initialScale;

    float xInput;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float Speed;
    [SerializeField] private int Jumpforce;
    [SerializeField] private Transform GroundChecker;
    [SerializeField] private Vector2 GroundChekerSize;
    [SerializeField] private LayerMask Ground;
    bool onGround;
    bool wasOnGround;
    [Tooltip("Minimum time between jump triggers, regardless of how many Update() frames pass before physics actually lifts the player off the ground checker.")]
    [SerializeField] private float jumpDebounce = 0.1f;
    private float nextJumpAllowedTime = 0f;
    [SerializeField] private float WalkingSFXInterval;
    float WalkingSFXTime;
    [SerializeField] private Animator animator;
    private bool isFacingRight = true;
    private bool movementEnabled = true;
    private bool scriptedHorizontalMovement;
    private bool scriptedWalkingAnimationOnly;
    private float scriptedHorizontalVelocity;
    bool kill;
    [SerializeField] private SpriteRenderer DadJumpscare;
    [SerializeField] private Animator DadJumpscareAnimator;
    [SerializeField] private SpriteRenderer MomJumpscare;
    [SerializeField] private Animator MomJumpscareAnimator;
    [Header("Void Death")]
    [SerializeField] private float voidKillY = -11f;
    [SerializeField] private float voidRespawnDelay = 0.5f;
    [Header("Cutscene Scenes")]
    [SerializeField] private string[] cutsceneSceneNames;
    private Vector3 fallbackRespawnPosition;

    public void SetMovementEnabled(bool value)
    {
        scriptedHorizontalMovement = false;
        scriptedWalkingAnimationOnly = false;
        scriptedHorizontalVelocity = 0f;
        movementEnabled = value && !IsDying && !IsSceneTransitioning;

        if (animator != null)
            animator.speed = 1f;

        if (!movementEnabled)
        {
            xInput = 0f;

            if (rb != null)
                rb.linearVelocityX = 0f;

            if (animator != null)
                animator.SetBool("isWalking", false);
        }
    }

    /// <summary>Moves the player for a story sequence while normal input stays locked.</summary>
    public bool SetScriptedHorizontalVelocity(float velocity)
    {
        if (IsDying || IsSceneTransitioning || rb == null)
            return false;

        bool startWalkingAnimation = !scriptedHorizontalMovement
            || Mathf.Abs(scriptedHorizontalVelocity) <= 0.01f;
        movementEnabled = false;
        scriptedHorizontalMovement = true;
        scriptedWalkingAnimationOnly = false;
        scriptedHorizontalVelocity = velocity;
        rb.linearVelocityX = velocity;

        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool("onGround", true);
            animator.SetBool("isCrouching", false);
            animator.SetBool("isWalking", Mathf.Abs(velocity) > 0.01f);
            if (startWalkingAnimation && Mathf.Abs(velocity) > 0.01f
                && animator.HasState(0, WalkingState))
            {
                animator.Play(WalkingState, 0, 0f);
                animator.Update(0f);
            }
        }

        if (velocity > 0f && !isFacingRight)
            Flip();
        else if (velocity < 0f && isFacingRight)
            Flip();

        return true;
    }

    /// <summary>Shows frame zero of Walking while a story sequence keeps the player stationary.</summary>
    public void PlayScriptedWalkingAnimation()
    {
        if (IsDying || IsSceneTransitioning || animator == null)
            return;

        movementEnabled = false;
        scriptedHorizontalMovement = false;
        scriptedWalkingAnimationOnly = true;
        scriptedHorizontalVelocity = 0f;
        if (rb != null)
            rb.linearVelocityX = 0f;

        animator.SetBool("onGround", true);
        animator.SetBool("isCrouching", false);
        animator.SetBool("isWalking", true);
        if (animator.HasState(0, WalkingState))
        {
            animator.Play(WalkingState, 0, 0f);
            animator.Update(0f);
            animator.speed = 0f;
        }
    }

    public void StopScriptedMovement()
    {
        scriptedHorizontalMovement = false;
        scriptedWalkingAnimationOnly = false;
        scriptedHorizontalVelocity = 0f;
        if (rb != null)
            rb.linearVelocityX = 0f;
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool("onGround", true);
            animator.SetBool("isCrouching", false);
            animator.SetBool("isWalking", false);
            if (animator.HasState(0, IdleState))
            {
                animator.Play(IdleState, 0, 0f);
                animator.Update(0f);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (GroundChecker == null)
            return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(GroundChecker.position, GroundChekerSize);
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        rb = GetComponent<Rigidbody2D>();
        initialScale = transform.localScale;
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        fallbackRespawnPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
            // Fell into the void
            if (!kill &&
                !IsSceneTransitioning &&
                transform.position.y <= voidKillY)
            {
                BeginVoidDeath();
                return;
            }

            if (scriptedWalkingAnimationOnly)
        {

            rb.linearVelocityX = 0f;
            if (animator != null)
            {
                animator.SetBool("onGround", true);
                animator.SetBool("isCrouching", false);
                animator.SetBool("isWalking", true);
            }
            return;
        }

        if (scriptedHorizontalMovement)
        {
            rb.linearVelocityX = scriptedHorizontalVelocity;
            if (animator != null)
            {
                animator.SetBool("onGround", true);
                animator.SetBool("isCrouching", false);
                animator.SetBool("isWalking", Mathf.Abs(scriptedHorizontalVelocity) > 0.01f);
            }
            return;
        }

        if (!movementEnabled)
        {
            xInput = 0f;
            rb.linearVelocityX = 0f;

            if (animator != null)
                animator.SetBool("isWalking", false);

            return;
        }
        xInput = Input.GetAxis("Horizontal");
        rb.linearVelocityX = xInput * Speed;
        if (xInput != 0)
        {
            animator.SetBool("isWalking", true);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }
        bool isCrouching = Input.GetKey(KeyCode.LeftControl);
        if (isCrouching)
        {
            animator.SetBool("isCrouching", true);
        }
        else
        {
            animator.SetBool("isCrouching", false);
        }
        onGround = Physics2D.OverlapBox(GroundChecker.position, GroundChekerSize, 0, Ground);

        if (Input.GetKey(KeyCode.Space) && onGround && Time.time >= nextJumpAllowedTime)
        {
            rb.linearVelocityY = Jumpforce;
            nextJumpAllowedTime = Time.time + jumpDebounce;

            if (GameState.Instance != null)
            {
                GameState.Instance.Jump = true;
            }
            if (SoundEffectManager.instance != null)
                SoundEffectManager.instance.JumpSFX();
        }

        if (onGround && !wasOnGround)
        {
            if (SoundEffectManager.instance != null)
                SoundEffectManager.instance.LandSFX();
        }
        wasOnGround = onGround;

        if (isCrouching && onGround)
        {
            rb.linearVelocityX = (xInput * Speed) / 2;
        }
        if (onGround)
        {
            animator.SetBool("onGround", true);
        }
        else
        {
            animator.SetBool("onGround", false);
        }
        if (xInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (xInput < 0 && isFacingRight)
        {
            Flip();
        }

        if (onGround && rb.linearVelocityX != 0)
        {
            WalkingSFXTime = WalkingSFXTime - Time.deltaTime;
            if (WalkingSFXTime <= 0)
            {
                SoundEffectManager.instance?.WalkSFX();
                WalkingSFXTime = WalkingSFXInterval;
            }
        }
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (deathRoutine != null)
            StopCoroutine(deathRoutine);
        deathRoutine = null;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool cameFromDeath = kill;
        bool cameFromTransition = IsSceneTransitioning;

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);

        deathRoutine = null;
        kill = false;

        ResetJumpscare(DadJumpscare, DadJumpscareAnimator);
        ResetJumpscare(MomJumpscare, MomJumpscareAnimator);

        // ==========================================
        // CUTSCENE SCENE
        // Keep the real player completely disabled.
        // ==========================================
        if (IsCutsceneScene(scene.name))
        {
            IsSceneTransitioning = true;

            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;

            SetMovementEnabled(false);

            GetComponent<PlayerDoorInteractor2D>()
                ?.SetInteractionEnabled(false);

            // Disable player collision as extra protection.
            foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true))
                col.enabled = false;

            return;
        }

        // ==========================================
        // NORMAL GAMEPLAY SCENE
        // ==========================================
        bool shouldRestorePlayer =
            cameFromDeath ||
            cameFromTransition ||
            IsSceneTransitioning;

        IsSceneTransitioning = false;

        if (shouldRestorePlayer)
        {
            if (SoundEffectManager.instance != null)
                SoundEffectManager.instance.StopJumpscare();

            rb.linearVelocity = Vector2.zero;
            rb.simulated = simulationBeforeLock;

            transform.localScale = initialScale;
            isFacingRight = true;

            // Re-enable player colliders.
            foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true))
                col.enabled = true;

            if (animator != null)
                animator.SetBool("isCrouching", false);

            SetMovementEnabled(true);

            GetComponent<PlayerDoorInteractor2D>()
                ?.SetInteractionEnabled(true);

            if (cameFromDeath)
                SoundEffectManager.instance?.PlayRespawnSFX();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (kill || IsSceneTransitioning || TableHideSpot2D.IsPlayerHidden)
            return;

        EnemyAI enemy = collision.gameObject.GetComponentInParent<EnemyAI>();
        if (enemy != null && enemy.TryHandleStoryContact(this))
            return;

        if (collision.gameObject.CompareTag("Dad"))
            BeginDeath(EnemyAI.EnemyType.Dad, false);
        else if (collision.gameObject.CompareTag("Mom"))
            BeginDeath(EnemyAI.EnemyType.Mom, false);
    }

    /// <summary>Forces the normal Nightmare death/respawn flow from an ending scene.</summary>
    public void BeginNightmareDeath(EnemyAI.EnemyType type)
    {
        BeginDeath(type, true);
    }

    private void BeginDeath(EnemyAI.EnemyType type, bool forceNightmareReturn)
    {
        if (kill || IsSceneTransitioning)
            return;

        kill = true;
        LockPlayerBody();
        bool returnToBedroom = forceNightmareReturn
            || NightmareTaskController.IsNightmareGameplayScene(SceneManager.GetActiveScene().name);
        if (returnToBedroom && NightmareTaskController.Instance != null)
            NightmareTaskController.Instance.ResetCurrentRun();

        DeathStarted?.Invoke();
        foreach (DialogueController2D dialogue in FindObjectsByType<DialogueController2D>())
            dialogue.CancelPlayback();
        foreach (DiaryReaderOverlay2D reader in FindObjectsByType<DiaryReaderOverlay2D>())
            reader.HideWithoutCallback();
        deathRoutine = StartCoroutine(PlayDeathAndReturn(type, returnToBedroom));
    }

    public bool BeginSceneTransition()
    {
        if (kill || IsSceneTransitioning)
            return false;
        IsSceneTransitioning = true;
        LockPlayerBody();
        return true;
    }

    public void CancelSceneTransition()
    {
        if (!IsSceneTransitioning || kill)
            return;
        IsSceneTransitioning = false;
        rb.simulated = simulationBeforeLock;
        SetMovementEnabled(true);
        GetComponent<PlayerDoorInteractor2D>()?.SetInteractionEnabled(true);
    }

    private void LockPlayerBody()
    {
        SetMovementEnabled(false);
        GetComponent<PlayerDoorInteractor2D>()?.SetInteractionEnabled(false);
        simulationBeforeLock = rb.simulated;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
    }

    private IEnumerator PlayDeathAndReturn(EnemyAI.EnemyType type, bool returnToBedroom)
    {
        ResetJumpscare(DadJumpscare, DadJumpscareAnimator);
        ResetJumpscare(MomJumpscare, MomJumpscareAnimator);
        SpriteRenderer visual = type == EnemyAI.EnemyType.Dad ? DadJumpscare : MomJumpscare;
        Animator scareAnimator = type == EnemyAI.EnemyType.Dad ? DadJumpscareAnimator : MomJumpscareAnimator;
        string stateName = type == EnemyAI.EnemyType.Dad ? "Base Layer.DadJumpscare" : "Base Layer.MomJumpscare";
        bool animationAvailable = StartJumpscare(visual, scareAnimator, stateName);
        float audioDuration = SoundEffectManager.instance != null
            ? SoundEffectManager.instance.PlayJumpscareFor(type) : 0f;
        if (audioDuration <= 0f)
            audioDuration = MissingJumpscareDuration;

        double startedAt = Time.realtimeSinceStartupAsDouble;
        double lastAnimationProgressAt = startedAt;
        float lastAnimationProgress = -1f;
        bool animationFinished = false;
        while (true)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!animationFinished && animationAvailable && scareAnimator != null && scareAnimator.isActiveAndEnabled)
            {
                float progress = scareAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                if (progress > lastAnimationProgress)
                {
                    lastAnimationProgress = progress;
                    lastAnimationProgressAt = now;
                }
                animationFinished = progress >= 1f;
            }
            // Missing or stalled animation must not leave a permanent death lock.
            if (now - lastAnimationProgressAt >= MissingJumpscareDuration)
                animationFinished = true;
            if (animationFinished && now - startedAt >= audioDuration)
                break;
            yield return null;
        }

        deathRoutine = null;
        if (!returnToBedroom)
            yield break;

        string bedroom = NightmareBedroomIntro2D.BedroomSceneName;
        if (!Application.CanStreamedLevelBeLoaded(bedroom))
        {
            Debug.LogError("Nightmare respawn requires NM_Bedroom1 in Build Settings.", this);
            yield break;
        }

        NightmareBedroomIntro2D.ClearPendingArrival();
        SceneSpawnManager2D.PrepareArrival(bedroom, NightmareBedroomIntro2D.BedroomStartSpawnId);
        SceneManager.LoadScene(bedroom);
    }

    private static bool StartJumpscare(SpriteRenderer visual, Animator scareAnimator, string stateName)
    {
        if (visual == null)
            return false;
        visual.gameObject.SetActive(true);
        Canvas canvas = visual.GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
            canvas.worldCamera = Camera.main;
        }
        visual.enabled = true;

        if (scareAnimator == null || scareAnimator.runtimeAnimatorController == null)
            return false;
        scareAnimator.enabled = true;
        scareAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        int stateHash = Animator.StringToHash(stateName);
        if (!scareAnimator.HasState(0, stateHash))
            return false;
        scareAnimator.Play(stateHash, 0, 0f);
        scareAnimator.Update(0f);
        return true;
    }

    private static void ResetJumpscare(SpriteRenderer visual, Animator scareAnimator)
    {
        if (visual != null)
            visual.enabled = false;
        if (scareAnimator != null && scareAnimator.isActiveAndEnabled
            && scareAnimator.runtimeAnimatorController != null)
        {
            scareAnimator.Rebind();
            scareAnimator.Update(0f);
        }
        if (visual != null)
            visual.enabled = false;
    }

    private void BeginVoidDeath()
    {
        if (kill || IsSceneTransitioning)
            return;

        kill = true;

        LockPlayerBody();

        // Reset Nightmare progress/run just like normal death.
        if (NightmareTaskController.Instance != null)
            NightmareTaskController.Instance.ResetCurrentRun();

        DeathStarted?.Invoke();

        foreach (DialogueController2D dialogue in FindObjectsByType<DialogueController2D>())
            dialogue.CancelPlayback();

        foreach (DiaryReaderOverlay2D reader in FindObjectsByType<DiaryReaderOverlay2D>())
            reader.HideWithoutCallback();

        deathRoutine = StartCoroutine(VoidDeathAndReturnToBedroom());
    }

    private IEnumerator VoidDeathAndReturnToBedroom()
    {
        // Small pause after falling into the void
        yield return new WaitForSecondsRealtime(voidRespawnDelay);

        deathRoutine = null;

        string bedroom = NightmareBedroomIntro2D.BedroomSceneName;

        if (!Application.CanStreamedLevelBeLoaded(bedroom))
        {
            Debug.LogError(
                "Void respawn requires NM_Bedroom1 in Build Settings.",
                this
            );

            yield break;
        }

        NightmareBedroomIntro2D.ClearPendingArrival();

        SceneSpawnManager2D.PrepareArrival(
            bedroom,
            NightmareBedroomIntro2D.BedroomStartSpawnId
        );

        SceneManager.LoadScene(bedroom);
    }
    private bool IsCutsceneScene(string sceneName)
    {
        if (cutsceneSceneNames == null)
            return false;

        foreach (string cutsceneScene in cutsceneSceneNames)
        {
            if (sceneName == cutsceneScene)
                return true;
        }

        return false;
    }
    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector2 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

}