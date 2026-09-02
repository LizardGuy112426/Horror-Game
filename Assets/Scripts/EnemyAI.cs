using UnityEngine;

/// <summary>
/// Moves an enemy between inspector-assigned patrol points and follows the player
/// while they are within range. This intentionally does not implement combat.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyAI : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField, Min(0f)] private float patrolSpeed = 2f;
    [SerializeField, Min(0f)] private float arriveDistance = 0.1f;

    [Header("Chase")]
    [SerializeField, Min(0f)] private float detectionRadius = 5f;
    [SerializeField, Min(0f)] private float chaseSpeed = 3.5f;
    [Tooltip("Optional explicit target. Leave empty to follow the persistent main character.")]
    [SerializeField] private Transform playerTarget;
    [Tooltip("Fallback used by the Enemy Testing scene when no persistent main character exists.")]
    [SerializeField] private LayerMask playerMask;

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
    private bool warnedAboutPatrolPoints;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator ??= GetComponent<Animator>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        player = FindPlayerInRange();
        isChasing = player != null;

        if (animator != null)
        {
            animator.speed = isChasing ? chaseAnimationSpeed : patrolAnimationSpeed;
        }
    }

    private void FixedUpdate()
    {
        if (isChasing)
        {
            MoveHorizontallyTo(player.position, chaseSpeed);
            return;
        }

        Patrol();
    }

    private Transform FindPlayerInRange()
    {
        Transform preferredTarget = playerTarget;

        if (preferredTarget == null && MCControllers.Instance != null)
        {
            preferredTarget = MCControllers.Instance.transform;
        }

        if (preferredTarget != null)
        {
            return IsWithinDetectionRange(preferredTarget.position) ? preferredTarget : null;
        }

        if (playerMask.value == 0)
        {
            return null;
        }

        Collider2D detectedCollider = Physics2D.OverlapCircle(transform.position, detectionRadius, playerMask);
        return detectedCollider != null ? detectedCollider.transform : null;
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
            rb.linearVelocityX = 0f;

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
            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = flipSpriteWhenFacingLeft && horizontalDistance < 0f;
        }

        // Preserve the vertical velocity so gravity and floor collisions remain physics-driven.
        rb.linearVelocityX = Mathf.Sign(horizontalDistance) * speed;
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
