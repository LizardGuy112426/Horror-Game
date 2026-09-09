using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("BGM")]
    [SerializeField] private AudioSource BGMSource;

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

    public void ChangeVolume(float volume)
    {
        BGMSource.volume = volume;
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