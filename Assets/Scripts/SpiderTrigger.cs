using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpiderTrigger : MonoBehaviour   // removed "abstract"
{
    [SerializeField] protected PlayerInteractable2D interactable;

    public void Configure(PlayerInteractable2D owner)   // changed protected → public
    {
        interactable = owner;
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && interactable != null)
            player.UnregisterInteractable(interactable);
    }

    private void Register(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player != null && interactable != null)
            player.RegisterInteractable(interactable);
    }

    private void OnValidate()
    {
        Collider2D zone = GetComponent<Collider2D>();
        if (zone != null)
            zone.isTrigger = true;
    }
}