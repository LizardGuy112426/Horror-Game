using UnityEngine;

/// <summary>Minimal left/right player controller for the EmptyScene prototype.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class SimplePlayer2D : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float moveSpeed = 4f;

    private Rigidbody2D body;
    private SpriteRenderer visual;
    private float horizontalInput;
    private bool movementEnabled = true;

    public void SetMoveSpeed(float newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
    }

    public void SetMovementEnabled(bool value)
    {
        movementEnabled = value;
        if (!movementEnabled)
        {
            horizontalInput = 0f;
            if (body != null)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        visual = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (!movementEnabled)
        {
            horizontalInput = 0f;
            return;
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (horizontalInput != 0f && visual != null)
            visual.flipX = horizontalInput < 0f;
    }

    private void FixedUpdate()
    {
        float horizontalVelocity = movementEnabled ? horizontalInput * moveSpeed : 0f;
        body.linearVelocity = new Vector2(horizontalVelocity, body.linearVelocity.y);
    }
}
