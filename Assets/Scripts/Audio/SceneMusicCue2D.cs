using UnityEngine;

/// <summary>Scene-local Inspector settings consumed by the persistent music player.</summary>
public sealed class SceneMusicCue2D : MonoBehaviour
{
    public AudioClip musicClip;
    public bool loop;
    [Min(0f)] public float fadeOutDuration = 1f;
    [Min(0f)] public float fadeInDuration;
    public bool restartOnEnter = true;
    [Range(0f, 1f)] public float volume = 1f;
}
