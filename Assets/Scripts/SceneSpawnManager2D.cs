using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>Stores one door arrival request and consumes it after the target Scene loads.</summary>
public static class SceneSpawnManager2D
{
    private static string pendingSceneName;
    private static string pendingSpawnId;
    private static bool hasPendingArrival;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        pendingSceneName = string.Empty;
        pendingSpawnId = string.Empty;
        hasPendingArrival = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void PrepareArrival(string targetSceneName, string targetSpawnId)
    {
        pendingSceneName = targetSceneName == null ? string.Empty : targetSceneName.Trim();
        pendingSpawnId = targetSpawnId == null ? string.Empty : targetSpawnId.Trim();
        hasPendingArrival = !string.IsNullOrWhiteSpace(pendingSpawnId);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool hadPendingArrival = hasPendingArrival;
        string requestedScene = pendingSceneName;
        string requestedSpawn = pendingSpawnId;
        ClearPendingArrival();

        if (hadPendingArrival
            && !string.Equals(scene.name, requestedScene, System.StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"Door arrival expected scene '{requestedScene}', but '{scene.name}' loaded. "
                + "The arrival request was discarded.");
            return;
        }

        PlayerDoorInteractor2D player = FindPlayer(scene);
        if (player == null)
        {
            if (hadPendingArrival)
            {
                Debug.LogWarning(
                    $"Scene '{scene.name}' has no persistent PlayerDoorInteractor2D. "
                    + $"Could not use Spawn ID '{requestedSpawn}'.");
            }
            return;
        }

        string spawnToUse = hadPendingArrival ? requestedSpawn : "Default";
        SceneSpawnPoint2D destination = FindSpawnPoint(scene, spawnToUse);

        if (destination != null)
        {
            player.TeleportTo(destination.transform.position);
            Debug.Log(
                $"Player placed at Spawn '{spawnToUse}' in scene '{scene.name}'.",
                destination);
        }
        else if (hadPendingArrival)
        {
            Debug.LogWarning(
                $"Scene '{scene.name}' has no Scene Spawn Point 2D with Spawn ID "
                + $"'{requestedSpawn}'. The Player kept its previous position.");
        }

        BindSceneReferences(scene, player);
    }

    private static SceneSpawnPoint2D FindSpawnPoint(Scene scene, string spawnId)
    {
        SceneSpawnPoint2D match = null;
        SceneSpawnPoint2D[] points = Object.FindObjectsByType<SceneSpawnPoint2D>(
            FindObjectsInactive.Include);

        foreach (SceneSpawnPoint2D point in points)
        {
            if (point.gameObject.scene != scene
                || !string.Equals(point.SpawnId, spawnId, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (match == null)
            {
                match = point;
            }
            else
            {
                Debug.LogWarning(
                    $"Scene '{scene.name}' has more than one Spawn ID '{spawnId}'. "
                    + $"Using '{match.name}'.",
                    match);
            }
        }

        return match;
    }

    private static PlayerDoorInteractor2D FindPlayer(Scene scene)
    {
        if (MCControllers.Instance != null)
        {
            PlayerDoorInteractor2D persistentPlayer =
                MCControllers.Instance.GetComponent<PlayerDoorInteractor2D>();
            if (persistentPlayer != null && persistentPlayer.gameObject.activeInHierarchy)
                return persistentPlayer;
        }

        PlayerDoorInteractor2D[] players = Object.FindObjectsByType<PlayerDoorInteractor2D>(
            FindObjectsInactive.Include);

        foreach (PlayerDoorInteractor2D player in players)
        {
            if (player.gameObject.activeInHierarchy)
                return player;
        }

        return null;
    }

    private static void BindSceneReferences(
        Scene scene,
        PlayerDoorInteractor2D player)
    {
        MCControllers movement = player.GetComponent<MCControllers>();

        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include);
        foreach (CinemachineCamera camera in cameras)
        {
            if (camera.gameObject.scene == scene)
                camera.Follow = player.transform;
        }

        DialogueController2D[] dialogues =
            Object.FindObjectsByType<DialogueController2D>(
                FindObjectsInactive.Include);
        foreach (DialogueController2D dialogue in dialogues)
        {
            if (dialogue.gameObject.scene == scene)
                dialogue.BindPlayer(movement, player);
        }
    }

    private static void ClearPendingArrival()
    {
        pendingSceneName = string.Empty;
        pendingSpawnId = string.Empty;
        hasPendingArrival = false;
    }
}
