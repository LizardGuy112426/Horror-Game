using UnityEngine;

/// <summary>Shared contract for doors, items, and future world interactions.</summary>
public abstract class PlayerInteractable2D : MonoBehaviour
{
    public abstract Vector3 InteractionPosition { get; }
    public virtual bool CanInteract => isActiveAndEnabled;

    public abstract void SetFocused(bool focused);
    public abstract bool Interact(PlayerDoorInteractor2D player);
}
