using UnityEngine;

/// <summary>Reliable trigger relay. OnTriggerStay repairs missed enter events automatically.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class DoorInteractionTrigger2D : MonoBehaviour
{
    [SerializeField] private DoorTransition2D door;

    public void Configure(DoorTransition2D owner)
    {
        door = owner;
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Register(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Register(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && door != null)
            player.UnregisterInteractable(door);
    }

    private void Register(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && door != null)
            player.RegisterInteractable(door);
    }

    private void OnValidate()
    {
        Collider2D zone = GetComponent<Collider2D>();
        if (zone != null)
            zone.isTrigger = true;
    }
}
