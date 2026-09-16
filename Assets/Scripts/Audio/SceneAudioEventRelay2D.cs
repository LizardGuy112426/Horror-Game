using UnityEngine;

/// <summary>Keeps scene UnityEvent targets local while routing calls to persistent audio.</summary>
public sealed class SceneAudioEventRelay2D : MonoBehaviour
{
    public void ClickSFX() => SoundEffectManager.instance?.ClickSFX();
    public void HoverSFX() => SoundEffectManager.instance?.HoverSFX();
    public void WalkSFX() => SoundEffectManager.instance?.WalkSFX();
    public void JumpSFX() => SoundEffectManager.instance?.JumpSFX();
    public void LandSFX() => SoundEffectManager.instance?.LandSFX();
    public void SpiderSFX() => SoundEffectManager.instance?.SpiderSFX();
    public void DadWalkSFX(float speed) => SoundEffectManager.instance?.DadWalkSFX(speed);
    public void MomWalkSFX(float speed) => SoundEffectManager.instance?.MomWalkSFX(speed);
    public void DadJumpscare() => SoundEffectManager.instance?.DadJumpscare();
    public void MomJumpscare() => SoundEffectManager.instance?.MomJumpscare();
    public void PlayRespawnSFX() => SoundEffectManager.instance?.PlayRespawnSFX();
    public void PlayDoorOpen() => SoundEffectManager.instance?.PlayDoorOpen();
    public void PlayDoorCloseOnSceneLoad() => SoundEffectManager.instance?.PlayDoorCloseOnSceneLoad();
    public void ChangeVolume(float value) => SoundVolumeController2D.SetVolume(value);
    public void ChangeMusicVolume(float value) => MusicVolumeController2D.SetVolume(value);
    public void ChangeEnemyVolume(float value) => EnemyVolumeController2D.SetVolume(value);
    public void ReportChasing() => AudioManager.instance?.ReportChasing();
    public void SetChaseMusicSuppressed(bool value) => AudioManager.instance?.SetChaseMusicSuppressed(value);
    public void DadSpotSFXPlay() => AudioManager.instance?.DadSpotSFXPlay();
    public void MomSpotSFXPlay() => AudioManager.instance?.MomSpotSFXPlay();
    public void StopEnemySpotSound() => AudioManager.instance?.StopEnemySpotSound();
}
