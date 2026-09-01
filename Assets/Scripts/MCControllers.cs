using UnityEngine;

public class MCControllers : MonoBehaviour
{
    public static MCControllers Instance { get; private set; }

    float xInput;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float Speed;
    [SerializeField] private int Jumpforce;
    [SerializeField] private Transform GroundChecker;
    [SerializeField] private Vector2 GroundChekerSize;
    [SerializeField] private LayerMask Ground;
    bool onGround;
    [SerializeField] private Animator animator;
    private bool isFacingRight = true;
    private bool movementEnabled = true;

    public void SetMovementEnabled(bool value)
    {
        movementEnabled = value;

        if (!movementEnabled)
        {
            xInput = 0f;

            if (rb != null)
                rb.linearVelocityX = 0f;

            if (animator != null)
                animator.SetBool("isWalking", false);
        }
    }
    private void OnDrawGizmos()
    {
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
    }

    // Update is called once per frame
    void Update()
    {
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

        if (Input.GetKey(KeyCode.Space) && onGround)
        {
            rb.linearVelocityY = Jumpforce;
            GameState.Instance.Jump = true;
        }
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
            animator.SetBool("onGround",false);
        }
        if (xInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (xInput < 0 && isFacingRight)
        {
            Flip();
        }
    }
    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector2 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }
   
}
