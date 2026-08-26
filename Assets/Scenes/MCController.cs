using UnityEngine;

public class MCController : MonoBehaviour
{
    float xInput;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private int Speed;
    [SerializeField] private int CrouchSpeed;
    [SerializeField] private Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // A/D movement
        xInput = Input.GetAxis("Horizontal");

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
}
