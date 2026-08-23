using System.Collections.Generic;
using UnityEngine;

/// <summary>Chooses the nearest door or item in range and owns the player's E-key interaction.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerDoorInteractor2D : MonoBehaviour
{
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    private readonly HashSet<PlayerInteractable2D> nearbyInteractables = new();
    private Rigidbody2D body;
    private PlayerInteractable2D focusedInteractable;
    private bool interactionEnabled = true;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!interactionEnabled)
            return;

        RefreshFocusedInteractable();

        if (focusedInteractable != null && Input.GetKeyDown(interactionKey))
            focusedInteractable.Interact(this);
    }

    public void RegisterDoor(DoorTransition2D door)
    {
        RegisterInteractable(door);
    }

    public void UnregisterDoor(DoorTransition2D door)
    {
        UnregisterInteractable(door);
    }

    public void RegisterInteractable(PlayerInteractable2D interactable)
    {
        if (interactable != null)
            nearbyInteractables.Add(interactable);
    }

    public void UnregisterInteractable(PlayerInteractable2D interactable)
    {
        if (interactable == null)
            return;

        nearbyInteractables.Remove(interactable);
        if (focusedInteractable == interactable)
        {
            focusedInteractable.SetFocused(false);
            focusedInteractable = null;
        }
    }

    public void ForgetDoor(DoorTransition2D door)
    {
        UnregisterInteractable(door);
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        if (!interactionEnabled && focusedInteractable != null)
        {
            focusedInteractable.SetFocused(false);
            focusedInteractable = null;
        }
    }

    public void TeleportTo(Vector3 destination)
    {
        body.linearVelocity = Vector2.zero;
        body.position = new Vector2(destination.x, destination.y);
        transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        Physics2D.SyncTransforms();
    }

    private void RefreshFocusedInteractable()
    {
        nearbyInteractables.RemoveWhere(interactable =>
            interactable == null || !interactable.CanInteract);

        PlayerInteractable2D nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (PlayerInteractable2D interactable in nearbyInteractables)
        {
            float distance = (interactable.InteractionPosition - transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = interactable;
            }
        }

        if (nearest == focusedInteractable)
        {
            if (focusedInteractable != null)
                focusedInteractable.SetFocused(true);
            return;
        }

        if (focusedInteractable != null)
            focusedInteractable.SetFocused(false);

        focusedInteractable = nearest;
        if (focusedInteractable != null)
            focusedInteractable.SetFocused(true);
    }

    private void OnDisable()
    {
        if (focusedInteractable != null)
            focusedInteractable.SetFocused(false);
        focusedInteractable = null;
        nearbyInteractables.Clear();
    }
}
