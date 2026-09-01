using UnityEngine;

/// <summary>Registers a stage-gated Parents NPC with the persistent player's E interaction.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ParentsInteractionTrigger2D : MonoBehaviour
{
    [SerializeField] private ParentsNPC2D parents;

    public void Configure(ParentsNPC2D owner)
    {
        parents = owner;
        Collider2D zone = GetComponent<Collider2D>();
        if (zone != null)
            zone.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && parents != null)
            player.UnregisterInteractable(parents);
    }

    private void Register(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && parents != null && parents.CanInteract)
            player.RegisterInteractable(parents);
    }

    private void OnValidate()
    {
        Collider2D zone = GetComponent<Collider2D>();
        if (zone != null)
            zone.isTrigger = true;
    }
}
