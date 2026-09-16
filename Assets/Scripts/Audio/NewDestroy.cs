using UnityEngine;

public class HidePlayerOnSceneEnter : MonoBehaviour
{
    private void Start()
    {
        MCControllers player = MCControllers.Instance;

        if (player == null)
            return;

        // Stop normal player controls.
        player.SetMovementEnabled(false);

        PlayerDoorInteractor2D interactor =
            player.GetComponent<PlayerDoorInteractor2D>();

        if (interactor != null)
            interactor.SetInteractionEnabled(false);

        // Stop physics.
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // Hide character sprites only.
        SpriteRenderer[] renderers =
            player.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
            renderer.enabled = false;

        // Disable player collision.
        Collider2D[] colliders =
            player.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D col in colliders)
            col.enabled = false;
    }
}