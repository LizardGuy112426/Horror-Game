using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns the editable, timed RUN/STAY modal.</summary>
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
    private Action stayCallback;
    private enum Choice { Run, Stay, Timeout }
    private double deadline;
    private bool resolved;
    private bool cursorCaptured;
    private bool storedCursorState;
    private CursorLockMode storedCursorLockMode;

    public static NightmareFrontDoorChoiceUI Show(Action onRun, Action onTimeout = null, Action onStay = null)
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

        choice.Initialize(onRun, onTimeout, onStay);
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

    private void Initialize(Action onRun, Action onTimeout, Action onStay)
    {
        runCallback = onRun;
        timeoutCallback = onTimeout;
        stayCallback = onStay;
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

        if (stayButton != null)
        {
            stayButton.onClick.RemoveListener(ChooseStay);
            stayButton.onClick.AddListener(ChooseStay);
            stayButton.interactable = onStay != null;
        }
    }

    private void ChooseRun()
    {
        // Check the same deadline here and in Update, regardless of UI update order.
        Resolve(Time.realtimeSinceStartupAsDouble >= deadline ? Choice.Timeout : Choice.Run);
    }

    private void ChooseStay()
    {
        if (stayCallback != null)
            Resolve(Time.realtimeSinceStartupAsDouble >= deadline ? Choice.Timeout : Choice.Stay);
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
            Resolve(Choice.Timeout);
    }

    private void UpdateTimeText()
    {
        if (timeText != null)
            timeText.text = Mathf.CeilToInt(Mathf.Max(0f,
                (float)(deadline - Time.realtimeSinceStartupAsDouble))).ToString();
    }

    private void Resolve(Choice choice)
    {
        if (resolved)
            return;
        resolved = true;
        if (runButton != null)
            runButton.interactable = false;
        if (stayButton != null)
            stayButton.interactable = false;

        Action callback = choice == Choice.Timeout ? timeoutCallback
            : choice == Choice.Stay ? stayCallback : runCallback;
        runCallback = null;
        timeoutCallback = null;
        stayCallback = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
        callback?.Invoke();
    }

    public void Cancel()
    {
        resolved = true;
        runCallback = null;
        timeoutCallback = null;
        stayCallback = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        resolved = true;
        runCallback = null;
        timeoutCallback = null;
        stayCallback = null;
        if (runButton != null)
            runButton.onClick.RemoveListener(ChooseRun);
        if (stayButton != null)
            stayButton.onClick.RemoveListener(ChooseStay);

        if (!Application.isPlaying || !cursorCaptured)
            return;

        Cursor.visible = storedCursorState;
        Cursor.lockState = storedCursorLockMode;
        cursorCaptured = false;
    }
}
