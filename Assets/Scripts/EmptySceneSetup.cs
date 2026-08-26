using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Scene-safe setup for the hand-drawn EmptyScene. Save tilemaps as Home and Door,
/// then the required player, floor collision, and Door interaction are assembled at play time.
/// </summary>
public sealed class EmptySceneSetup : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Tilemap homeTilemap;
    [SerializeField] private GameObject playerObject;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForEmptyScene()
    {
        if (SceneManager.GetActiveScene().name == "EmptyScene" && FindAnyObjectByType<EmptySceneSetup>() == null)
            new GameObject("Empty Scene Setup").AddComponent<EmptySceneSetup>();
    }

    private void Start()
    {
        Tilemap home = homeTilemap != null ? homeTilemap : FindTilemap("Home");
        ConfigureFloorCollision(home);
        ConfigureOrCreatePlayer(home);
    }

    private static Tilemap FindTilemap(string objectName)
    {
        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            if (tilemap.gameObject.name == objectName)
                return tilemap;
        }

        Debug.LogWarning($"EmptyScene setup: no Tilemap named '{objectName}' was found.");
        return null;
    }

    private static void ConfigureFloorCollision(Tilemap background)
    {
        if (background == null)
            return;

        if (background.GetComponent<TilemapCollider2D>() == null)
            background.gameObject.AddComponent<TilemapCollider2D>();
    }

    private void ConfigureOrCreatePlayer(Tilemap background)
    {
        GameObject player = playerObject != null ? playerObject : GameObject.Find("Player");
        if (player != null)
        {
            ConfigurePlayerComponents(player);
            return;
        }

        Vector3 spawn = Vector3.zero;
        if (background != null)
        {
            Bounds backgroundBounds = background.localBounds;
            spawn = background.transform.TransformPoint(new Vector3(backgroundBounds.min.x + 1f, backgroundBounds.center.y, 0f));
        }

        player = new GameObject("Player", typeof(SpriteRenderer));
        player.transform.position = spawn;
        player.transform.localScale = new Vector3(0.6f, 1.1f, 1f);

        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        renderer.color = new Color(0.9f, 0.8f, 0.7f);
        renderer.sortingOrder = 10;

        ConfigurePlayerComponents(player);
    }

    private void ConfigurePlayerComponents(GameObject player)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            player.layer = playerLayer;

        if (player.GetComponent<BoxCollider2D>() == null)
            player.AddComponent<BoxCollider2D>();

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body == null)
            body = player.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (player.GetComponent<SimplePlayer2D>() == null)
            player.AddComponent<SimplePlayer2D>();
        if (player.GetComponent<PlayerDoorInteractor2D>() == null)
            player.AddComponent<PlayerDoorInteractor2D>();
    }
}
