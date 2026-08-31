using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState Instance;

    [Header("Task Conditions")]
    public bool Jump;
    public bool hasKey;
    public bool doorOpened;

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
}