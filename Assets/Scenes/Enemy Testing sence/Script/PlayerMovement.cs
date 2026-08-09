using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;

    [Header("地面检测")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private float horizontalInput;
    private bool isGrounded;
    private bool jumpRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // 获取左右移动输入
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 玩家按下Space时记录跳跃
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequested = true;
        }

        FlipPlayer();
    }

    private void FixedUpdate()
    {
        if (groundCheck == null)
        {
            Debug.LogWarning("GroundCheck还没有放入PlayerMovement！");
            return;
        }

        // 检查脚底是否碰到Ground Layer
        Collider2D groundHit = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        isGrounded = groundHit != null;

        float verticalVelocity = rb.linearVelocity.y;

        // 只有站在地面上才能跳跃
        if (jumpRequested && isGrounded)
        {
            verticalVelocity = jumpForce;
        }

        rb.linearVelocity = new Vector2(
            horizontalInput * moveSpeed,
            verticalVelocity
        );

        // 处理完跳跃输入后重置
        jumpRequested = false;
    }

    private void FlipPlayer()
    {
        if (horizontalInput == 0)
        {
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalInput);
        transform.localScale = scale;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}