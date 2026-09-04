using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent, typed controller for the first parents story task.</summary>
[DefaultExecutionOrder(-900)]
public sealed class StoryTaskController : MonoBehaviour
{
    private const string LivingRoomScene = "Happy_LivingRoom";
    private const string KitchenScene = "Happy_Kitchen";

    public static StoryTaskController Instance { get; private set; }
    public StoryTaskStage CurrentStage => currentStage;
    public event Action<StoryTaskStage> StageChanged;

    [Header("Current Story State")]
    [SerializeField] private StoryTaskStage currentStage = StoryTaskStage.GoToKitchen;

    [Header("Persistent Task HUD")]
    [SerializeField] private GameObject taskHud;
    [SerializeField] private Text taskText;
    [SerializeField] private Image taskIcon;
    [SerializeField] private Sprite incompleteIcon = null;
    [SerializeField] private Sprite completeIcon = null;
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color completedTextColor = Color.gray;

    [Header("Editable Task Text")]
    [SerializeField] private string goToKitchenText = "前往厨房";
    [SerializeField] private string talkInKitchenText = "和父母对话";
    [SerializeField] private string returnToLivingRoomText = "返回客厅";
    [SerializeField] private string talkInLivingRoomText = "和父母对话";
    [SerializeField] private string completedText = "任务完成";

    [Header("Final Transition")]
    [SerializeField] private StoryFadeTransition2D finalTransition;

    public void Configure(
        GameObject hud,
        Text label,
        Image icon,
        StoryFadeTransition2D transition)
    {
        taskHud = hud;
        taskText = label;
        taskIcon = icon;
        finalTransition = transition;
        RefreshTaskHud();
    }

    public void AdvanceAfterDialogue(StoryTaskStage dialogueStage)
    {
        if (dialogueStage != currentStage)
        {
            Debug.LogWarning(
                $"Ignored dialogue completion for {dialogueStage}; current task is {currentStage}.",
                this);
            return;
        }

        switch (dialogueStage)
        {
            case StoryTaskStage.TalkToParentsInKitchen:
                SetStage(StoryTaskStage.ReturnToLivingRoom);
                break;
            case StoryTaskStage.TalkToParentsInLivingRoom:
                SetStage(StoryTaskStage.Completed);
                if (taskHud != null)
                    taskHud.SetActive(false);
                if (finalTransition != null)
                    finalTransition.FadeToTargetScene();
                else
                    Debug.LogWarning("Story Task Controller has no Final Transition.", this);
                break;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        EvaluateScene(SceneManager.GetActiveScene());
        RefreshTaskHud();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EvaluateScene(scene);
    }

    private void EvaluateScene(Scene scene)
    {
        if (currentStage == StoryTaskStage.GoToKitchen && scene.name == KitchenScene)
            SetStage(StoryTaskStage.TalkToParentsInKitchen);
        else if (currentStage == StoryTaskStage.ReturnToLivingRoom && scene.name == LivingRoomScene)
            SetStage(StoryTaskStage.TalkToParentsInLivingRoom);
        else
            RefreshTaskHud();
    }

    private void SetStage(StoryTaskStage stage)
    {
        if (currentStage == stage)
            return;

        currentStage = stage;
        if (currentStage == StoryTaskStage.TalkToParentsInLivingRoom)
            StoryTaskRuntimeBootstrap.EnsureAndSynchronizeParents(SceneManager.GetActiveScene());

        RefreshTaskHud();
        StageChanged?.Invoke(currentStage);
    }

    private void RefreshTaskHud()
    {
        bool completed = currentStage == StoryTaskStage.Completed;
        if (taskHud != null)
            taskHud.SetActive(!completed);

        if (taskText != null)
        {
            taskText.text = GetTaskText(currentStage);
            taskText.color = completed ? completedTextColor : normalTextColor;
        }

        if (taskIcon != null)
        {
            Sprite requestedIcon = completed ? completeIcon : incompleteIcon;
            if (requestedIcon != null)
                taskIcon.sprite = requestedIcon;
        }
    }

    private string GetTaskText(StoryTaskStage stage)
    {
        return stage switch
        {
            StoryTaskStage.GoToKitchen => goToKitchenText,
            StoryTaskStage.TalkToParentsInKitchen => talkInKitchenText,
            StoryTaskStage.ReturnToLivingRoom => returnToLivingRoomText,
            StoryTaskStage.TalkToParentsInLivingRoom => talkInLivingRoomText,
            StoryTaskStage.Completed => completedText,
            _ => string.Empty
        };
    }

}

/// <summary>Loads the Inspector-authored persistent task prefab in gameplay scenes.</summary>
public static class StoryTaskRuntimeBootstrap
{
    private const string ResourceName = "StoryTaskSystem";
    private const string KitchenParentsResource = "ParentsNPC_Kitchen";
    private const string LivingParentsResource = "ParentsNPC_LivingRoom";
    private static bool isHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        isHooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookSceneLoading()
    {
        if (isHooked)
            return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        isHooked = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.StartsWith("NM_", StringComparison.Ordinal))
        {
            RemoveTaskSystem();
            return;
        }

        if (!scene.name.StartsWith("Happy_", StringComparison.Ordinal))
            return;

        EnsureTaskSystem();
        if (scene.name == "Happy_Kitchen")
            EnsureAndSynchronizeParents(scene);
    }

    private static void RemoveTaskSystem()
    {
        StoryTaskController controller = StoryTaskController.Instance;
        if (controller == null)
            return;

        // Hide the inherited HUD before Unity destroys the persistent root at frame end.
        controller.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(controller.gameObject);
    }

    private static void EnsureTaskSystem()
    {
        if (StoryTaskController.Instance != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(ResourceName);
        if (prefab == null)
        {
            Debug.LogWarning(
                "StoryTaskSystem prefab is missing from Assets/Resources. Run Tools > Horror Game > Setup First Parents Story Task.");
            return;
        }

        UnityEngine.Object.Instantiate(prefab);
    }

    public static void EnsureAndSynchronizeParents(Scene scene)
    {
        ParentsNPC2D[] parentsInLoadedScenes = UnityEngine.Object.FindObjectsByType<ParentsNPC2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (ParentsNPC2D parents in parentsInLoadedScenes)
        {
            if (parents != null && parents.gameObject.scene == scene)
            {
                parents.SynchronizeWithCurrentStage();
                return;
            }
        }

        string resourceName = scene.name switch
        {
            "Happy_Kitchen" => KitchenParentsResource,
            "Happy_LivingRoom" => LivingParentsResource,
            _ => null
        };
        if (string.IsNullOrEmpty(resourceName))
            return;

        GameObject prefab = Resources.Load<GameObject>(resourceName);
        if (prefab != null)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.GetComponent<ParentsNPC2D>()?.SynchronizeWithCurrentStage();
        }
        else
            Debug.LogWarning($"Missing Resources/{resourceName}.prefab.");
    }

}
