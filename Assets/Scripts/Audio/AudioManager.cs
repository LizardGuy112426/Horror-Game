using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("BGM")]
    [SerializeField] private AudioSource BGMSource;

    [Header("Chase Music")]
    [Tooltip("Volume the chase music fades up to while actively being chased.")]
    [Range(0f, 1f)][SerializeField] private float chaseMusicVolume = 1f;
    [Tooltip("How long the music takes to fade in once chasing starts.")]
    [SerializeField, Min(0f)] private float chaseFadeInTime = 0.5f;
    [Tooltip("How long the music takes to fade out once the grace period ends.")]
    [SerializeField, Min(0f)] private float chaseFadeOutTime = 2f;
    [Tooltip("How long to wait after the last chase report before starting to fade out.")]
    [SerializeField, Min(0f)] private float chaseGraceDuration = 3f;

    private float lastChasingTime = -999f;
    private bool isChaseMusicActive;
    private Coroutine chaseMusicFadeRoutine;

    [Header("Enemy Spotted You")]
    [SerializeField] private AudioClip DadSpotSFX;
    [SerializeField] private AudioClip MomSpotSFX;
    [SerializeField] private AudioSource EnemySpotAudioSource;
    [Range(0f, 1f)]
    [SerializeField] private float EnemySpotVolume = 1f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (isChaseMusicActive && Time.time - lastChasingTime >= chaseGraceDuration)
        {
            StopChaseMusic();
        }
    }

    public void ChangeVolume(float volume)
    {
        BGMSource.volume = volume;
    }

    /// <summary>Call every frame from a chasing enemy (mirrors SoundEffectManager.ReportChaseProximity).
    /// Keeps the chase music going and resets the 3-second grace timer.</summary>
    public void ReportChasing()
    {
        lastChasingTime = Time.time;

        if (!isChaseMusicActive)
            StartChaseMusic();
    }

    private void StartChaseMusic()
    {
        isChaseMusicActive = true;

        if (chaseMusicFadeRoutine != null)
            StopCoroutine(chaseMusicFadeRoutine);

        if (!BGMSource.isPlaying)
            BGMSource.Play();

        chaseMusicFadeRoutine = StartCoroutine(FadeBGMVolume(chaseMusicVolume, chaseFadeInTime));
    }

    private void StopChaseMusic()
    {
        isChaseMusicActive = false;

        if (chaseMusicFadeRoutine != null)
            StopCoroutine(chaseMusicFadeRoutine);

        chaseMusicFadeRoutine = StartCoroutine(FadeBGMVolume(0f, chaseFadeOutTime));
    }

    private IEnumerator FadeBGMVolume(float targetVolume, float duration)
    {
        float startVolume = BGMSource.volume;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            BGMSource.volume = targetVolume;
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                BGMSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                yield return null;
            }

            BGMSource.volume = targetVolume;
        }

        if (targetVolume <= 0f)
            BGMSource.Stop();

        chaseMusicFadeRoutine = null;
    }

    public void DadSpotSFXPlay()
    {
        if (EnemySpotAudioSource != null && DadSpotSFX != null)
        {
            EnemySpotAudioSource.PlayOneShot(
                DadSpotSFX,
                EnemySpotVolume
            );
        }
    }

    public void MomSpotSFXPlay()
    {
        if (EnemySpotAudioSource != null && MomSpotSFX != null)
        {
            EnemySpotAudioSource.PlayOneShot(
                MomSpotSFX,
                EnemySpotVolume
            );
        }
    }

    public void StopEnemySpotSound()
    {
        if (EnemySpotAudioSource != null)
        {
            EnemySpotAudioSource.Stop();
        }
    }
}