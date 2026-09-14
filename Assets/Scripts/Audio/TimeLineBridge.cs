using UnityEngine;

/// <summary>
/// Place this on any regular (non-persistent) GameObject in a scene that has a Timeline.
/// A Timeline Signal Receiver can call these public methods directly, letting the
/// Timeline trigger audio on the persistent SoundEffectManager without ever needing
/// a direct reference to it (which isn't possible at edit time, since DontDestroyOnLoad
/// objects don't exist in the scene until runtime).
/// </summary>
public class TimelineAudioBridge : MonoBehaviour
{
    // Add one method here per sound you want a Timeline Signal to be able to trigger.
    // Wire each one up in the Signal Receiver's list as a UnityEvent target.

    public void PlayJumpSFX()
    {
        if (SoundEffectManager.instance != null)
            SoundEffectManager.instance.JumpSFX();
    }

    public void PlayClickSFX()
    {
        if (SoundEffectManager.instance != null)
            SoundEffectManager.instance.ClickSFX();
    }

    // Example for whatever "awake" sound you actually need —
    // rename this and point it at the correct method once you tell me which clip/category it is.
    public void PlayAwakeSFX()
    {
        if (SoundEffectManager.instance != null)
            SoundEffectManager.instance.ClickSFX(); // placeholder call
    }

    // --- Cutscene movement lock ---
    // Same problem as audio: MCControllers is DontDestroyOnLoad, so Activation Track
    // can't reference it either. Use a Signal at the start of the cutscene calling
    // LockPlayerMovement(), and another Signal at the end calling UnlockPlayerMovement().

    public void LockPlayerMovement()
    {
        if (MCControllers.Instance != null)
            MCControllers.Instance.SetMovementEnabled(false);
    }

    public void UnlockPlayerMovement()
    {
        if (MCControllers.Instance != null)
            MCControllers.Instance.SetMovementEnabled(true);
    }

    // --- Chase music suppression for cutscenes ---
    // If enemy detection logic can still technically fire during this Timeline scene
    // (e.g. the enemy or player object persists), this stops chase music from ever
    // starting (or immediately kills it if it started) so it can't play over cutscene audio.

    public void SuppressChaseMusic()
    {
        if (AudioManager.instance != null)
            AudioManager.instance.SetChaseMusicSuppressed(true);
    }

    public void UnsuppressChaseMusic()
    {
        if (AudioManager.instance != null)
            AudioManager.instance.SetChaseMusicSuppressed(false);
    }
}