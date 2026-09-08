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

    [SerializeField] private AudioClip DadJumpscareSFX;
    [SerializeField] private AudioClip MomJumpscareSFX;
    [SerializeField] private AudioSource JumpscareAudioSource;
    [Range(0f, 1f)][SerializeField] private float JumpscareVolume = 1f;
    public void HoverSFX()
    {
        HoverAudioSource.PlayOneShot(ButtonHoverSFX);
    }

    public void WalkSFX()
    {
        WalkAudioSource.volume = walkVolume;
        AudioSource.PlayOneShot(WalkingSFX);
    }
    public void DadWalkSFX(float currentSpeed)
    {
        float speedFraction = Mathf.InverseLerp(monsterMinSpeed, monsterMaxSpeed, Mathf.Abs(currentSpeed));
        DadWalkAudioSource.pitch = Mathf.Lerp(monsterMinPitch, monsterMaxPitch, speedFraction);
        DadWalkAudioSource.volume = monsterWalkVolume;
        DadWalkAudioSource.PlayOneShot(DadWalkingSFX);
    }
    public void MomWalkSFX(float currentSpeed)
    {
        float speedFraction = Mathf.InverseLerp(monsterMinSpeed, monsterMaxSpeed, Mathf.Abs(currentSpeed));
        MomWalkAudioSource.pitch = Mathf.Lerp(monsterMinPitch, monsterMaxPitch, speedFraction);
        MomWalkAudioSource.volume = monsterWalkVolume;
        MomWalkAudioSource.PlayOneShot(MomWalkingSFX);
    }
    public void JumpSFX()
    {
        AudioSource.PlayOneShot(JumpingSFX);
    }

    public void ClickSFX()
    {
        AudioSource.PlayOneShot(ButtonClickSFX);
    }

    public void ChangeVolume(float volume)
    {
        HoverAudioSource.volume = volume;
        AudioSource.volume = volume;
    }

    public void DadJumpscare()
    {
        JumpscareAudioSource.volume = JumpscareVolume;
        AudioSource.PlayOneShot(DadJumpscareSFX, 2.0f);
    }
    public void MomJumpscare()
    {
        JumpscareAudioSource.volume = JumpscareVolume;
        AudioSource.PlayOneShot(MomJumpscareSFX, 2.0f);
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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

}
