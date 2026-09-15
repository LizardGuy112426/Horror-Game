using System;
using UnityEngine;
using UnityEngine.Audio;

public static class EnemyVolumeController2D
{
    private const string MixerResourcePath = "GameplayAudio";
    private const string EnemyVolumeParameter = "EnemyVolume";
    private const float MutedDecibels = -80f;

    private static AudioMixer audioMixer;
    private static float currentVolume = 1f;
    private static bool warnedAboutMissingMixer;

    public static float CurrentVolume => currentVolume;
    public static event Action<float> VolumeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        audioMixer = null;
        currentVolume = 1f;
        warnedAboutMissingMixer = false;
        VolumeChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        ApplyVolume();
    }

    public static void SetVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        bool changed = !Mathf.Approximately(currentVolume, clampedVolume);
        currentVolume = clampedVolume;

        ApplyVolume();

        if (changed)
            VolumeChanged?.Invoke(currentVolume);
    }

    private static void ApplyVolume()
    {
        if (audioMixer == null)
            audioMixer = Resources.Load<AudioMixer>(MixerResourcePath);

        if (audioMixer == null)
        {
            if (!warnedAboutMissingMixer)
            {
                Debug.LogWarning($"Enemy AudioMixer was not found at Resources/{MixerResourcePath}.");
                warnedAboutMissingMixer = true;
            }

            return;
        }

        float decibels = currentVolume <= 0.0001f
            ? MutedDecibels
            : 20f * Mathf.Log10(currentVolume);
        audioMixer.SetFloat(EnemyVolumeParameter, decibels);
    }
}
