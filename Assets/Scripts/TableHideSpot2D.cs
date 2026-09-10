using UnityEngine;

/// <summary>Lets the persistent player hide inside a table and swaps its cover sprite.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class TableHideSpot2D : PlayerInteractable2D
{
    [Header("Positions")]
    [SerializeField] private Transform hidePosition;
    [SerializeField] private Transform exitPosition;

    [Header("Table Visuals")]
    [SerializeField] private SpriteRenderer tableRenderer;
    [SerializeField] private Sprite normalTableSprite;
    [SerializeField] private Sprite hiddenTableSprite;

    [Header("Interaction Prompt")]
    [SerializeField] private GameObject interactPrompt;

    private Collider2D triggerZone;
    private PlayerDoorInteractor2D nearbyPlayer;
    private PlayerDoorInteractor2D hiddenPlayer;
    private Rigidbody2D hiddenBody;
    private MCControllers hiddenMovement;
    private SpriteRenderer[] hiddenVisuals;
    private bool[] hiddenVisualEnabledStates;
    private static TableHideSpot2D activeHideSpot;

    public static bool IsPlayerHidden => activeHideSpot != null && activeHideSpot.hiddenPlayer != null;
    public override Vector3 InteractionPosition => triggerZone != null
        ? triggerZone.bounds.center
        : transform.position;
    public override bool CanInteract => isActiveAndEnabled
        && (hiddenPlayer != null || nearbyPlayer != null);
    public override int InteractionPriority => hiddenPlayer != null ? 1000 : 10;

    private void Awake()
    {
        triggerZone = GetComponent<Collider2D>();
        triggerZone.isTrigger = true;

        if (tableRenderer == null)
            tableRenderer = GetComponent<SpriteRenderer>();
        if (normalTableSprite == null && tableRenderer != null)
            normalTableSprite = tableRenderer.sprite;

        RestoreTableSprite();
        SetFocused(false);
    }

    private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => RegisterPlayer(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player == null || player != nearbyPlayer || player == hiddenPlayer)
            return;

        player.UnregisterInteractable(this);
        nearbyPlayer = null;
    }

    public override void SetFocused(bool focused)
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(focused && CanInteract && hiddenPlayer == null);
    }

    public override bool Interact(PlayerDoorInteractor2D player)
    {
        if (player == null)
            return false;

        if (hiddenPlayer != null)
        {
            if (player != hiddenPlayer)
                return false;

            ExitHide();
            return true;
        }

        if (player != nearbyPlayer || hidePosition == null || exitPosition == null)
        {
            Debug.LogWarning($"Table hide spot '{name}' needs a player, Hide Position, and Exit Position.", this);
            return false;
        }

        EnterHide(player);
        return true;
    }

    private void RegisterPlayer(Collider2D other)
    {
        PlayerDoorInteractor2D player = other.GetComponentInParent<PlayerDoorInteractor2D>();
        if (player == null || hiddenPlayer != null)
            return;

        nearbyPlayer = player;
        player.RegisterInteractable(this);
    }

    private void EnterHide(PlayerDoorInteractor2D player)
    {
        hiddenPlayer = player;
        hiddenBody = player.GetComponent<Rigidbody2D>();
        hiddenMovement = player.GetComponent<MCControllers>();
        if (hiddenMovement == null)
            hiddenMovement = MCControllers.Instance;

        hiddenVisuals = player.GetComponentsInChildren<SpriteRenderer>(true);
        hiddenVisualEnabledStates = new bool[hiddenVisuals.Length];
        for (int index = 0; index < hiddenVisuals.Length; index++)
        {
            SpriteRenderer visual = hiddenVisuals[index];
            if (visual == null)
                continue;

            // Jumpscare renderers are player children but start disabled.
            hiddenVisualEnabledStates[index] = visual.enabled;
            visual.enabled = false;
        }

        hiddenMovement?.SetMovementEnabled(false);
        if (hiddenBody != null)
        {
            hiddenBody.linearVelocity = Vector2.zero;
            hiddenBody.simulated = false;
        }

        player.TeleportTo(hidePosition.position);
        SetTableSprite(hiddenTableSprite);
        SetFocused(false);
        activeHideSpot = this;
    }

    private void ExitHide()
    {
        PlayerDoorInteractor2D player = hiddenPlayer;

        RestoreTableSprite();
        if (hiddenBody != null)
            hiddenBody.simulated = true;

        player.TeleportTo(exitPosition.position);
        hiddenMovement?.SetMovementEnabled(true);
        RestorePlayerVisuals();

        hiddenPlayer = null;
        hiddenBody = null;
        hiddenMovement = null;
        hiddenVisuals = null;
        hiddenVisualEnabledStates = null;
        nearbyPlayer = null;
        ClearHiddenState();
        player.UnregisterInteractable(this);
    }

    private void RestoreTableSprite() => SetTableSprite(normalTableSprite);

    private void SetTableSprite(Sprite sprite)
    {
        if (tableRenderer != null && sprite != null)
            tableRenderer.sprite = sprite;
    }

    private void OnDisable() => RestoreFromInterruptedHide();
    private void OnDestroy() => RestoreFromInterruptedHide();

    private void RestoreFromInterruptedHide()
    {
        RestoreTableSprite();
        if (hiddenBody != null)
            hiddenBody.simulated = true;
        hiddenMovement?.SetMovementEnabled(true);
        RestorePlayerVisuals();

        hiddenPlayer = null;
        hiddenBody = null;
        hiddenMovement = null;
        hiddenVisuals = null;
        hiddenVisualEnabledStates = null;
        nearbyPlayer = null;
        ClearHiddenState();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHiddenState() => activeHideSpot = null;

    private void ClearHiddenState()
    {
        if (activeHideSpot == this)
            activeHideSpot = null;
    }

    private void RestorePlayerVisuals()
    {
        if (hiddenVisuals == null || hiddenVisualEnabledStates == null)
            return;

        for (int index = 0; index < hiddenVisuals.Length; index++)
        {
            SpriteRenderer visual = hiddenVisuals[index];
            if (visual != null)
                visual.enabled = hiddenVisualEnabledStates[index];
        }
    }

    private void OnValidate()
    {
        Collider2D zone = GetComponent<Collider2D>();
        if (zone != null)
            zone.isTrigger = true;

        if (tableRenderer == null)
            tableRenderer = GetComponent<SpriteRenderer>();
        if (normalTableSprite == null && tableRenderer != null)
            normalTableSprite = tableRenderer.sprite;
    }
}
