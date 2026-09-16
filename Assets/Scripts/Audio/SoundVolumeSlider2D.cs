using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public sealed class SoundVolumeSlider2D : MonoBehaviour
{
    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        slider.SetValueWithoutNotify(SoundVolumeController2D.CurrentVolume);
        slider.onValueChanged.AddListener(HandleSliderChanged);
        SoundVolumeController2D.VolumeChanged += HandleGlobalVolumeChanged;
    }

    private void OnDisable()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(HandleSliderChanged);

        SoundVolumeController2D.VolumeChanged -= HandleGlobalVolumeChanged;
    }

    private static void HandleSliderChanged(float volume)
    {
        SoundVolumeController2D.SetVolume(volume);
    }

    private void HandleGlobalVolumeChanged(float volume)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(volume);
    }
}
