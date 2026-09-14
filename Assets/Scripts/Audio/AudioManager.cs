using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Chase Music Suppression")]
    [Tooltip("Chase music will never start (and immediately stops if already playing) while the active scene's name is in this list. " +
             "Useful for cutscene/Timeline scenes where enemy detection logic might still technically fire.")]
    [SerializeField] private List<string> chaseMusicDisabledScenes = new List<string>();

    private bool chaseMusicSuppressed;
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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoadedForChaseSuppression;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedForChaseSuppression;
    }

    private void HandleSceneLoadedForChaseSuppression(Scene scene, LoadSceneMode mode)
    {
        chaseMusicSuppressed = chaseMusicDisabledScenes.Contains(scene.name);

        if (chaseMusicSuppressed)
            StopChaseMusicImmediately();
    }

    private void Start()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.name == "Ending Animation")
        {
            StopIfPlaying(EnemySpotAudioSource);
        }
    }

    private static void StopIfPlaying(AudioSource source)
    {
        if (source != null)
            source.Stop();
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

    /// <summary>
    /// Manually suppress or re-allow chase music, e.g. from a Timeline Signal at the start/end
    /// of a cutscene. Independent of (and combined with) the scene-name list above — either one
    /// suppressing counts as suppressed.
    /// </summary>
    public void SetChaseMusicSuppressed(bool suppressed)
    {
        chaseMusicSuppressed = suppressed;

        if (suppressed)
            StopChaseMusicImmediately();
    }

    /// <summary>Call every frame from a chasing enemy (mirrors SoundEffectManager.ReportChaseProximity).
    /// Keeps the chase music going and resets the 3-second grace timer. Does nothing while suppressed.</summary>
    public void ReportChasing()
    {
        if (chaseMusicSuppressed)
            return;

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

    /// <summary>Hard cut, no fade — used when suppression kicks in, since we don't want
    /// even a brief fade-out audible over cutscene audio.</summary>
    private void StopChaseMusicImmediately()
    {
        isChaseMusicActive = false;

        if (chaseMusicFadeRoutine != null)
        {
            StopCoroutine(chaseMusicFadeRoutine);
            chaseMusicFadeRoutine = null;
        }

        if (BGMSource != null)
            BGMSource.Stop();
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