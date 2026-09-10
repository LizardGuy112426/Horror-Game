using UnityEngine;

/// <summary>
/// Self-contained temporary Nightmare furniture. It creates the same E/dialogue
/// interaction used by existing furniture and records its objective on dialogue completion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(ItemDialogueInteractable2D))]
[RequireComponent(typeof(ItemInteractionTrigger2D))]
public sealed class NightmareObjectiveFurniture2D : MonoBehaviour
{
    [Header("World Visual")]
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
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && worldSprite != null)
            renderer.sprite = worldSprite;

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.size = interactionSize;
        }

    }

    private void EnsureInteraction()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (worldSprite != null)
            renderer.sprite = worldSprite;

        interactionCollider = GetComponent<BoxCollider2D>();
        interactionCollider.isTrigger = true;
        interactionCollider.size = interactionSize;

        dialogueItem = GetComponent<ItemDialogueInteractable2D>();
        interactionTrigger = GetComponent<ItemInteractionTrigger2D>();

        if (dialogueItem == null || interactionTrigger == null)
        {
            Debug.LogWarning($"Nightmare furniture '{name}' is missing its dialogue components.", this);
            return;
        }

        promptObject = CreatePrompt();
        dialogueItem.Configure(interactionCollider, promptObject, null);
        interactionTrigger.Configure(dialogueItem);
    }

    private GameObject CreatePrompt()
    {
        Transform existingPrompt = transform.Find("E Prompt");
        if (existingPrompt != null)
            return existingPrompt.gameObject;

        GameObject prompt = new("E Prompt", typeof(TextMesh));
        prompt.transform.SetParent(transform, false);
        prompt.transform.localPosition = promptOffset;

        TextMesh text = prompt.GetComponent<TextMesh>();
        text.text = "E";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 54;
        text.characterSize = 0.1f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;

        MeshRenderer renderer = prompt.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = 100;

        prompt.SetActive(false);
        return prompt;
    }
}
