using UnityEngine;

public class MCController : MonoBehaviour
{
    private const float DirectionDeadZone = 0.01f;

    float xInput;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private int Speed;
    [SerializeField] private int CrouchSpeed;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer characterRenderer;

    private bool movementEnabled = true;

    public void SetMovementEnabled(bool value)
    {
        movementEnabled = value;
        if (movementEnabled)
            return;

        xInput = 0f;
        if (rb != null)
            rb.linearVelocityX = 0f;
        if (animator != null)
            animator.SetFloat("Speed", 0f);
    }

    private void Awake()
    {
        ResolveCharacterRenderer();
    }

    private void OnValidate()
    {
        ResolveCharacterRenderer();
    }

    private void Update()
    {
        if (!movementEnabled)
        {
            xInput = 0f;
            if (rb != null)
                rb.linearVelocityX = 0f;
            return;
        }

        // A/D movement
        xInput = Input.GetAxis("Horizontal");

        // The source artwork faces right. Flip only the SpriteRenderer when moving left.
        if (characterRenderer != null)
        {
            if (xInput < -DirectionDeadZone)
                characterRenderer.flipX = true;
            else if (xInput > DirectionDeadZone)
                characterRenderer.flipX = false;
        }

        // Check if W is being held
        bool isCrouching = Input.GetKey(KeyCode.W);

        // Normal Walk
        if (isCrouching)
        {
            rb.linearVelocityX = xInput * Speed;
        }
        // Crouch Walk
        else
        {
            rb.linearVelocityX = xInput * CrouchSpeed;
        }


        //Character animation
        animator.SetFloat("Speed", Mathf.Abs(xInput));
        animator.SetBool("Crouch", isCrouching);
    }

    private void ResolveCharacterRenderer()
    {
        if (characterRenderer == null)
            characterRenderer = GetComponent<SpriteRenderer>();
    }
}
