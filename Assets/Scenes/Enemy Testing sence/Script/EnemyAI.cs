using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float arriveDistance = 0.1f;

    [Header("Chase")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private LayerMask playerMask;

    private Rigidbody2D rb;
    private int patrolIndex;
    private Transform player;
    private bool isChasing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // 在侦测范围内寻找 Player 图层的碰撞体
        Collider2D foundPlayer = Physics2D.OverlapCircle(
            transform.position,
            detectionRadius,
            playerMask
        );

        if (foundPlayer != null)
        {
            player = foundPlayer.transform;
            isChasing = true;
        }
        else
        {
            isChasing = false;
        }
    }

    private void FixedUpdate()
    {
        if (isChasing && player != null)
        {
            MoveHorizontallyTo(player.position, chaseSpeed);
        }
        else
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Transform target = patrolPoints[patrolIndex];
        MoveHorizontallyTo(target.position, patrolSpeed);

        Vector2 targetPosition = new Vector2(target.position.x, rb.position.y);

        if (Vector2.Distance(rb.position, targetPosition) <= arriveDistance)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    private void MoveHorizontallyTo(Vector2 target, float speed)
    {
        Vector2 destination = new Vector2(target.x, rb.position.y);

        rb.MovePosition(Vector2.MoveTowards(
            rb.position,
            destination,
            speed * Time.fixedDeltaTime
        ));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}