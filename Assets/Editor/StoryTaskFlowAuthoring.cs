using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the reusable task HUD and runtime Parents NPC prefabs.</summary>
[InitializeOnLoad]
public static class StoryTaskFlowAuthoring
{
    private const string StoryPrefabPath = "Assets/Resources/StoryTaskSystem.prefab";
    private const string ParentsPrefabPath = "Assets/Prefeb/ParentsNPC.prefab";
    private const string KitchenParentsPrefabPath = "Assets/Resources/ParentsNPC_Kitchen.prefab";
    private const string LivingParentsPrefabPath = "Assets/Resources/ParentsNPC_LivingRoom.prefab";
    private const string LivingRoomPath = "Assets/Scenes/Happy/Happy_LivingRoom.unity";
    private const string KitchenPath = "Assets/Scenes/Happy/Happy_Kitchen.unity";
    private const string DialogueFontPath = "Assets/Image/UI/AaWeiWeiDianZhenTi-2.ttf";
    private const string EPromptPath = "Assets/Image/UI/EBUTTON.png";
    private const string LegacyGuidePrefabPath = "Assets/Prefeb/Canvas.prefab";

    static StoryTaskFlowAuthoring()
    {
        AutoSetupIfNeeded();
    }

    private static void AutoSetupIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || BuildPipeline.isBuildingPlayer
            || AssetDatabase.LoadAssetAtPath<GameObject>(StoryPrefabPath) != null)
            return;

        Setup();
    }

    [MenuItem("Tools/Horror Game/Setup First Parents Story Task")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;

        EnsureFolder("Assets", "Resources");
        GameObject storyPrefab = CreateStoryTaskPrefab();
        GameObject parentsPrefab = CreateParentsPrefab();
        CreateParentsVariant(
            parentsPrefab,
            KitchenParentsPrefabPath,
            StoryTaskStage.TalkToParentsInKitchen,
            "Parents - Kitchen",
            new Vector3(0f, -2f, 0f),
            "父母",
            "你来了。先回客厅等我们吧。");
        CreateParentsVariant(
            parentsPrefab,
            LivingParentsPrefabPath,
            StoryTaskStage.TalkToParentsInLivingRoom,
            "Parents - Living Room",
            new Vector3(8f, -2f, 0f),
            "父母",
            "你终于回来了，我们有些话想和你说。");
        EditorUtility.SetDirty(storyPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "First parents story task is ready: StoryTaskSystem and runtime Parents NPC prefabs.");
    }

    [MenuItem("Tools/Horror Game/Validate First Parents Story Task")]
    public static void ValidateGeneratedAssets()
    {
        GameObject story = RequirePrefab(StoryPrefabPath);
        GameObject parents = RequirePrefab(ParentsPrefabPath);
        GameObject kitchen = RequirePrefab(KitchenParentsPrefabPath);
        GameObject living = RequirePrefab(LivingParentsPrefabPath);

        StoryTaskController controller = story.GetComponent<StoryTaskController>();
        StoryFadeTransition2D transition = story.GetComponent<StoryFadeTransition2D>();
        if (controller == null || transition == null)
            throw new System.InvalidOperationException("StoryTaskSystem is missing its controller or fade transition.");
        StoryTaskHudAppearance2D appearance = story.GetComponentInChildren<StoryTaskHudAppearance2D>(true);
        if (appearance == null)
            throw new System.InvalidOperationException("StoryTaskSystem is missing Task HUD Appearance 2D.");

        SerializedObject appearanceData = new SerializedObject(appearance);
        if (appearanceData.FindProperty("backgroundImage").objectReferenceValue == null
            || appearanceData.FindProperty("taskIconImage").objectReferenceValue == null
            || appearanceData.FindProperty("taskLabel").objectReferenceValue == null)
            throw new System.InvalidOperationException("Task HUD Appearance Inspector references are incomplete.");

        SerializedObject storyData = new SerializedObject(controller);
        if (storyData.FindProperty("taskHud").objectReferenceValue == null
            || storyData.FindProperty("taskText").objectReferenceValue == null
            || storyData.FindProperty("finalTransition").objectReferenceValue == null)
            throw new System.InvalidOperationException("StoryTaskSystem HUD references are incomplete.");

        ParentsNPC2D baseParents = parents.GetComponent<ParentsNPC2D>();
        if (baseParents == null)
            throw new System.InvalidOperationException("ParentsNPC prefab has no ParentsNPC2D.");
        SerializedObject parentsData = new SerializedObject(baseParents);
        if (parentsData.FindProperty("interactionTrigger").objectReferenceValue == null
            || parentsData.FindProperty("ePrompt").objectReferenceValue == null
            || parentsData.FindProperty("spriteRenderer").objectReferenceValue == null
            || parentsData.FindProperty("animator").objectReferenceValue == null)
            throw new System.InvalidOperationException("ParentsNPC Inspector references are incomplete.");

        if (kitchen.GetComponent<ParentsNPC2D>().RequiredTaskStage
                != StoryTaskStage.TalkToParentsInKitchen
            || living.GetComponent<ParentsNPC2D>().RequiredTaskStage
                != StoryTaskStage.TalkToParentsInLivingRoom)
            throw new System.InvalidOperationException("Parents NPC stage variants are configured incorrectly.");

        string[] gameplayScenes =
        {
            LivingRoomPath,
            KitchenPath,
            "Assets/Scenes/Happy/Happy_Staircase.unity",
            "Assets/Scenes/Happy/Happy_Floor_2_Hallways.unity"
        };
        foreach (string scenePath in gameplayScenes)
            ValidateNoLegacyGuide(scenePath);

        Debug.Log("First parents story task validation passed.");
    }

    private static void ValidateNoLegacyGuide(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForValidation = !scene.isLoaded;
        if (openedForValidation)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "TaskController"
                || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == LegacyGuidePrefabPath)
                throw new System.InvalidOperationException($"Old task guide remains in {scene.name}.");
        }

        if (openedForValidation)
            EditorSceneManager.CloseScene(scene, true);
    }

    [MenuItem("Tools/Horror Game/Remove Old Task Guide UI")]
    public static void RemoveOldTaskGuideUi()
    {
        string[] scenePaths =
        {
            LivingRoomPath,
            KitchenPath,
            "Assets/Scenes/Happy/Happy_Staircase.unity",
            "Assets/Scenes/Happy/Happy_Floor_2_Hallways.unity"
        };

        foreach (string scenePath in scenePaths)
            RemoveLegacyGuideFromScene(scenePath);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyGuidePrefabPath) != null)
            AssetDatabase.DeleteAsset(LegacyGuidePrefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Removed the old task guide UI prefab and all of its gameplay scene instances.");
    }

    private static void RemoveLegacyGuideFromScene(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForCleanup = !scene.isLoaded;
        if (openedForCleanup)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(root);
            string prefabPath = instanceRoot != null
                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot)
                : string.Empty;
            if (prefabPath != LegacyGuidePrefabPath)
                continue;

            Object.DestroyImmediate(instanceRoot);
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (openedForCleanup)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static GameObject RequirePrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new System.InvalidOperationException($"Missing required prefab: {path}");
        return prefab;
    }

    private static GameObject CreateStoryTaskPrefab()
    {
        GameObject root = new GameObject(
            "StoryTaskSystem",
            typeof(StoryTaskController),
            typeof(StoryFadeTransition2D));

        GameObject canvasObject = new GameObject(
            "Story Task Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform hud = CreateRect(canvasObject.transform, "Task HUD");
        hud.anchorMin = new Vector2(0f, 1f);
        hud.anchorMax = new Vector2(0f, 1f);
        hud.pivot = new Vector2(0f, 1f);
        hud.anchoredPosition = new Vector2(30f, -55f);
        hud.sizeDelta = new Vector2(520f, 105f);
        Image hudBackground = hud.gameObject.AddComponent<Image>();
        hudBackground.color = new Color(0.035f, 0.03f, 0.05f, 0.82f);

        RectTransform iconRect = CreateRect(hud, "Task Icon");
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(18f, 0f);
        iconRect.sizeDelta = new Vector2(64f, 64f);
        Image taskIcon = iconRect.gameObject.AddComponent<Image>();
        taskIcon.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        taskIcon.color = new Color(0.9f, 0.74f, 0.35f, 1f);

        RectTransform textRect = CreateRect(hud, "Task Text");
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(100f, 10f);
        textRect.offsetMax = new Vector2(-20f, -10f);
        Text taskText = textRect.gameObject.AddComponent<Text>();
        taskText.font = LoadDialogueFont();
        taskText.fontSize = 32;
        taskText.alignment = TextAnchor.MiddleLeft;
        taskText.color = Color.white;
        taskText.text = "前往厨房";

        StoryTaskHudAppearance2D appearance = hud.gameObject.AddComponent<StoryTaskHudAppearance2D>();
        appearance.Configure(
            hudBackground,
            taskIcon,
            taskText,
            hudBackground.sprite,
            taskIcon.sprite,
            taskText.font);

        RectTransform overlayRect = CreateRect(canvasObject.transform, "Final Black Overlay");
        Stretch(overlayRect);
        Image overlay = overlayRect.gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);
        overlay.raycastTarget = false;
        overlayRect.gameObject.SetActive(false);
        overlayRect.SetAsLastSibling();

        StoryFadeTransition2D transition = root.GetComponent<StoryFadeTransition2D>();
        transition.Configure(overlay, 3f, "Cutscene2");
        StoryTaskController controller = root.GetComponent<StoryTaskController>();
        controller.Configure(hud.gameObject, taskText, taskIcon, transition);

        PrefabUtility.SaveAsPrefabAsset(root, StoryPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(StoryPrefabPath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(StoryPrefabPath);
    }

    private static GameObject CreateParentsPrefab()
    {
        GameObject root = new GameObject("ParentsNPC", typeof(ParentsNPC2D));
        GameObject content = new GameObject("Content Root");
        content.transform.SetParent(root.transform, false);

        GameObject visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator));
        visual.transform.SetParent(content.transform, false);
        visual.transform.localScale = new Vector3(1.25f, 2f, 1f);
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.color = new Color(0.83f, 0.76f, 0.7f, 1f);
        renderer.sortingOrder = 5;
        Animator animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = null;

        GameObject triggerObject = new GameObject(
            "Interaction Trigger",
            typeof(BoxCollider2D),
            typeof(ParentsInteractionTrigger2D));
        triggerObject.transform.SetParent(content.transform, false);
        BoxCollider2D trigger = triggerObject.GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(3.5f, 2.8f);

        GameObject prompt = new GameObject(
            "E Prompt",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        prompt.transform.SetParent(content.transform, false);
        RectTransform promptRect = prompt.GetComponent<RectTransform>();
        promptRect.localPosition = new Vector3(0f, 1.8f, 0f);
        promptRect.localScale = Vector3.one * 0.01f;
        promptRect.sizeDelta = new Vector2(110f, 70f);
        Canvas promptCanvas = prompt.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 100;

        RectTransform promptImageRect = CreateRect(prompt.transform, "E Image");
        Stretch(promptImageRect);
        Image promptImage = promptImageRect.gameObject.AddComponent<Image>();
        promptImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EPromptPath);
        promptImage.preserveAspect = true;
        prompt.SetActive(false);

        ParentsNPC2D parents = root.GetComponent<ParentsNPC2D>();
        parents.Configure(
            StoryTaskStage.TalkToParentsInKitchen,
            content,
            visual,
            renderer,
            animator,
            trigger,
            prompt);
        triggerObject.GetComponent<ParentsInteractionTrigger2D>().Configure(parents);

        PrefabUtility.SaveAsPrefabAsset(root, ParentsPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(ParentsPrefabPath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(ParentsPrefabPath);
    }

    private static void CreateParentsVariant(
        GameObject basePrefab,
        string assetPath,
        StoryTaskStage requiredStage,
        string objectName,
        Vector3 position,
        string speaker,
        string dialogue)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = objectName;
        instance.transform.position = position;
        ParentsNPC2D parents = instance.GetComponent<ParentsNPC2D>();
        parents.SetStoryConfiguration(
            requiredStage,
            new[]
            {
                new DialogueLine
                {
                    speakerName = speaker,
                    dialogue = dialogue
                }
            });
        PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
        Object.DestroyImmediate(instance);
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Font LoadDialogueFont()
    {
        return AssetDatabase.LoadAssetAtPath<Font>(DialogueFontPath)
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
