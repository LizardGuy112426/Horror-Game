using UnityEngine;

/// <summary>A named, Scene-local arrival position that can be moved in the Scene view.</summary>
[DisallowMultipleComponent]
public sealed class SceneSpawnPoint2D : MonoBehaviour
{
    [Tooltip("Must exactly match a door's Target Spawn ID.")]
    [SerializeField] private string spawnId = string.Empty;

    public string SpawnId => spawnId;

    public void Configure(string id)
    {
        spawnId = id == null ? string.Empty : id.Trim();
    }

    private void OnValidate()
    {
        spawnId = spawnId == null ? string.Empty : spawnId.Trim();
    }

    private void OnDrawGizmos()
    {
        Vector3 position = transform.position;
        Gizmos.color = new Color(0.15f, 1f, 0.45f, 0.95f);
        Gizmos.DrawWireSphere(position, 0.2f);
        Gizmos.DrawLine(position, position + Vector3.up * 0.55f);
        Gizmos.DrawCube(position + Vector3.up * 0.55f, Vector3.one * 0.1f);
    }
}
