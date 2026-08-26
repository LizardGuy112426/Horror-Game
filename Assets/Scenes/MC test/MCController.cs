using UnityEngine;

public class MCController : MonoBehaviour
{
    float xInput;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private int Speed;
    [SerializeField] private Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        xInput = Input.GetAxis("Horizontal");
        rb.linearVelocityX = xInput * Speed;

        //Character animation
        animator.SetFloat("Speed", Mathf.Abs(xInput));

    }
}
