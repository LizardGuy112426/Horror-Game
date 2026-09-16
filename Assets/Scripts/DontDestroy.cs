using UnityEngine;

public class DontDestroy : MonoBehaviour
{
    public static DontDestroy Instance;
    private void Awake()
    {
        // Ending Timeline owns this local audio hierarchy. The new audio root
        // handles persistence; do not detach or preserve the Timeline's child.
        if (GetComponent<AudioManager>() != null || GetComponent<SoundEffectManager>() != null)
            return;
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
