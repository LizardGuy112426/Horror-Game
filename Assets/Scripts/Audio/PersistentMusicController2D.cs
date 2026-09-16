using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Owns the only persistent audio root. Ending scenes retain their authored audio.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class PersistentMusicController2D : MonoBehaviour
{
    public static PersistentMusicController2D Instance { get; private set; }
    [SerializeField] private AudioSource musicSource;
    private Coroutine transition;
    private float targetVolume = 1f;
    private ScheduledPlayback scheduledPlayback;

    /// <summary>A single owned CG start. DSP time continues after a one-shot clip ends.</summary>
    public sealed class ScheduledPlayback
    {
        public bool IsReady { get; internal set; }
        public bool IsCancelled { get; internal set; }
        public bool UsesAudioClock { get; internal set; }
        public double StartTime { get; internal set; }
        public double Elapsed => (UsesAudioClock ? AudioSettings.dspTime : Time.realtimeSinceStartupAsDouble) - StartTime;
    }
    public AudioSource MusicSource => musicSource;
    public static bool UsesLocalEndingAudio(string sceneName) =>
        sceneName == "Ending Animation" || sceneName == "ChooseEnding";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        AudioManager.instance = null;
        SoundEffectManager.instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>("PersistentAudioSystem");
        if (prefab != null) Instantiate(prefab);
        else Debug.LogError("Missing Resources/PersistentAudioSystem.prefab.");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        CancelSynchronizedMusic(scheduledPlayback);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        CancelSynchronizedMusic(scheduledPlayback);
        bool ending = UsesLocalEndingAudio(scene.name);
        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
            if (source != musicSource) source.Stop();
        // Clear effects from the previous scene, but leave pending door-close callbacks intact.
        GetComponent<SoundEffectManager>()?.ResetForScene();
        GetComponent<AudioManager>()?.ResetForScene(scene.name);
        // Persistent random enemy ambience must only run in a scene that
        // actually contains an EnemyAI. Previously this was enabled in every
        // scene whose name started with "NM_", so enemy random sounds leaked
        // into Nightmare scenes with no enemy.
        bool sceneHasEnemy = SceneContainsEnemy(scene);

        foreach (RandomSoundPlayer ambient in GetComponentsInChildren<RandomSoundPlayer>(true))
        {
            ambient.enabled = !ending
                && scene.name.StartsWith("NM_")
                && sceneHasEnemy;

            if (ambient.enabled)
                ambient.ScheduleNextCheck();
        }
        SceneMusicCue2D cue = null;
        bool synchronizedCutscene = false;
        if (!ending)
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var cg in root.GetComponentsInChildren<CutsceneController>())
                    synchronizedCutscene |= cg.isActiveAndEnabled && cg.SynchronizeWithMusic;
                var candidate = root.GetComponentInChildren<SceneMusicCue2D>();
                if (cue == null && candidate != null && candidate.isActiveAndEnabled) cue = candidate;
            }
        // The CG owns its startup request. Do not also auto-play the same cue here.
        if (synchronizedCutscene)
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            return;
        }
        if (cue == null)
        {
            StopMusic(0f);
            return;
        }
        targetVolume = cue.volume;
        PlayMusic(cue.musicClip, cue.loop, cue.fadeOutDuration, cue.fadeInDuration, cue.restartOnEnter);
    }

    private static bool SceneContainsEnemy(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(
            FindObjectsInactive.Include
        );

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.scene == scene)
                return true;
        }

        return false;
    }

    public void PlayMusic(AudioClip clip, bool loop, float fadeOut, float fadeIn, bool restart)
    {
        CancelSynchronizedMusic(scheduledPlayback);
        if (musicSource == null) return;
        if (transition != null) StopCoroutine(transition);
        if (!restart && clip != null && musicSource.clip == clip)
        {
            musicSource.loop = loop;
            musicSource.volume = targetVolume;
            transition = null;
            return;
        }
        transition = StartCoroutine(ChangeMusic(clip, loop, fadeOut, fadeIn));
    }

    public void StopMusic(float fadeDuration) => PlayMusic(null, false, fadeDuration, 0f, true);

    public ScheduledPlayback PrepareSynchronizedMusic(SceneMusicCue2D cue)
    {
        CancelSynchronizedMusic(scheduledPlayback);
        if (transition != null) StopCoroutine(transition);
        var request = new ScheduledPlayback();
        scheduledPlayback = request;
        transition = StartCoroutine(PrepareScheduledPlayback(cue, request));
        return request;
    }

    public void CancelSynchronizedMusic(ScheduledPlayback request)
    {
        if (request == null || request != scheduledPlayback) return;
        request.IsCancelled = true;
        if (transition != null) StopCoroutine(transition);
        transition = null;
        if (musicSource != null) musicSource.Stop(); // Also cancels a future PlayScheduled.
        scheduledPlayback = null;
    }

    private IEnumerator PrepareScheduledPlayback(SceneMusicCue2D cue, ScheduledPlayback request)
    {
        if (musicSource == null || !musicSource.isActiveAndEnabled || cue == null || cue.musicClip == null)
        {
            StartSilentClock(request, "CG music cue, clip or active AudioSource is missing.");
            yield break;
        }

        if (musicSource.isPlaying && cue.fadeOutDuration > 0f)
        {
            double began = Time.realtimeSinceStartupAsDouble;
            float volume = musicSource.volume;
            while (Time.realtimeSinceStartupAsDouble - began < cue.fadeOutDuration)
            {
                musicSource.volume = Mathf.Lerp(volume, 0f,
                    (float)((Time.realtimeSinceStartupAsDouble - began) / cue.fadeOutDuration));
                yield return null;
            }
        }
        musicSource.Stop();
        var clip = cue.musicClip;
        if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        double deadline = Time.realtimeSinceStartupAsDouble + 10d;
        while ((clip.loadState == AudioDataLoadState.Loading || clip.loadState == AudioDataLoadState.Unloaded)
            && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            StartSilentClock(request, "CG music failed to load within 10 seconds: " + clip.name);
            yield break;
        }

        // Let the scene's first Start/initialization pass finish before submitting a future
        // audio start. Otherwise a heavy first frame can consume the scheduling lead-in.
        yield return null;
        targetVolume = cue.volume;
        musicSource.clip = clip;
        musicSource.loop = cue.loop;
        musicSource.pitch = 1f;
        musicSource.timeSamples = 0;
        musicSource.volume = cue.fadeInDuration > 0f ? 0f : targetVolume;
        request.UsesAudioClock = true;
        request.StartTime = AudioSettings.dspTime + 0.15d;
        musicSource.PlayScheduled(request.StartTime);
        request.IsReady = true;
        if (cue.fadeInDuration > 0f)
        {
            while (request.Elapsed < cue.fadeInDuration)
            {
                musicSource.volume = targetVolume * Mathf.Clamp01((float)(request.Elapsed / cue.fadeInDuration));
                yield return null;
            }
            musicSource.volume = targetVolume;
        }
        transition = null;
    }

    private void StartSilentClock(ScheduledPlayback request, string reason)
    {
        if (musicSource != null) { musicSource.Stop(); musicSource.clip = null; }
        Debug.LogWarning(reason + " Continuing CG with a realtime clock.", this);
        request.UsesAudioClock = false;
        request.StartTime = Time.realtimeSinceStartupAsDouble;
        request.IsReady = true;
        transition = null;
    }

    private IEnumerator ChangeMusic(AudioClip clip, bool loop, float fadeOut, float fadeIn)
    {
        if (musicSource.isPlaying && fadeOut > 0f)
            yield return FadeTo(0f, fadeOut);
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        if (clip != null)
        {
            musicSource.volume = fadeIn > 0f ? 0f : targetVolume;
            musicSource.Play();
            if (fadeIn > 0f) yield return FadeTo(targetVolume, fadeIn);
        }
        transition = null;
    }

    private IEnumerator FadeTo(float volume, float duration)
    {
        float start = musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(start, volume, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        musicSource.volume = volume;
    }
}
