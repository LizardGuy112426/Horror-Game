using UnityEngine;

/// <summary>
/// Place this on any GameObject in the Timeline scene. On load, it destroys the
/// persistent MCControllers player instance entirely, so nothing on that object
/// (including its AudioSource) can keep running or leak sound into this scene.
///
/// IMPORTANT: since MCControllers is DontDestroyOnLoad, destroying it here is
/// permanent — no player will exist afterward unless the NEXT scene explicitly
/// spawns a fresh one. Any manager field that was pointed at this player's
/// AudioSource (the actual root cause of the original leak) will become a
/// missing reference until re-wired to a new instance or a dedicated AudioSource.
/// </summary>
public class DestroyPlayerOnSceneEnter : MonoBehaviour
{
    private void Awake()
    {
        if (MCControllers.Instance != null)
        {
            Destroy(MCControllers.Instance.gameObject);
        }
    }
}
