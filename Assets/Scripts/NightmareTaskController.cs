using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the Nightmare-only task state and its persistent HUD. This deliberately
/// remains separate from the Happy parents task system.
/// </summary>
[DefaultExecutionOrder(-850)]
public sealed class NightmareTaskController : MonoBehaviour
{
    public const string BedroomSceneName = "NM_Bedroom1";
    public const string VoidSceneName = "Void";
    public const string FloorTwoSceneName = "NM_Floor_2_Hallways";

    private static readonly NightmareObjectiveItem[] RequiredObjectives =
    {
        NightmareObjectiveItem.OwnClothes,
        NightmareObjectiveItem.IdentityCard,
        NightmareObjectiveItem.Wallet
    };

    [Serializable]
    private sealed class CollectedObjective
    {
        public NightmareObjectiveItem objective;
        public Sprite icon;
    }

    public static NightmareTaskController Instance { get; private set; }

    public NightmareTaskStage CurrentStage => currentStage;
    public int RunVersion => runVersion;
    public bool HasReachedFloorTwoThisRun { get; private set; }

    [Header("Task Text")]
    [SerializeField] private string findWayOutText = "Find a way out.";
    [SerializeField] private string collectEscapeItemsText = "Avoid monsters. Find the items. Escape.";
    [SerializeField] private string goToParentsBedroomForKeyText = "Find the front door key.";
    [SerializeField] private string goToFrontDoorText = "Go to the front door.";

