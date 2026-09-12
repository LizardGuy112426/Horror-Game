using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundEffectManager : MonoBehaviour
{
    public static SoundEffectManager instance;
    [SerializeField] private AudioSource HoverAudioSource;
    [SerializeField] private AudioSource AudioSource;
    [SerializeField] private AudioSource WalkAudioSource;
    [SerializeField] private AudioClip ButtonHoverSFX;
    [SerializeField] private AudioClip ButtonClickSFX;
    [SerializeField] private AudioClip WalkingSFX;
    [SerializeField] private AudioClip JumpingSFX;
    [SerializeField] private AudioClip SpidermanSFX;
    [SerializeField] private AudioClip DadWalkingSFX;
    [SerializeField] private AudioClip MomWalkingSFX;

    [Header("Door")]
    [Tooltip("Played immediately when the player interacts with a door.")]
    [SerializeField] private AudioClip DoorOpenSFX;
    [Tooltip("Played once the new scene finishes loading, since the door object itself is destroyed by then.")]
    [SerializeField] private AudioClip DoorCloseSFX;

    [Header("Master Volume")]
    [Tooltip("Controls Hover, Click, Jump, Heartbeat, and Spiderman Interact together. Dad/Mom Walk, Jumpscare, and ambient enemy idle sounds share EnemyVolume instead.")]
    [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;

    [Header("Player Walk (fixed ratio, not independently adjustable)")]
    [Tooltip("Player footsteps always play at this fraction of masterVolume.")]
    private const float WalkVolumeMultiplier = 0.3f;

    [Header("Ducking (lowers other sounds while player walk SFX plays)")]
    [Range(0f, 1f)][SerializeField] private float duckAmount = 0.5f;

    private float duckFactor = 1f;
    private Coroutine duckRoutine;

    [Header("Monster Walking Pitch By Speed")]
    [SerializeField] private float monsterMinSpeed = 0f;
    [SerializeField] private float monsterMaxSpeed = 8f;
    [SerializeField] private float monsterMinPitch = 0.85f;
    [SerializeField] private float monsterMaxPitch = 1.4f;
    [Tooltip("Shared by Dad/Mom footsteps, Jumpscare, and ambient enemy idle sounds (RandomSoundPlayer).")]
    [Range(0f, 1f)][SerializeField] private float EnemyVolume = 1f;
    [SerializeField] private AudioSource DadWalkAudioSource;
    [SerializeField] private AudioSource MomWalkAudioSource;

    [Header("Jumpscare")]
    [SerializeField] private AudioClip DadJumpscareSFX;
    [SerializeField] private AudioClip MomJumpscareSFX;
    [SerializeField] private AudioSource JumpscareAudioSource;

    [Header("Chase Heartbeat (proximity)")]
    [Tooltip("Should be a seamless looping heartbeat clip.")]
    [SerializeField] private AudioClip HeartbeatSFX;
    [SerializeField] private AudioSource HeartbeatAudioSource;
    [Range(0f, 1f)][SerializeField] private float heartbeatMinVolume = 0.05f;
    [Range(0f, 1f)][SerializeField] private float heartbeatMaxVolume = 1f;
    [SerializeField] private bool heartbeatPitchScales = false;
    [SerializeField] private float heartbeatMinPitch = 0.9f;
    [SerializeField] private float heartbeatMaxPitch = 1.3f;

    [Header("Heartbeat Fade-Out")]
    [Tooltip("How long after the chase ends before the heartbeat starts fading out.")]
    [SerializeField] private float heartbeatFadeOutDelay = 3f;
    [Tooltip("How long the fade-out itself takes once it starts.")]
    [SerializeField, Min(0f)] private float heartbeatFadeOutDuration = 1.5f;

    private float timeSinceLastChase = Mathf.Infinity;
    private Coroutine heartbeatFadeRoutine;

    private float heartbeatClosestDistance;
    private float heartbeatMaxDistanceRef;
    private bool heartbeatReportedThisFrame;

    private bool jumpscarePlaying;
    private Coroutine jumpscareRoutine;

    public bool IsJumpscarePlaying => jumpscarePlaying;

    public void HoverSFX()
    {
        if (jumpscarePlaying) return;
        HoverAudioSource.PlayOneShot(ButtonHoverSFX, masterVolume * duckFactor);
    }

    /// <summary>Player footstep sound. Fixed at 0.3x masterVolume, and ducks all other combined sounds while it plays.</summary>
    public void WalkSFX()
    {
        if (jumpscarePlaying || WalkAudioSource == null || WalkingSFX == null) return;

        WalkAudioSource.PlayOneShot(WalkingSFX, masterVolume * WalkVolumeMultiplier);

        if (duckRoutine != null)
            StopCoroutine(duckRoutine);
        duckRoutine = StartCoroutine(DuckOtherSoundsRoutine(WalkingSFX.length));
    }

    public void DadWalkSFX(float currentSpeed)
    {
        if (jumpscarePlaying || DadWalkAudioSource == null || DadWalkingSFX == null) return;
        float speedFraction = Mathf.InverseLerp(monsterMinSpeed, monsterMaxSpeed, Mathf.Abs(currentSpeed));
        DadWalkAudioSource.pitch = Mathf.Lerp(monsterMinPitch, monsterMaxPitch, speedFraction);
        DadWalkAudioSource.PlayOneShot(DadWalkingSFX, EnemyVolume * duckFactor);
    }
    public void MomWalkSFX(float currentSpeed)
    {
        if (jumpscarePlaying || MomWalkAudioSource == null || MomWalkingSFX == null) return;
        float speedFraction = Mathf.InverseLerp(monsterMinSpeed, monsterMaxSpeed, Mathf.Abs(currentSpeed));
        MomWalkAudioSource.pitch = Mathf.Lerp(monsterMinPitch, monsterMaxPitch, speedFraction);
        MomWalkAudioSource.PlayOneShot(MomWalkingSFX, EnemyVolume * duckFactor);
    }
    public void JumpSFX()
    {
        if (jumpscarePlaying) return;
        AudioSource.PlayOneShot(JumpingSFX, masterVolume * duckFactor);
    }
    public void SpiderSFX()
    {
        if (jumpscarePlaying) return;
        AudioSource.PlayOneShot(SpidermanSFX, masterVolume * duckFactor);
    }

    /// <summary>Length of the Spiderman interact clip in seconds, or 0 if unset.</summary>
    public float GetSpiderSFXDuration() => SpidermanSFX != null ? SpidermanSFX.length : 0f;

    public void ClickSFX()
    {
        if (jumpscarePlaying) return;
        AudioSource.PlayOneShot(ButtonClickSFX, masterVolume * duckFactor);
    }

    /// <summary>Sets the single master volume shared by combined sound categories (Hover, Click, Jump, Heartbeat, Spiderman Interact).</summary>
    public void ChangeVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    public void PlayDoorOpen()
    {
        if (jumpscarePlaying || AudioSource == null || DoorOpenSFX == null) return;
        AudioSource.PlayOneShot(DoorOpenSFX, masterVolume * duckFactor);
    }

    /// <summary>
    /// Call this right when the player interacts with a door. Since the door itself
    /// gets destroyed the moment the new scene loads, this waits for the next
    /// SceneManager.sceneLoaded event (fired on the persistent SoundEffectManager)
    /// and plays the close sound then, instead of relying on a coroutine on the door.
    /// </summary>
    public void PlayDoorCloseOnSceneLoad()
    {
        SceneManager.sceneLoaded -= HandleDoorCloseSceneLoaded;
        SceneManager.sceneLoaded += HandleDoorCloseSceneLoaded;
    }

    private void HandleDoorCloseSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleDoorCloseSceneLoaded;

        if (jumpscarePlaying || AudioSource == null || DoorCloseSFX == null) return;
        AudioSource.PlayOneShot(DoorCloseSFX, masterVolume * duckFactor);
    }

    /// <summary>Shared volume control for Dad/Mom footsteps, Jumpscare, and ambient enemy idle sounds.</summary>
    public void ChangeEnemyVolume(float volume)
    {
        EnemyVolume = Mathf.Clamp01(volume);
    }

    /// <summary>Read-only access so other scripts (e.g. RandomSoundPlayer) can scale their own playback by EnemyVolume.</summary>
    public float GetEnemyVolume() => EnemyVolume;

    private IEnumerator DuckOtherSoundsRoutine(float duration)
    {
        duckFactor = duckAmount;
        yield return new WaitForSeconds(duration);
        duckFactor = 1f;
        duckRoutine = null;
    }

    public void DadJumpscare()
    {
        PlayJumpscare(DadJumpscareSFX);
    }

    public void MomJumpscare()
    {
        PlayJumpscare(MomJumpscareSFX);
    }

    /// <summary>Returns the actual playback time, or zero when audio is unavailable.</summary>
    public float PlayJumpscareFor(EnemyAI.EnemyType type)
    {
        return PlayJumpscare(type == EnemyAI.EnemyType.Dad ? DadJumpscareSFX : MomJumpscareSFX);
    }

    public void StopJumpscare()
    {
        if (jumpscareRoutine != null)
            StopCoroutine(jumpscareRoutine);
        jumpscareRoutine = null;
        jumpscarePlaying = false;
        StopIfPlaying(JumpscareAudioSource);
    }


    /// <summary>
    /// Call every frame from a chasing enemy with its current distance to the player
    /// and the distance at which it started chasing (used as the "loudest" point).
    /// If multiple enemies report in the same frame, the closest one controls the heartbeat.
    /// </summary>
    public void ReportChaseProximity(float distanceToPlayer, float maxDistance)
    {
        if (!heartbeatReportedThisFrame || distanceToPlayer < heartbeatClosestDistance)
        {
            heartbeatClosestDistance = distanceToPlayer;
            heartbeatMaxDistanceRef = maxDistance;
        }

        heartbeatReportedThisFrame = true;
    }

    private void ApplyHeartbeat(float distanceToPlayer, float maxDistance)
    {
        if (jumpscarePlaying || HeartbeatAudioSource == null || HeartbeatSFX == null)
            return;

        if (!HeartbeatAudioSource.isPlaying)
        {
            HeartbeatAudioSource.clip = HeartbeatSFX;
            HeartbeatAudioSource.loop = true;
            HeartbeatAudioSource.Play();
        }

        float proximity = 1f - Mathf.Clamp01(distanceToPlayer / Mathf.Max(0.01f, maxDistance));
        HeartbeatAudioSource.volume = Mathf.Lerp(heartbeatMinVolume, heartbeatMaxVolume, proximity) * masterVolume * duckFactor;

        if (heartbeatPitchScales)
            HeartbeatAudioSource.pitch = Mathf.Lerp(heartbeatMinPitch, heartbeatMaxPitch, proximity);
    }

    private float PlayJumpscare(AudioClip clip)
    {
        if (clip == null || JumpscareAudioSource == null || !JumpscareAudioSource.isActiveAndEnabled)
            return 0f;

        MuteAllOtherSounds();

        JumpscareAudioSource.PlayOneShot(clip, EnemyVolume);

        if (jumpscareRoutine != null)
            StopCoroutine(jumpscareRoutine);

        float duration = clip.length / Mathf.Max(0.01f, Mathf.Abs(JumpscareAudioSource.pitch));
        jumpscareRoutine = StartCoroutine(ClearJumpscareFlagAfter(duration));
        return duration;
    }

    private void MuteAllOtherSounds()
    {
        jumpscarePlaying = true;

        StopIfPlaying(HoverAudioSource);
        StopIfPlaying(AudioSource);
        StopIfPlaying(WalkAudioSource);
        StopIfPlaying(DadWalkAudioSource);
        StopIfPlaying(MomWalkAudioSource);
        StopIfPlaying(HeartbeatAudioSource);

        if (heartbeatFadeRoutine != null)
        {
            StopCoroutine(heartbeatFadeRoutine);
            heartbeatFadeRoutine = null;
        }

        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopEnemySpotSound();
        }
    }

    private static void StopIfPlaying(AudioSource source)
    {
        if (source != null)
            source.Stop();
    }

    private IEnumerator ClearJumpscareFlagAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        jumpscarePlaying = false;
        jumpscareRoutine = null;
    }

    void Awake()
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

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void LateUpdate()
    {
        if (heartbeatReportedThisFrame)
        {
            timeSinceLastChase = 0f;

            if (heartbeatFadeRoutine != null)
            {
                StopCoroutine(heartbeatFadeRoutine);
                heartbeatFadeRoutine = null;
            }

            ApplyHeartbeat(heartbeatClosestDistance, heartbeatMaxDistanceRef);
        }
        else
        {
            timeSinceLastChase += Time.deltaTime;

            bool shouldStartFade = timeSinceLastChase >= heartbeatFadeOutDelay
                && heartbeatFadeRoutine == null
                && HeartbeatAudioSource != null
                && HeartbeatAudioSource.isPlaying;

            if (shouldStartFade)
                heartbeatFadeRoutine = StartCoroutine(FadeOutHeartbeatRoutine());
        }

        heartbeatReportedThisFrame = false;
    }

    private IEnumerator FadeOutHeartbeatRoutine()
    {
        float startVolume = HeartbeatAudioSource.volume;
        float elapsed = 0f;

        while (elapsed < heartbeatFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            HeartbeatAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / heartbeatFadeOutDuration);
            yield return null;
        }

        HeartbeatAudioSource.Stop();
        heartbeatFadeRoutine = null;
    }

}