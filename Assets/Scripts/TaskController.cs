using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TaskController : MonoBehaviour
{
    public static TaskController Instance;

    [System.Serializable]
    public class Task
    {
        [TextArea]
        public string taskText;

        [Tooltip("Name of the bool in GameState.")]
        public string boolName;

        public bool completed;
    }
    [Header("Text Colors")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color completeTextColor = Color.gray;
    [Header("Tasks")]
    [SerializeField] private List<Task> tasks = new List<Task>();

    [Header("Task UI")]
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private Image taskIcon;

    [Header("Icons")]
    [SerializeField] private Sprite incompleteIcon;
    [SerializeField] private Sprite completeIcon;

    [Header("Display")]
    [SerializeField] private bool showCompletedTask = true;

    private int currentTaskIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        UpdateTaskUI();
    }

    private void Update()
    {
        CheckCurrentTask();
    }

    private void CheckCurrentTask()
    {
        if (currentTaskIndex >= tasks.Count)
            return;

        Task currentTask = tasks[currentTaskIndex];

        if (currentTask.completed)
            return;

        if (GameState.Instance == null)
            return;

        bool condition = GetBoolValue(currentTask.boolName);

        if (condition)
        {
            CompleteCurrentTask();
        }
    }

    private bool GetBoolValue(string boolName)
    {
        var field = typeof(GameState).GetField(boolName);

        if (field == null)
        {
            Debug.LogWarning(
                "TaskController: Could not find bool '" +
                boolName + "' in GameState."
            );

            return false;
        }

        return (bool)field.GetValue(GameState.Instance);
    }

    private void CompleteCurrentTask()
    {
        tasks[currentTaskIndex].completed = true;

        UpdateTaskUI();

        currentTaskIndex++;

        if (currentTaskIndex < tasks.Count)
        {
            UpdateTaskUI();
        }
        else
        {
            Debug.Log("All tasks completed!");
        }
    }

    private void UpdateTaskUI()
    {
        if (currentTaskIndex >= tasks.Count)
        {
            if (taskText != null)
            {
                taskText.text = "All tasks complete!";
                taskText.color = completeTextColor;
                taskText.fontStyle = FontStyles.Normal;
            }

            if (taskIcon != null)
                taskIcon.sprite = completeIcon;

            return;
        }

        Task currentTask = tasks[currentTaskIndex];

        if (taskText != null)
        {
            taskText.text = currentTask.taskText;

            if (currentTask.completed)
            {
                taskText.fontStyle = FontStyles.Strikethrough;
                taskText.color = completeTextColor;
            }
            else
            {
                taskText.fontStyle = FontStyles.Normal;
                taskText.color = normalTextColor;
            }
        }

        if (taskIcon != null)
        {
            taskIcon.sprite = currentTask.completed
                ? completeIcon
                : incompleteIcon;
        }
    }
}