using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns the editable RUN/STAY modal. STAY is intentionally disabled for this story pass.</summary>
public sealed class NightmareFrontDoorChoiceUI : MonoBehaviour
{
    private const string ResourceName = "NightmareFrontDoorChoiceUI";
    private const double ChoiceDuration = 5d;

    [Header("Editable UI References")]
    [SerializeField] private GameObject inputBlocker;
    [SerializeField] private Text choiceTitle;
    [SerializeField] private Button runButton;
    [SerializeField] private Button stayButton;
    [SerializeField] private Text timeText;

    private Action runCallback;
    private Action timeoutCallback;
    private double deadline;
    private bool resolved;
    private bool cursorCaptured;
    private bool storedCursorState;
    private CursorLockMode storedCursorLockMode;

    public static NightmareFrontDoorChoiceUI Show(Action onRun, Action onTimeout = null)
    {
        GameObject prefab = Resources.Load<GameObject>(ResourceName);
        if (prefab == null)
        {
            Debug.LogWarning(
                "Nightmare Front Door Choice UI is missing from Assets/Resources. "
                + "The player will be released so the game cannot soft-lock.");
            return null;
        }

        GameObject instance = Instantiate(prefab);
        NightmareFrontDoorChoiceUI choice = instance.GetComponent<NightmareFrontDoorChoiceUI>();
        if (choice == null)
        {
            Destroy(instance);
            Debug.LogWarning("NightmareFrontDoorChoiceUI prefab has no NightmareFrontDoorChoiceUI component.");
            return null;
        }

        choice.Initialize(onRun, onTimeout);
        return choice;
    }

    public void Configure(
        GameObject blocker,
        Text title,
        Button run,
        Button stay)
    {
        inputBlocker = blocker;
        choiceTitle = title;
        runButton = run;
        stayButton = stay;
    }

    private void Initialize(Action onRun, Action onTimeout)
    {
        runCallback = onRun;
        timeoutCallback = onTimeout;
        resolved = false;
        deadline = Time.realtimeSinceStartupAsDouble + ChoiceDuration;
        storedCursorState = Cursor.visible;
        storedCursorLockMode = Cursor.lockState;
        cursorCaptured = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        gameObject.SetActive(true);
        if (timeText == null)
        {
            Transform timeObject = transform.Find("TIME");
            if (timeObject != null)
                timeText = timeObject.GetComponent<Text>();
        }
        UpdateTimeText();
        if (inputBlocker != null)
            inputBlocker.SetActive(true);
        if (choiceTitle != null && string.IsNullOrWhiteSpace(choiceTitle.text))
            choiceTitle.text = "请选择";

        if (runButton != null)
        {
            runButton.onClick.RemoveListener(ChooseRun);
            runButton.onClick.AddListener(ChooseRun);
            runButton.interactable = true;
        }

        // STAY is a visible placeholder until its story branch is authored.
        if (stayButton != null)
        {
            stayButton.onClick.RemoveAllListeners();
            stayButton.interactable = false;
        }
    }

    private void ChooseRun()
    {
        // Check the same deadline here and in Update, regardless of UI update order.
        Resolve(Time.realtimeSinceStartupAsDouble >= deadline);
    }

    private void Update()
    {
        if (resolved)
            return;
        if (MCControllers.Instance != null && MCControllers.Instance.IsDying)
        {
            Cancel();
            return;
        }
        UpdateTimeText();
        if (Time.realtimeSinceStartupAsDouble >= deadline)
            Resolve(true);
    }

    private void UpdateTimeText()
    {
        if (timeText != null)
            timeText.text = Mathf.CeilToInt(Mathf.Max(0f,
                (float)(deadline - Time.realtimeSinceStartupAsDouble))).ToString();
    }

    private void Resolve(bool timedOut)
    {
        if (resolved)
            return;
        resolved = true;
        if (runButton != null)
            runButton.interactable = false;

        Action callback = timedOut ? timeoutCallback : runCallback;
        runCallback = null;
        timeoutCallback = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
        callback?.Invoke();
    }

    public void Cancel()
    {
        resolved = true;
        runCallback = null;
        timeoutCallback = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        resolved = true;
        runCallback = null;
        timeoutCallback = null;
        if (runButton != null)
            runButton.onClick.RemoveListener(ChooseRun);

        if (!Application.isPlaying || !cursorCaptured)
            return;

        Cursor.visible = storedCursorState;
        Cursor.lockState = storedCursorLockMode;
        cursorCaptured = false;
    }
}
