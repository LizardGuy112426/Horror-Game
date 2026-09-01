using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Uses separate keys for the nearest item and the nearest door in range.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerDoorInteractor2D : MonoBehaviour
{
    [Header("Interaction Keys")]
    [FormerlySerializedAs("interactionKey")]
    [SerializeField] private KeyCode itemInteractionKey = KeyCode.E;
    [SerializeField] private KeyCode doorInteractionKey = KeyCode.E;

    private readonly HashSet<PlayerInteractable2D> nearbyInteractables = new();
    private Rigidbody2D body;
    private PlayerInteractable2D focusedItem;
    private DoorTransition2D focusedDoor;
    private bool interactionEnabled = true;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!interactionEnabled)
            return;

        RefreshFocusedInteractables();

        if (focusedItem != null && Input.GetKeyDown(itemInteractionKey))
        {
            if (focusedItem.Interact(this))
                return;
        }

        if (focusedDoor != null && Input.GetKeyDown(doorInteractionKey))
            focusedDoor.Interact(this);
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
        if (focusedItem == interactable)
        {
            focusedItem.SetFocused(false);
            focusedItem = null;
        }

        if (focusedDoor == interactable)
        {
            focusedDoor.SetFocused(false);
            focusedDoor = null;
        }
    }

    public void ForgetDoor(DoorTransition2D door)
    {
        UnregisterInteractable(door);
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        if (!interactionEnabled)
        {
            ClearFocusedItem();
            ClearFocusedDoor();
        }
    }

    public void TeleportTo(Vector3 destination)
    {
        body.linearVelocity = Vector2.zero;
        body.position = new Vector2(destination.x, destination.y);
        transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        Physics2D.SyncTransforms();
        Debug.Log($"TELEPORT RESULT: {transform.position}");
}

    private void RefreshFocusedInteractables()
    {
        nearbyInteractables.RemoveWhere(interactable =>
            interactable == null || !interactable.CanInteract);

        PlayerInteractable2D nearestItem = null;
        DoorTransition2D nearestDoor = null;
        int highestItemPriority = int.MinValue;
        float nearestItemDistance = float.PositiveInfinity;
        float nearestDoorDistance = float.PositiveInfinity;

        foreach (PlayerInteractable2D interactable in nearbyInteractables)
        {
            float distance = (interactable.InteractionPosition - transform.position).sqrMagnitude;

            if (interactable is not DoorTransition2D
                && (interactable.InteractionPriority > highestItemPriority
                    || (interactable.InteractionPriority == highestItemPriority
                        && distance < nearestItemDistance)))
            {
                highestItemPriority = interactable.InteractionPriority;
                nearestItemDistance = distance;
                nearestItem = interactable;
            }
            else if (interactable is DoorTransition2D door
                && distance < nearestDoorDistance)
            {
                nearestDoorDistance = distance;
                nearestDoor = door;
            }
        }

        UpdateItemFocus(nearestItem);
        UpdateDoorFocus(nearestDoor);
    }

    private void UpdateItemFocus(PlayerInteractable2D nearestItem)
    {
        if (nearestItem == focusedItem)
        {
            if (focusedItem != null)
                focusedItem.SetFocused(true);
            return;
        }

        ClearFocusedItem();
        focusedItem = nearestItem;
        if (focusedItem != null)
            focusedItem.SetFocused(true);
    }

    private void UpdateDoorFocus(DoorTransition2D nearestDoor)
    {
        if (nearestDoor == focusedDoor)
        {
            if (focusedDoor != null)
                focusedDoor.SetFocused(true);
            return;
        }

        ClearFocusedDoor();
        focusedDoor = nearestDoor;
        if (focusedDoor != null)
            focusedDoor.SetFocused(true);
    }

    private void ClearFocusedItem()
    {
        if (focusedItem != null)
            focusedItem.SetFocused(false);
        focusedItem = null;
    }

    private void ClearFocusedDoor()
    {
        if (focusedDoor != null)
            focusedDoor.SetFocused(false);
        focusedDoor = null;
    }

    private void OnDisable()
    {
        ClearFocusedItem();
        ClearFocusedDoor();
        nearbyInteractables.Clear();
    }
}