    [Header("Prefab UI References")]
    [Tooltip("The child Canvas that contains the editable Nightmare task HUD.")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private Text taskText;
    [SerializeField] private GameObject objectiveRoot;
    [SerializeField] private Text[] objectiveTexts = new Text[RequiredObjectives.Length];
    [SerializeField] private Image[] inventoryIconImages = new Image[5];

    [Header("Runtime State")]
    [SerializeField] private NightmareTaskStage currentStage = NightmareTaskStage.FindWayOut;
    [SerializeField, HideInInspector] private List<CollectedObjective> collectedObjectives = new();

    private bool requestedHudVisible = true;
    private int runVersion;
    private bool reportedMissingUi;

    public static bool IsNightmareGameplayScene(string sceneName)
    {
        return string.Equals(sceneName, VoidSceneName, StringComparison.Ordinal)
            || (sceneName.StartsWith("NM_", StringComparison.Ordinal)
                && !string.Equals(sceneName, "NM_TESTING", StringComparison.Ordinal));
    }

    public bool IsObjectiveCollected(NightmareObjectiveItem objective)
    {
        return FindCollectedObjective(objective) != null;
    }

    public bool CanCollectObjective(NightmareObjectiveItem objective)
    {
        return IsRequiredObjective(objective)
            && currentStage == NightmareTaskStage.CollectEscapeItems
            && !IsObjectiveCollected(objective);
    }

    public bool TryCollectObjective(
        NightmareObjectiveItem objective,
        Sprite inventoryIcon,
        int expectedRunVersion)
    {
        if (expectedRunVersion != runVersion || !CanCollectObjective(objective))
            return false;

        collectedObjectives.Add(new CollectedObjective
        {
            objective = objective,
            icon = inventoryIcon
        });

        if (AreAllRequiredObjectivesCollected())
            currentStage = NightmareTaskStage.GoToParentsBedroomForKey;

        RefreshHud();
        return true;
    }

    /// <summary>Records the diary key in the next inventory slot and unlocks the front-door task.</summary>
    public bool TryCollectFrontDoorKey(Sprite inventoryIcon, int expectedRunVersion)
    {
        if (expectedRunVersion != runVersion
            || currentStage != NightmareTaskStage.GoToParentsBedroomForKey
            || IsObjectiveCollected(NightmareObjectiveItem.FrontDoorKey))
        {
            return false;
        }

        collectedObjectives.Add(new CollectedObjective
        {
            objective = NightmareObjectiveItem.FrontDoorKey,
            icon = inventoryIcon
        });
        currentStage = NightmareTaskStage.GoToFrontDoor;
        RefreshHud();
        return true;
    }

    /// <summary>Clears all Nightmare objective progress when a monster kills the player.</summary>
    public void ResetCurrentRun()
    {
        runVersion++;
        HasReachedFloorTwoThisRun = false;
        currentStage = NightmareTaskStage.FindWayOut;
        collectedObjectives.Clear();
        RefreshHud();
    }

    public void ShowHud()
    {
        requestedHudVisible = true;
        RefreshHud();
    }

    public void HideHud()
    {
        requestedHudVisible = false;
        RefreshHud();
    }

    internal void SynchronizeForScene(Scene scene, bool shouldShowHud)
    {
        if (scene.name == FloorTwoSceneName)
            HasReachedFloorTwoThisRun = true;

        if (scene.name == FloorTwoSceneName
            && currentStage == NightmareTaskStage.FindWayOut)
        {
            currentStage = NightmareTaskStage.CollectEscapeItems;
        }

        requestedHudVisible = shouldShowHud;
        RefreshHud();
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

        // Do not let the HUD render even for one frame behind Cutscene2's bedroom intro.
        requestedHudVisible = !(string.Equals(
            SceneManager.GetActiveScene().name,
            BedroomSceneName,
            StringComparison.Ordinal)
            && NightmareBedroomIntro2D.HasPendingCutsceneArrival);
        EnsureHud();
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private CollectedObjective FindCollectedObjective(NightmareObjectiveItem objective)
    {
        foreach (CollectedObjective collected in collectedObjectives)
        {
            if (collected != null && collected.objective == objective)
                return collected;
        }

        return null;
    }

    private bool AreAllRequiredObjectivesCollected()
    {
        foreach (NightmareObjectiveItem objective in RequiredObjectives)
        {
            if (!IsObjectiveCollected(objective))
                return false;
        }

        return true;
    }

    private static bool IsRequiredObjective(NightmareObjectiveItem objective)
    {
        foreach (NightmareObjectiveItem requiredObjective in RequiredObjectives)
        {
            if (objective == requiredObjective)
                return true;
        }

        return false;
    }

    private bool EnsureHud()
    {
        if (HasCompleteUi())
            return true;

        if (!reportedMissingUi)
        {
            Debug.LogWarning(
                "NightmareTaskSystem is missing one or more UI references. "
                + "Restore the editable children in Assets/Resources/NightmareTaskSystem.prefab.",
                this);
            reportedMissingUi = true;
        }

        return false;
    }

    private bool HasCompleteUi()
    {
        if (hudRoot == null || taskText == null || objectiveRoot == null
            || objectiveTexts == null || objectiveTexts.Length < RequiredObjectives.Length
            || inventoryIconImages == null || inventoryIconImages.Length < 5)
        {
            return false;
        }

        foreach (Text objectiveText in objectiveTexts)
        {
            if (objectiveText == null)
                return false;
        }

        for (int index = 0; index < 5; index++)
        {
            if (inventoryIconImages[index] == null)
                return false;
        }

        return true;
    }

    private void RefreshHud()
    {
        if (!EnsureHud())
            return;

        bool activeSceneIsNightmare = IsNightmareGameplayScene(SceneManager.GetActiveScene().name);
        if (hudRoot != null)
            hudRoot.SetActive(activeSceneIsNightmare && requestedHudVisible);

        if (taskText != null)
        {
            taskText.text = currentStage switch
            {
                NightmareTaskStage.FindWayOut => findWayOutText,
                NightmareTaskStage.CollectEscapeItems => collectEscapeItemsText,
                NightmareTaskStage.GoToParentsBedroomForKey => goToParentsBedroomForKeyText,
                NightmareTaskStage.GoToFrontDoor => goToFrontDoorText,
                _ => string.Empty
            };
        }

        bool showObjectives = currentStage == NightmareTaskStage.CollectEscapeItems;
        if (objectiveRoot != null)
            objectiveRoot.SetActive(showObjectives);

        for (int index = 0; index < RequiredObjectives.Length; index++)
        {
            if (objectiveTexts[index] == null)
                continue;

            NightmareObjectiveItem objective = RequiredObjectives[index];
            string count = IsObjectiveCollected(objective) ? "1" : "0";
            objectiveTexts[index].text = $"{GetObjectiveLabel(objective)} ({count}/1)";
        }

        for (int index = 0; index < 5; index++)
        {
            CollectedObjective collected = index < collectedObjectives.Count
                ? collectedObjectives[index]
                : null;
            bool hasCollectedItem = collected != null;
            bool hasIcon = hasCollectedItem && collected.icon != null;
            Image icon = inventoryIconImages[index];
            icon.gameObject.SetActive(hasIcon);
            if (hasIcon)
                icon.sprite = collected.icon;
        }
    }

    private static string GetObjectiveLabel(NightmareObjectiveItem objective)
    {
        return objective switch
        {
            NightmareObjectiveItem.OwnClothes => "Clothes",
            NightmareObjectiveItem.IdentityCard => "ID card",
            NightmareObjectiveItem.Wallet => "Wallet",
            _ => string.Empty
        };
    }

}

/// <summary>Creates the Nightmare task runtime only for the formal Nightmare route.</summary>
public static class NightmareTaskRuntimeBootstrap
{
    private const string ResourceName = "NightmareTaskSystem";
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
        if (!NightmareTaskController.IsNightmareGameplayScene(scene.name))
        {
            RemoveTaskSystem();
            return;
        }

        NightmareTaskController controller = EnsureTaskSystem();
        if (controller == null)
            return;

        bool waitForBedroomIntro = scene.name == NightmareTaskController.BedroomSceneName
            && NightmareBedroomIntro2D.HasPendingCutsceneArrival;
        controller.SynchronizeForScene(scene, !waitForBedroomIntro);
    }

    private static NightmareTaskController EnsureTaskSystem()
    {
        if (NightmareTaskController.Instance != null)
            return NightmareTaskController.Instance;

        GameObject prefab = Resources.Load<GameObject>(ResourceName);
        if (prefab == null)
        {
            Debug.LogWarning(
                "NightmareTaskSystem prefab is missing from Assets/Resources. "
                + "Restore Assets/Resources/NightmareTaskSystem.prefab.");
            return null;
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        return instance.GetComponent<NightmareTaskController>();
    }

    private static void RemoveTaskSystem()
    {
        NightmareTaskController controller = NightmareTaskController.Instance;
        if (controller != null)
        {
            controller.HideHud();
            UnityEngine.Object.Destroy(controller.gameObject);
        }
    }
}
