using UnityEngine;

public class HideSpot : MonoBehaviour
{
    [SerializeField] private Transform hidePosition;
    [SerializeField] private Transform exitPosition;
    [SerializeField] private GameObject interactPrompt;

    public Transform HidePosition => hidePosition;
    public Transform ExitPosition => exitPosition;

    private void Awake()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHide playerHide = other.GetComponentInParent<PlayerHide>();

        if (playerHide != null)
        {
            playerHide.SetAvailableHideSpot(this);
            SetPromptVisible(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerHide playerHide = other.GetComponentInParent<PlayerHide>();

        if (playerHide != null)
        {
            playerHide.ClearAvailableHideSpot(this);
            SetPromptVisible(false);
        }
    }

    public void HidePrompt()
    {
        SetPromptVisible(false);
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(visible);
    }
}