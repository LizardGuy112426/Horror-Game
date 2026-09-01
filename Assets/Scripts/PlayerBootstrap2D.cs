using UnityEngine;

/// <summary>Creates the one persistent player when a gameplay Scene starts without one.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class PlayerBootstrap2D : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;

    private void Awake()
    {
        if (MCControllers.Instance != null)
            return;

        MCControllers[] scenePlayers = FindObjectsByType<MCControllers>(
            FindObjectsInactive.Exclude);

        if (scenePlayers.Length > 0)
            return;

        if (playerPrefab == null)
        {
            Debug.LogWarning(
                $"Player Bootstrap on '{name}' has no Player Prefab.",
                this);
            return;
        }

        Instantiate(playerPrefab);
    }
}
