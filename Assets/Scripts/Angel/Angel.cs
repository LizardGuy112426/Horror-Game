using UnityEngine;

public class Angel: MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool facesRightByDefault = true;
    private Transform player;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag(targetTag);
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (player == null)
            return;
        bool playerIsToTheRight = player.position.x > transform.position.x;

        spriteRenderer.flipX = facesRightByDefault ? !playerIsToTheRight : playerIsToTheRight;
    }
}