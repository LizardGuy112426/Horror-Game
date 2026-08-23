using UnityEngine;

/// <summary>Registers an item through enter and stay so a missed enter event self-repairs.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ItemInteractionTrigger2D : MonoBehaviour
{
    [SerializeField] private ItemDialogueInteractable2D item;

    public void Configure(ItemDialogueInteractable2D owner)
    {
        item = owner;
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && item != null)
            player.UnregisterInteractable(item);
    }

    private void Register(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && item != null)
            player.RegisterInteractable(item);
    }

    private void OnValidate()
    {
        Collider2D zone = GetComponent<Collider2D>();
        if (zone != null)
            zone.isTrigger = true;
    }
}
