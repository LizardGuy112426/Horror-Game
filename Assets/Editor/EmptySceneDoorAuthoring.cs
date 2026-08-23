using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Creates the editable scene hierarchy and reusable door prefab once.</summary>
public static class EmptySceneDoorAuthoring
{
    private const string ScenePath = "Assets/Scenes/EmptyScene.unity";
    private const string PrefabFolder = "Assets/0822/Prefabs";
    private const string PrefabPath = PrefabFolder + "/InteractiveDoor.prefab";

    [MenuItem("Tools/Horror Game/Setup EmptyScene Door")]
    public static void EnsureProjectSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;

        EnsurePrefabExists();

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool closeWhenFinished = !scene.IsValid() || !scene.isLoaded;
        if (closeWhenFinished)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        bool changed = ConfigureScene(scene);
        if (changed)
            EditorSceneManager.SaveScene(scene);

        if (closeWhenFinished)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static bool ConfigureScene(Scene scene)
    {
        Tilemap home = FindInScene<Tilemap>(scene, "Home");
        Tilemap doorArt = FindInScene<Tilemap>(scene, "Door");
        GameObject player = FindGameObject(scene, "Player");
        EmptySceneSetup setup = FindInScene<EmptySceneSetup>(scene, "Empty Scene Setup");

        if (home == null || doorArt == null || player == null)
        {
            Debug.LogWarning("EmptyScene door setup needs objects named Home, Door, and Player.");
            return false;
        }

        bool changed = false;
        if (home.GetComponent<TilemapCollider2D>() == null)
        {
            home.gameObject.AddComponent<TilemapCollider2D>();
            changed = true;
        }

        bool bodyCreated = player.GetComponent<Rigidbody2D>() == null;
        Rigidbody2D body = GetOrAdd<Rigidbody2D>(player, ref changed);
        if (bodyCreated)
            body.gravityScale = 3f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        GetOrAdd<BoxCollider2D>(player, ref changed);
        GetOrAdd<SimplePlayer2D>(player, ref changed);
        GetOrAdd<PlayerDoorInteractor2D>(player, ref changed);

        if (setup != null)
        {
            SerializedObject serializedSetup = new SerializedObject(setup);
            serializedSetup.FindProperty("homeTilemap").objectReferenceValue = home;
            serializedSetup.FindProperty("playerObject").objectReferenceValue = player;
            changed |= serializedSetup.ApplyModifiedPropertiesWithoutUndo();
        }

        changed |= ConfigureDoorHierarchy(doorArt.gameObject, doorArt.localBounds, player.transform.position.y);
        return changed;
    }

    private static bool ConfigureDoorHierarchy(GameObject root, Bounds localBounds, float playerWorldY)
    {
        bool changed = false;
        DoorTransition2D door = GetOrAdd<DoorTransition2D>(root, ref changed);
        Vector3 center = localBounds.center;
        float height = Mathf.Max(1.6f, localBounds.size.y);
        float barrierWidth = Mathf.Clamp(localBounds.size.x * 0.35f, 0.25f, 0.6f);
        float playerLocalY = root.transform.InverseTransformPoint(new Vector3(0f, playerWorldY, 0f)).y;

        Transform barrierTransform = GetOrCreateChild(root.transform, "Barrier Collider", ref changed, out bool barrierCreated);
        BoxCollider2D barrier = GetOrAdd<BoxCollider2D>(barrierTransform.gameObject, ref changed);
        if (barrierCreated)
        {
            barrierTransform.localPosition = center;
            barrier.size = new Vector2(barrierWidth, height);
        }
        barrier.isTrigger = false;

        Transform triggerTransform = GetOrCreateChild(root.transform, "Interaction Trigger", ref changed, out bool triggerCreated);
        BoxCollider2D trigger = GetOrAdd<BoxCollider2D>(triggerTransform.gameObject, ref changed);
        DoorInteractionTrigger2D relay = GetOrAdd<DoorInteractionTrigger2D>(triggerTransform.gameObject, ref changed);
        if (triggerCreated)
        {
            triggerTransform.localPosition = center;
            trigger.size = new Vector2(Mathf.Max(2.6f, localBounds.size.x + 1.6f), height);
        }
        trigger.isTrigger = true;

        Transform leftExit = GetOrCreateChild(root.transform, "Left Exit", ref changed, out bool leftCreated);
        Transform rightExit = GetOrCreateChild(root.transform, "Right Exit", ref changed, out bool rightCreated);
        float exitOffset = trigger.size.x * 0.5f + 0.55f;
        if (leftCreated)
            leftExit.localPosition = new Vector3(center.x - exitOffset, playerLocalY, 0f);
        if (rightCreated)
            rightExit.localPosition = new Vector3(center.x + exitOffset, playerLocalY, 0f);

        Transform promptTransform = GetOrCreateChild(root.transform, "E Prompt", ref changed, out bool promptCreated);
        TextMesh promptText = GetOrAdd<TextMesh>(promptTransform.gameObject, ref changed);
        if (promptCreated)
        {
            promptTransform.localPosition = new Vector3(center.x, playerLocalY + 1.4f, 0f);
            promptText.text = "[E]";
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.characterSize = 0.15f;
            promptText.fontSize = 48;
            promptText.color = Color.white;
        }

        relay.Configure(door);
        door.Configure(barrier, trigger, leftExit, rightExit, promptTransform.gameObject);
        EditorUtility.SetDirty(door);
        EditorUtility.SetDirty(relay);
        return changed;
    }

    private static void EnsurePrefabExists()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/0822", "Prefabs");

        GameObject root = new GameObject("InteractiveDoor");
        try
        {
            ConfigureDoorHierarchy(root, new Bounds(Vector3.zero, new Vector3(1f, 3f, 0f)), 0f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform GetOrCreateChild(Transform parent, string childName, ref bool changed, out bool created)
    {
        Transform child = parent.Find(childName);
        created = child == null;
        if (created)
        {
            child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            changed = true;
        }
        return child;
    }

    private static T GetOrAdd<T>(GameObject gameObject, ref bool changed) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
            changed = true;
        }
        return component;
    }

    private static T FindInScene<T>(Scene scene, string objectName) where T : Component
    {
        GameObject gameObject = FindGameObject(scene, objectName);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }
        }
        return null;
    }
}
