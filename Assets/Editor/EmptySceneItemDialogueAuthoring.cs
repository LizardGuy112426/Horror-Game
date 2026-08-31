using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time authoring helper for the user's in-scene item and editable dialogue UI.</summary>
public static class EmptySceneItemDialogueAuthoring
{
    private const string DialogueFontPath = "Assets/Image/UI/AaWeiWeiDianZhenTi-2.ttf";

    [MenuItem("Tools/Horror Game/Setup Happy Living Room Family Photo")]
    public static void ConfigureHappyLivingRoomFamilyPhoto()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || scene.name != "Happy_LivingRoom")
        {
            Debug.LogWarning("Open Happy_LivingRoom before setting up the Family Photo dialogue.");
            return;
        }

        GameObject familyPhoto = FindGameObject(scene, "Family Photo");
        GameObject player = FindGameObject(scene, "MC");
        if (familyPhoto == null || player == null)
        {
            Debug.LogWarning("Family Photo dialogue setup needs both 'Family Photo' and 'MC' in Happy_LivingRoom.");
            return;
        }

        MCControllers movement = player.GetComponent<MCControllers>();
        PlayerDoorInteractor2D interaction = player.GetComponent<PlayerDoorInteractor2D>();
        if (movement == null || interaction == null)
        {
            Debug.LogWarning("MC needs MCController and PlayerDoorInteractor2D before setting up dialogue.");
            return;
        }

        DialogueController2D dialogueController = CreateDialogueCanvas(scene, movement, interaction);
        ConfigureItem(familyPhoto, dialogueController);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Horror Game/Setup Active Item Dialogue")]
    public static void TryConfigureLoadedEmptyScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!scene.isLoaded || scene.name != "EmptyScene")
                continue;

            GameObject item = FindGameObject(scene, "item");
            if (item == null)
                continue;

            if (item.GetComponent<ItemDialogueInteractable2D>() != null
                && FindComponent<DialogueController2D>(scene, "Dialogue Canvas") != null)
                return;

            ConfigureScene(scene, item);
            EditorSceneManager.SaveScene(scene);
            return;
        }
    }

    private static void ConfigureScene(Scene scene, GameObject item)
    {
        GameObject player = FindGameObject(scene, "Player");
        if (player == null)
        {
            Debug.LogWarning("Item dialogue setup needs a Player object in EmptyScene.");
            return;
        }

        SimplePlayer2D movement = GetOrAdd<SimplePlayer2D>(player);
        PlayerDoorInteractor2D interaction = GetOrAdd<PlayerDoorInteractor2D>(player);
        DialogueController2D dialogueController = CreateDialogueCanvas(scene, movement, interaction);
        ConfigureItem(item, dialogueController);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static DialogueController2D CreateDialogueCanvas(
        Scene scene,
        Component movement,
        PlayerDoorInteractor2D interaction)
    {
        GameObject canvasObject = FindGameObject(scene, "Dialogue Canvas");
        bool canvasCreated = canvasObject == null;
        if (canvasCreated)
        {
            canvasObject = new GameObject(
                "Dialogue Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
        }

        Canvas canvas = GetOrAdd<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>(canvasObject);
        DialogueController2D controller = GetOrAdd<DialogueController2D>(canvasObject);

        RectTransform dialogueRoot = GetOrCreateRectChild(
            canvasObject.transform,
            "Dialogue UI",
            out bool dialogueRootCreated);
        if (dialogueRootCreated)
            StretchFullScreen(dialogueRoot);

        Image nameBackground = CreatePanel(
            dialogueRoot,
            "Name Background",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(135f, 260f),
            new Vector2(460f, 72f),
            new Color(0.12f, 0.09f, 0.16f, 0.96f));
        Text nameText = CreateText(
            nameBackground.transform,
            "Name Text",
            28,
            TextAnchor.MiddleLeft,
            new Color(1f, 0.86f, 0.65f));

        Image contentBackground = CreatePanel(
            dialogueRoot,
            "Content Background",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 30f),
            new Vector2(1660f, 220f),
            new Color(0.025f, 0.025f, 0.04f, 0.96f));
        Text contentText = CreateText(
            contentBackground.transform,
            "Content Text",
            28,
            TextAnchor.UpperLeft,
            Color.white);
        RectTransform contentRect = contentText.rectTransform;
        contentRect.offsetMin = new Vector2(45f, 45f);
        contentRect.offsetMax = new Vector2(-45f, -28f);

        Text hintText = CreateText(
            contentBackground.transform,
            "Continue Hint",
            17,
            TextAnchor.LowerRight,
            new Color(0.72f, 0.72f, 0.78f));
        RectTransform hintRect = hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0.55f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-35f, 12f);
        hintRect.sizeDelta = new Vector2(700f, 30f);

        if (movement is MCControllers mcMovement)
        {
            controller.Configure(
                dialogueRoot.gameObject,
                nameBackground,
                nameText,
                contentBackground,
                contentText,
                hintText,
                mcMovement,
                interaction);
        }
        else
        {
            controller.Configure(
                dialogueRoot.gameObject,
                nameBackground,
                nameText,
                contentBackground,
                contentText,
                hintText,
                movement as SimplePlayer2D,
                interaction);
        }
        dialogueRoot.gameObject.SetActive(false);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ConfigureItem(GameObject itemObject, DialogueController2D controller)
    {
        ItemDialogueInteractable2D item = itemObject.GetComponent<ItemDialogueInteractable2D>();
        bool itemCreated = item == null;
        if (itemCreated)
            item = itemObject.AddComponent<ItemDialogueInteractable2D>();

        if (itemCreated)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty repeatable = serializedItem.FindProperty("repeatable");
            SerializedProperty lines = serializedItem.FindProperty("dialogueLines");
            repeatable.boolValue = true;
            lines.arraySize = 1;
            SerializedProperty firstLine = lines.GetArrayElementAtIndex(0);
            firstLine.FindPropertyRelative("speakerName").stringValue = "主角";
            firstLine.FindPropertyRelative("dialogue").stringValue = "咳咳咳";
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
        }

        SpriteRenderer renderer = itemObject.GetComponent<SpriteRenderer>();
        Vector2 visualSize = renderer != null && renderer.sprite != null
            ? renderer.sprite.bounds.size
            : Vector2.one;

        Transform triggerTransform = GetOrCreateChild(itemObject.transform, "Item Interaction Trigger", out bool triggerCreated);
        BoxCollider2D trigger = GetOrAdd<BoxCollider2D>(triggerTransform.gameObject);
        trigger.isTrigger = true;
        if (triggerCreated)
        {
            triggerTransform.localPosition = Vector3.zero;
            trigger.size = new Vector2(
                Mathf.Max(2.5f, visualSize.x + 1.5f),
                Mathf.Max(1.8f, visualSize.y + 0.8f));
        }
        ItemInteractionTrigger2D relay = GetOrAdd<ItemInteractionTrigger2D>(triggerTransform.gameObject);

        GameObject prompt = CreateWorldPrompt(itemObject.transform, visualSize.y);
        relay.Configure(item);
        item.Configure(trigger, prompt, controller);
        EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(relay);
    }

    private static GameObject CreateWorldPrompt(Transform parent, float visualHeight)
    {
        RectTransform promptRect = GetOrCreateRectChild(parent, "Item E Prompt", out bool promptCreated);
        Canvas promptCanvas = GetOrAdd<Canvas>(promptRect.gameObject);
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 50;
        GetOrAdd<GraphicRaycaster>(promptRect.gameObject);
        if (promptCreated)
        {
            promptRect.localPosition = new Vector3(0f, visualHeight * 0.5f + 0.8f, 0f);
            promptRect.localScale = Vector3.one * 0.01f;
            promptRect.sizeDelta = new Vector2(120f, 60f);
        }

        Text promptText = CreateText(promptRect, "E Text", 42, TextAnchor.MiddleCenter, Color.white);
        promptText.text = "[E]";
        promptRect.gameObject.SetActive(false);
        return promptRect.gameObject;
    }

    private static Image CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        RectTransform rect = GetOrCreateRectChild(parent, name, out bool created);
        Image image = GetOrAdd<Image>(rect.gameObject);
        if (created)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            image.color = color;
        }
        return image;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        RectTransform rect = GetOrCreateRectChild(parent, name, out bool created);
        Graphic existingGraphic = rect.GetComponent<Graphic>();
        if (HasTextMeshProComponent(rect.gameObject)
            || (existingGraphic != null && existingGraphic is not Text))
            rect = ReplaceTextObject(rect, parent, name);

        Text text = GetOrAdd<Text>(rect.gameObject);
        if (created)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(25f, 8f);
            rect.offsetMax = new Vector2(-25f, -8f);
        }

        text.font = AssetDatabase.LoadAssetAtPath<Font>(DialogueFontPath)
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static bool HasTextMeshProComponent(GameObject gameObject)
    {
        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (component?.GetType().Namespace == "TMPro")
                return true;
        }

        return false;
    }

    private static RectTransform ReplaceTextObject(RectTransform original, Transform parent, string name)
    {
        Vector2 anchorMin = original.anchorMin;
        Vector2 anchorMax = original.anchorMax;
        Vector2 anchoredPosition = original.anchoredPosition;
        Vector2 sizeDelta = original.sizeDelta;
        Vector2 pivot = original.pivot;
        Vector3 localScale = original.localScale;
        Quaternion localRotation = original.localRotation;

        Object.DestroyImmediate(original.gameObject);

        RectTransform replacement = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        replacement.SetParent(parent, false);
        replacement.anchorMin = anchorMin;
        replacement.anchorMax = anchorMax;
        replacement.anchoredPosition = anchoredPosition;
        replacement.sizeDelta = sizeDelta;
        replacement.pivot = pivot;
        replacement.localScale = localScale;
        replacement.localRotation = localRotation;
        return replacement;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Transform GetOrCreateChild(Transform parent, string name, out bool created)
    {
        Transform child = parent.Find(name);
        created = child == null;
        if (created)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }
        return child;
    }

    private static RectTransform GetOrCreateRectChild(Transform parent, string name, out bool created)
    {
        Transform existing = parent.Find(name);
        created = existing == null;
        if (!created)
            return existing as RectTransform ?? existing.gameObject.AddComponent<RectTransform>();

        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static T FindComponent<T>(Scene scene, string objectName) where T : Component
    {
        GameObject gameObject = FindGameObject(scene, objectName);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                    return candidate.gameObject;
            }
        }
        return null;
    }
}
