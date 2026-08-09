using UnityEngine;

public class PlayerHide : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private SpriteRenderer[] playerVisuals;

    public bool IsHidden { get; private set; }

    private HideSpot availableHideSpot;
    private HideSpot currentHideSpot;

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        if (IsHidden)
            ExitHide();
        else if (availableHideSpot != null)
            EnterHide();
    }

    public void SetAvailableHideSpot(HideSpot hideSpot)
    {
        availableHideSpot = hideSpot;
    }

    public void ClearAvailableHideSpot(HideSpot hideSpot)
    {
        if (!IsHidden && availableHideSpot == hideSpot)
            availableHideSpot = null;
    }

    private void EnterHide()
    {
        currentHideSpot = availableHideSpot;
        currentHideSpot.HidePrompt();
        transform.position = currentHideSpot.HidePosition.position;

        IsHidden = true;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        foreach (SpriteRenderer visual in playerVisuals)
            visual.enabled = false;
    }

    private void ExitHide()
    {
        transform.position = currentHideSpot.ExitPosition.position;

        rb.simulated = true;
        IsHidden = false;

        if (playerMovementScript != null)
            playerMovementScript.enabled = true;

        foreach (SpriteRenderer visual in playerVisuals)
            visual.enabled = true;

        currentHideSpot = null;
    }
}