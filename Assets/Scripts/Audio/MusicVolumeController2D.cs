using System;
using UnityEngine;
using UnityEngine.Audio;

public static class MusicVolumeController2D
{
    private const string MixerResourcePath = "GameplayAudio";
    private const string MusicVolumeParameter = "MusicVolume";
    private const float MutedDecibels = -80f;

    private static AudioMixer musicMixer;
    private static float currentVolume = 1f;
    private static bool warnedAboutMissingMixer;

    public static float CurrentVolume => currentVolume;
    public static event Action<float> VolumeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        musicMixer = null;
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
        if (musicMixer == null)
            musicMixer = Resources.Load<AudioMixer>(MixerResourcePath);

        if (musicMixer == null)
        {
            if (!warnedAboutMissingMixer)
            {
                Debug.LogWarning($"Music AudioMixer was not found at Resources/{MixerResourcePath}.");
                warnedAboutMissingMixer = true;
            }

            return;
        }

        float decibels = currentVolume <= 0.0001f
            ? MutedDecibels
            : 20f * Mathf.Log10(currentVolume);
        musicMixer.SetFloat(MusicVolumeParameter, decibels);
    }
}
