using System.Collections;
using UnityEngine;

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
    [SerializeField] private AudioClip DadWalkingSFX;
    [SerializeField] private AudioClip MomWalkingSFX;
    [Range(0f, 1f)][SerializeField] private float walkVolume = 1f;

    [Header("Monster Walking Pitch By Speed")]
    [SerializeField] private float monsterMinSpeed = 0f;
    [SerializeField] private float monsterMaxSpeed = 8f;
    [SerializeField] private float monsterMinPitch = 0.85f;
    [SerializeField] private float monsterMaxPitch = 1.4f;
    [Range(0f, 1f)][SerializeField] private float monsterWalkVolume = 1f;
    [SerializeField] private AudioSource DadWalkAudioSource;
    [SerializeField] private AudioSource MomWalkAudioSource;

    [Header("Jumpscare")]
    [SerializeField] private AudioClip DadJumpscareSFX;
    [SerializeField] private AudioClip MomJumpscareSFX;
    [SerializeField] private AudioSource JumpscareAudioSource;
    [Range(0f, 1f)][SerializeField] private float JumpscareVolume = 1f;

    [Header("Chase Heartbeat (proximity)")]
    [Tooltip("Should be a seamless looping heartbeat clip.")]
    [SerializeField] private AudioClip HeartbeatSFX;
    [SerializeField] private AudioSource HeartbeatAudioSource;
    [Range(0f, 1f)][SerializeField] private float heartbeatMinVolume = 0.05f;
    [Range(0f, 1f)][SerializeField] private float heartbeatMaxVolume = 1f;
    [SerializeField] private bool heartbeatPitchScales = false;
    [SerializeField] private float heartbeatMinPitch = 0.9f;
    [SerializeField] private float heartbeatMaxPitch = 1.3f;

    private float heartbeatClosestDistance;
    private float heartbeatMaxDistanceRef;
    private bool heartbeatReportedThisFrame;

    private bool jumpscarePlaying;
    private Coroutine jumpscareRoutine;

    public bool IsJumpscarePlaying => jumpscarePlaying;

    public void HoverSFX()
    {
        if (jumpscarePlaying) return;
        HoverAudioSource.PlayOneShot(ButtonHoverSFX);
    }

    public void WalkSFX()
    {
        if (jumpscarePlaying || WalkAudioSource == null || WalkingSFX == null) return;
        WalkAudioSource.volume = walkVolume;
        WalkAudioSource.PlayOneShot(WalkingSFX);
    }
    public void DadWalkSFX(float currentSpeed)
    {
        if (jumpscarePlaying || DadWalkAudioSource == null || DadWalkingSFX == null) return;
        float speedFraction = Mathf.InverseLerp(monsterMinSpeed, monsterMaxSpeed, Mathf.Abs(currentSpeed));
        DadWalkAudioSource.pitch = Mathf.Lerp(monsterMinPitch, monsterMaxPitch, speedFraction);
        DadWalkAudioSource.volume = monsterWalkVolume;
        DadWalkAudioSource.PlayOneShot(DadWalkingSFX);
    }
    public void MomWalkSFX(float currentSpeed)
    {
        if (jumpscarePlaying || MomWalkAudioSource == null || MomWalkingSFX == null) return;
        float speedFraction = Mathf.InverseLerp(monsterMinSpeed, monsterMaxSpeed, Mathf.Abs(currentSpeed));
        MomWalkAudioSource.pitch = Mathf.Lerp(monsterMinPitch, monsterMaxPitch, speedFraction);
        MomWalkAudioSource.volume = monsterWalkVolume;
        MomWalkAudioSource.PlayOneShot(MomWalkingSFX);
    }
    public void JumpSFX()
    {
        if (jumpscarePlaying) return;
        AudioSource.PlayOneShot(JumpingSFX);
    }

    public void ClickSFX()
    {
        if (jumpscarePlaying) return;
        AudioSource.PlayOneShot(ButtonClickSFX);
    }

    public void ChangeVolume(float volume)
    {
        HoverAudioSource.volume = volume;
        AudioSource.volume = volume;
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
        HeartbeatAudioSource.volume = Mathf.Lerp(heartbeatMinVolume, heartbeatMaxVolume, proximity);

        if (heartbeatPitchScales)
            HeartbeatAudioSource.pitch = Mathf.Lerp(heartbeatMinPitch, heartbeatMaxPitch, proximity);
    }

    private void StopHeartbeat()
    {
        if (HeartbeatAudioSource != null && HeartbeatAudioSource.isPlaying)
            HeartbeatAudioSource.Stop();
    }

    private float PlayJumpscare(AudioClip clip)
    {
        if (clip == null || JumpscareAudioSource == null || !JumpscareAudioSource.isActiveAndEnabled)
            return 0f;

        MuteAllOtherSounds();

        JumpscareAudioSource.volume = JumpscareVolume;
        JumpscareAudioSource.PlayOneShot(clip);

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
            ApplyHeartbeat(heartbeatClosestDistance, heartbeatMaxDistanceRef);
        }
        else
        {
            StopHeartbeat();
        }

        heartbeatReportedThisFrame = false;
    }

}
