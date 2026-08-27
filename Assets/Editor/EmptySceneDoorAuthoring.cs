using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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

        changed |= ConfigureDoorHierarchy(doorArt.gameObject, doorArt.localBounds);
        return changed;
    }

    private static bool ConfigureDoorHierarchy(GameObject root, Bounds localBounds)
    {
        bool changed = false;
        DoorTransition2D door = GetOrAdd<DoorTransition2D>(root, ref changed);
        Vector3 center = localBounds.center;
        float height = Mathf.Max(1.6f, localBounds.size.y);
        float barrierWidth = Mathf.Clamp(localBounds.size.x * 0.35f, 0.25f, 0.6f);
        float promptLocalY = localBounds.min.y + 1.4f;

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

        RectTransform promptTransform = GetOrCreatePromptCanvas(root.transform, ref changed, out bool promptCreated);
        if (promptCreated)
        {
            promptTransform.localPosition = new Vector3(center.x, promptLocalY, 0f);
            promptTransform.localScale = Vector3.one * 0.01f;
            promptTransform.sizeDelta = new Vector2(120f, 60f);
        }

        Canvas promptCanvas = GetOrAdd<Canvas>(promptTransform.gameObject, ref changed);
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 50;

        RectTransform imageTransform = GetOrCreateRectChild(promptTransform, "F Image", ref changed, out _);
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;

        Image promptImage = GetOrAdd<Image>(imageTransform.gameObject, ref changed);
        promptImage.raycastTarget = false;
        promptImage.preserveAspect = true;
        promptTransform.gameObject.SetActive(false);

        relay.Configure(door);
        door.Configure(barrier, trigger, promptTransform.gameObject);
        EditorUtility.SetDirty(door);
        EditorUtility.SetDirty(relay);
        return changed;
    }

    private static RectTransform GetOrCreatePromptCanvas(Transform parent, ref bool changed, out bool created)
    {
        Transform existing = parent.Find("F Prompt") ?? parent.Find("E Prompt");
        created = existing == null;

        if (existing != null && existing is not RectTransform)
        {
            Vector3 localPosition = existing.localPosition;
            Object.DestroyImmediate(existing.gameObject);
            existing = null;
            created = true;
            changed = true;

            RectTransform replacement = new GameObject("F Prompt", typeof(RectTransform)).GetComponent<RectTransform>();
            replacement.SetParent(parent, false);
            replacement.localPosition = localPosition;
            return replacement;
        }

        if (existing == null)
        {
            RectTransform prompt = new GameObject("F Prompt", typeof(RectTransform)).GetComponent<RectTransform>();
            prompt.SetParent(parent, false);
            changed = true;
            return prompt;
        }

        if (existing.name != "F Prompt")
        {
            existing.name = "F Prompt";
            changed = true;
        }

        return (RectTransform)existing;
    }

    private static RectTransform GetOrCreateRectChild(
        Transform parent,
        string childName,
        ref bool changed,
        out bool created)
    {
        Transform existing = parent.Find(childName);
        created = existing == null;
        if (existing is RectTransform rectTransform)
            return rectTransform;

        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        RectTransform child = new GameObject(childName, typeof(RectTransform)).GetComponent<RectTransform>();
        child.SetParent(parent, false);
        changed = true;
        return child;
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
            ConfigureDoorHierarchy(root, new Bounds(Vector3.zero, new Vector3(1f, 3f, 0f)));
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
