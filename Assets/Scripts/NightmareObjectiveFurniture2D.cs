using UnityEngine;

/// <summary>
/// Self-contained temporary Nightmare furniture. It creates the same E/dialogue
/// interaction used by existing furniture and records its objective on dialogue completion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(ItemDialogueInteractable2D))]
[RequireComponent(typeof(ItemInteractionTrigger2D))]
public sealed class NightmareObjectiveFurniture2D : MonoBehaviour
{
    [Header("Optional World Visual")]
    [Tooltip("Optional. Leave empty to preserve this object's current visual, or use no SpriteRenderer at all.")]
    [SerializeField] private Sprite worldSprite;
    [SerializeField] private Vector2 interactionSize = new(1.5f, 1.5f);
    [SerializeField] private Vector3 promptOffset = new(0f, 1.1f, 0f);

    private BoxCollider2D interactionCollider;
    private ItemDialogueInteractable2D dialogueItem;
    private ItemInteractionTrigger2D interactionTrigger;
    private GameObject promptObject;

    private void Awake()
    {
        EnsureInteraction();
    }

    private void OnValidate()
    {
        ApplyWorldVisual();
        ConfigureTrigger();
        UpdateExistingPromptPosition();
    }

    private void EnsureInteraction()
    {
        interactionCollider = GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
        {
            Debug.LogWarning($"Nightmare furniture '{name}' is missing a BoxCollider2D.", this);
            return;
        }

        ApplyWorldVisual();
        ConfigureTrigger();

        dialogueItem = GetComponent<ItemDialogueInteractable2D>();
        interactionTrigger = GetComponent<ItemInteractionTrigger2D>();

        if (dialogueItem == null || interactionTrigger == null)
        {
            Debug.LogWarning($"Nightmare furniture '{name}' is missing its dialogue components.", this);
            return;
        }

        promptObject = WorldEPrompt2D.GetOrCreate(transform, promptOffset);
        dialogueItem.Configure(interactionCollider, promptObject, null);
        interactionTrigger.Configure(dialogueItem);
    }

    private void ApplyWorldVisual()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && worldSprite != null)
            renderer.sprite = worldSprite;
    }

    private void ConfigureTrigger()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (trigger == null)
            return;

        trigger.isTrigger = true;
        trigger.size = interactionSize;
    }

    private void UpdateExistingPromptPosition()
    {
        Transform existingPrompt = transform.Find("E Prompt");
        if (existingPrompt != null)
            existingPrompt.localPosition = promptOffset;
    }
}
