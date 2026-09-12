using System;
using UnityEngine;

/// <summary>
/// Moves an enemy between inspector-assigned patrol points and follows the player
/// while they are within range. This intentionally does not implement combat.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyAI : MonoBehaviour
{
    public enum EnemyType { Dad, Mom }
    private enum EventMode { None, Idle, Chase }

    [Header("Enemy Type")]
    [Tooltip("Controls which SoundEffectManager clips (Dad or Mom) this instance uses.")]
    [SerializeField] private EnemyType enemyType = EnemyType.Dad;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField, Min(0f)] private float patrolSpeed = 2f;
    [SerializeField, Min(0f)] private float arriveDistance = 0.1f;

    [Header("Chase")]
    [SerializeField, Min(0f)] private float detectionRadius = 5f;
    [SerializeField, Min(0f)] private float chaseSpeed = 3.5f;
    [SerializeField] private string targetTag = "Player";
    float WalkingSFXTime;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0f)] private float patrolAnimationSpeed = 1f;
    [SerializeField, Min(0f)] private float chaseAnimationSpeed = 2f;
    [SerializeField] private bool flipSpriteWhenFacingLeft = true;

    private Rigidbody2D rb;
    private int patrolIndex;
    private Transform player;
    private bool isChasing;
    private bool wasChasing;
    private bool isWaitingAfterLosingTarget;
    private float lostTargetWaitRemaining;
    private bool warnedAboutPatrolPoints;
    private const float LostTargetWaitDuration = 1f;
    [SerializeField] private float baseWalkingSFXInterval = 0.4f;
    [SerializeField] private float minWalkingSFXInterval = 0.15f;
    [SerializeField] private float Speed;

    private EventMode eventMode;
    private Transform eventTarget;
    private float eventChaseSpeed = -1f;
    private bool simulationBeforeEvent;
    private Func<MCControllers, bool> storyContactHandler;

    public void SetStoryContactHandler(Func<MCControllers, bool> handler) => storyContactHandler = handler;

    public bool TryHandleStoryContact(MCControllers target) => storyContactHandler?.Invoke(target) == true;

    private void OnDisable() => storyContactHandler = null;

    public void ConfigureEnemyType(EnemyType type) => enemyType = type;

    public void SetEventIdle()
    {
        CacheComponents();
        if (eventMode == EventMode.None)
            simulationBeforeEvent = rb.simulated;
        eventMode = EventMode.Idle;
        eventTarget = null;
        ResetChaseState();
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        StopInPlace();
        if (animator != null)
            animator.speed = patrolAnimationSpeed;
    }

    public void ChaseForEvent(Transform target)
    {
        ChaseForEvent(target, -1f);
    }

    public void ChaseForEvent(Transform target, float speedOverride)
    {
        CacheComponents();
        if (eventMode == EventMode.None)
            simulationBeforeEvent = rb.simulated;
        eventMode = EventMode.Chase;
        eventTarget = target;
        eventChaseSpeed = speedOverride;
        rb.simulated = simulationBeforeEvent;
        ResetChaseState();
    }

    public void ReleaseEventControl()
    {
        storyContactHandler = null;
        if (eventMode == EventMode.None)
            return;
        rb.simulated = simulationBeforeEvent;
        eventMode = EventMode.None;
        eventTarget = null;
        eventChaseSpeed = -1f;
        ResetChaseState();
        StopInPlace();
    }

    private void ResetChaseState()
    {
        player = null;
        isChasing = false;
        wasChasing = false;
        isWaitingAfterLosingTarget = false;
        lostTargetWaitRemaining = 0f;
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (eventMode == EventMode.Idle || PlayerUnavailable())
        {
            ResetChaseState();
            StopInPlace();
            return;
        }

        player = FindPlayerInRange();
        isChasing = player != null;

        if (isChasing)
        {
            isWaitingAfterLosingTarget = false;
            lostTargetWaitRemaining = 0f;
        }
        else if (wasChasing)
        {
            isWaitingAfterLosingTarget = true;
            lostTargetWaitRemaining = LostTargetWaitDuration;
        }
        else if (isWaitingAfterLosingTarget)
        {
            lostTargetWaitRemaining -= Time.deltaTime;
            if (lostTargetWaitRemaining <= 0f)
            {
                lostTargetWaitRemaining = 0f;
                isWaitingAfterLosingTarget = false;
            }
        }

        if (animator != null)
        {
            animator.speed = isChasing ? chaseAnimationSpeed : patrolAnimationSpeed;
        }

        // Record this before optional audio playback so a missing audio manager
        // can never prevent the chase/lost-target state machine from advancing.
        bool startedChasing = isChasing && !wasChasing;
        wasChasing = isChasing;

        // Fires once on the exact frame the enemy spots the player.
        if (startedChasing)
        {
            PlaySpotSFX();
        }

        // Report proximity every frame while chasing so the shared heartbeat can scale with it.
        if (isChasing)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (SoundEffectManager.instance != null)
                SoundEffectManager.instance.ReportChaseProximity(distanceToPlayer, detectionRadius);
        }

        if (rb.linearVelocityX != 0)
        {
            WalkingSFXTime -= Time.deltaTime;
            if (WalkingSFXTime <= 0)
            {
                float currentSpeed = Mathf.Abs(rb.linearVelocityX);
                PlayWalkSFX(currentSpeed);

                float speedFraction = Mathf.InverseLerp(0f, Speed, currentSpeed);
                WalkingSFXTime = Mathf.Lerp(baseWalkingSFXInterval, minWalkingSFXInterval, speedFraction);
            }
        }
    }

    private void PlaySpotSFX()
    {
        if (AudioManager.instance == null)
            return;

        if (enemyType == EnemyType.Dad)
            AudioManager.instance.DadSpotSFXPlay();
        else
            AudioManager.instance.MomSpotSFXPlay();
    }

    private void PlayWalkSFX(float currentSpeed)
    {
        if (SoundEffectManager.instance == null)
            return;

        if (enemyType == EnemyType.Dad)
            SoundEffectManager.instance.DadWalkSFX(currentSpeed);
        else
            SoundEffectManager.instance.MomWalkSFX(currentSpeed);
    }

    private void FixedUpdate()
    {
        if (eventMode == EventMode.Idle || PlayerUnavailable()
            || (eventMode == EventMode.Chase && (TableHideSpot2D.IsPlayerHidden || eventTarget == null)))
        {
            StopInPlace();
            return;
        }

        if (isChasing && player != null)
        {
            float speed = eventMode == EventMode.Chase && eventChaseSpeed >= 0f
                ? eventChaseSpeed
                : chaseSpeed;
            MoveHorizontallyTo(player.position, speed);
            return;
        }

        if (isWaitingAfterLosingTarget || eventMode == EventMode.Chase)
        {
            StopInPlace();
            return;
        }

        Patrol();
    }

    private Transform FindPlayerInRange()
    {
        if (TableHideSpot2D.IsPlayerHidden)
            return null;

        if (eventMode == EventMode.Chase)
            return eventTarget != null && eventTarget.gameObject.activeInHierarchy ? eventTarget : null;

        Transform target = MCControllers.Instance != null ? MCControllers.Instance.transform : null;
        if (target == null)
        {
            GameObject targetObject = GameObject.FindWithTag(targetTag);
            if (targetObject == null)
                return null;
            target = targetObject.transform;
        }


        if (IsWithinDetectionRange(target.position))
            return target;

        return null;
    }

    private static bool PlayerUnavailable()
    {
        MCControllers movement = MCControllers.Instance;
        return movement != null && (movement.IsDying || movement.IsSceneTransitioning);
    }

    private bool IsWithinDetectionRange(Vector3 targetPosition)
    {
        float radiusSquared = detectionRadius * detectionRadius;
        return (targetPosition - transform.position).sqrMagnitude <= radiusSquared;
    }

    private void Patrol()
    {
        if (!TryGetPatrolTarget(out Transform target))
        {
            StopInPlace();

            if (!warnedAboutPatrolPoints)
            {
                Debug.LogWarning($"{name} has no valid patrol points. Assign one or more points in the EnemyAI component.", this);
                warnedAboutPatrolPoints = true;
            }

            return;
        }

        warnedAboutPatrolPoints = false;
        MoveHorizontallyTo(target.position, patrolSpeed);

        if (Mathf.Abs(rb.position.x - target.position.x) <= arriveDistance)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    private void StopInPlace()
    {
        rb.linearVelocityX = 0f;

        if (animator != null && animator.isActiveAndEnabled)
            animator.SetBool("isWalking", false);
    }

    private bool TryGetPatrolTarget(out Transform target)
    {
        target = null;

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return false;
        }

        for (int indexOffset = 0; indexOffset < patrolPoints.Length; indexOffset++)
        {
            int candidateIndex = (patrolIndex + indexOffset) % patrolPoints.Length;
            Transform candidate = patrolPoints[candidateIndex];

            if (candidate == null)
            {
                continue;
            }

            patrolIndex = candidateIndex;
            target = candidate;
            return true;
        }

        return false;
    }

    private void MoveHorizontallyTo(Vector2 targetPosition, float speed)
    {
        float horizontalDistance = targetPosition.x - rb.position.x;

        if (Mathf.Abs(horizontalDistance) <= arriveDistance)
        {
            rb.linearVelocityX = 0f;

            if (animator != null)
                animator.SetBool("isWalking", false);

            return;
        }

        // Flip enemy based on movement direction
        if (spriteRenderer != null && flipSpriteWhenFacingLeft)
        {
            if (horizontalDistance < 0f)
                spriteRenderer.flipX = false;   // Facing left
            else if (horizontalDistance > 0f)
                spriteRenderer.flipX = true;  // Facing right
        }

        // Move horizontally
        rb.linearVelocityX = Mathf.Sign(horizontalDistance) * speed;

        if (animator != null)
            animator.SetBool("isWalking", true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        if (patrolPoints == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        foreach (Transform point in patrolPoints)
        {
            if (point == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(point.position, 0.15f);
            Gizmos.DrawLine(transform.position, point.position);
        }
    }
}
