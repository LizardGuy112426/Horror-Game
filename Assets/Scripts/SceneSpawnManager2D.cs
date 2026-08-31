using UnityEngine;
using UnityEngine.SceneManagement;

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
        if (!hasPendingArrival)
            return;

        string requestedScene = pendingSceneName;
        string requestedSpawn = pendingSpawnId;
        ClearPendingArrival();

        if (!string.Equals(scene.name, requestedScene, System.StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"Door arrival expected scene '{requestedScene}', but '{scene.name}' loaded. "
                + "The arrival request was discarded.");
            return;
        }

        SceneSpawnPoint2D destination = FindSpawnPoint(scene, requestedSpawn);
        if (destination == null)
        {
            Debug.LogWarning(
                $"Scene '{scene.name}' has no Scene Spawn Point 2D with Spawn ID "
                + $"'{requestedSpawn}'. The Player kept its Scene-authored position.");
            return;
        }

        PlayerDoorInteractor2D player = FindPlayer(scene);
        if (player == null)
        {
            Debug.LogWarning(
                $"Scene '{scene.name}' has no PlayerDoorInteractor2D. "
                + $"Could not use Spawn ID '{requestedSpawn}'.");
            return;
        }

        Debug.Log(
            $"Teleporting Player to Spawn '{requestedSpawn}' " +
            $"at position {destination.transform.position}"
        );

        player.TeleportTo(destination.transform.position);
        Debug.Log(
            $"Door arrival placed Player at '{requestedSpawn}' in scene '{scene.name}'.",
            destination);
    }

    private static SceneSpawnPoint2D FindSpawnPoint(Scene scene, string spawnId)
    {
        SceneSpawnPoint2D match = null;
        SceneSpawnPoint2D[] points = Object.FindObjectsByType<SceneSpawnPoint2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

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
        PlayerDoorInteractor2D[] players = Object.FindObjectsByType<PlayerDoorInteractor2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (PlayerDoorInteractor2D player in players)
        {
            if (player.gameObject.activeInHierarchy)
                return player;
        }

        return null;
    }

    private static void ClearPendingArrival()
    {
        pendingSceneName = string.Empty;
        pendingSpawnId = string.Empty;
        hasPendingArrival = false;
    }
}
