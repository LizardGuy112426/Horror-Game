using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>Batch-only integration checks in an isolated copy of the project.</summary>
[InitializeOnLoad]
public static class PersistentAudioValidation
{
    private static IEnumerator checks;
    private static double resumeAt;
    static PersistentAudioValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("AudioValidation", false)) return;
            SessionState.SetBool("AudioValidation", false);
            checks = CheckRuntime();
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run validation in an isolated batch project.");
        ValidateAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Cutscene2.unity");
        SessionState.SetBool("AudioValidation", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void ValidateAssets()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PersistentAudioAuthoring.AudioPath);
        Require(prefab != null, "Missing persistent prefab");
        Require(prefab.GetComponent<PersistentMusicController2D>() != null, "Missing root controller");
        var sfx = new SerializedObject(prefab.GetComponent<SoundEffectManager>());
        foreach (string field in new[] { "AudioSource", "HoverAudioSource", "WalkAudioSource", "RespawnSFX" })
            Require(sfx.FindProperty(field).objectReferenceValue != null, "Unassigned " + field);
        foreach (string path in new[] { PersistentAudioAuthoring.AudioPath, "Assets/Prefeb/Audio.prefab" })
            CheckRouting(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        CheckSliderBindings(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/OptionCanva.prefab"));
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            Scene scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var cg in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CutsceneController>(true)))
                Require(cg.SynchronizeWithMusic == (scene.name == "Cutscene2"), "Unexpected CG synchronization setting in " + scene.name);
            foreach (var sceneRoot in scene.GetRootGameObjects()) CheckSliderBindings(sceneRoot);
            if (PersistentMusicController2D.UsesLocalEndingAudio(scene.name)) continue;
            Require(!scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<AudioManager>(true) != null),
                "Duplicate Audio in " + scene.name);
            foreach (var component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (component == null) continue;
                using var data = new SerializedObject(component);
                var prop = data.GetIterator();
                while (prop.Next(true))
                {
                    if (!prop.propertyPath.EndsWith(".m_Target") || prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.objectReferenceValue is SceneAudioEventRelay2D)
                    {
                        var method = data.FindProperty(prop.propertyPath.Replace(".m_Target", ".m_MethodName"));
                        Require(typeof(SceneAudioEventRelay2D).GetMethod(method.stringValue) != null,
                            "Invalid event relay method in " + scene.name);
                    }
                }
            }
        }
        Debug.Log("PERSISTENT_AUDIO_ASSETS_OK");
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < resumeAt) return;
        try
        {
            if (!checks.MoveNext())
            {
                EditorApplication.update -= Tick;
                Debug.Log("PERSISTENT_AUDIO_RUNTIME_OK");
                EditorApplication.Exit(0);
                return;
            }
            resumeAt = EditorApplication.timeSinceStartup + (checks.Current is float seconds ? seconds : 0.1f);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerator CheckRuntime()
    {
        yield return 0.5f;
        CheckVolumeControls();
        var root = PersistentMusicController2D.Instance;
        Require(root != null, "Direct Cutscene2 launch did not bootstrap audio");
        Require(root.transform.parent == null, "Audio must be a root");
        Require(root.MusicSource.clip != null && root.MusicSource.clip.name == "StartToHorror", "Incorrect CG2 clip");
        Require(!root.MusicSource.loop, "CG2 must not loop");
        var synchronizedCg = UnityEngine.Object.FindAnyObjectByType<CutsceneController>();
        Require(synchronizedCg.SynchronizedPlaybackReady && synchronizedCg.SynchronizedElapsedSeconds >= 0d,
            "CG2 did not start its shared clock");
        MusicVolumeController2D.SetVolume(0.42f);
        SceneManager.LoadScene("Happy_Kitchen");
        yield return 0.5f;
        Require(PersistentMusicController2D.Instance == root, "Audio replaced during load");
        Require(root.MusicSource.clip != null && root.MusicSource.clip.name == "BirthdaySong", "Kitchen cue missing");
        SceneManager.LoadScene("Cutscene2");
        yield return 0.25f;
        Require(root.MusicSource.clip.name == "BirthdaySong", "CG2 did not wait for old track fade");
        Require(root.MusicSource.volume < 1f, "Old track did not fade");
        synchronizedCg = UnityEngine.Object.FindAnyObjectByType<CutsceneController>();
        Require(!synchronizedCg.SynchronizedPlaybackReady, "CG began before previous BGM finished fading");
        using (var data = new SerializedObject(synchronizedCg))
        {
            var overlay = (Image)data.FindProperty("blackOverlay").objectReferenceValue;
            var dialogue = (Text)data.FindProperty("dialogueText").objectReferenceValue;
            Require(overlay.gameObject.activeSelf && overlay.color.a == 1f && dialogue.text.Length == 0,
                "CG must stay black with empty dialogue during music preparation");
        }
        double musicReadyDeadline = Time.realtimeSinceStartupAsDouble + 12d;
        while ((!synchronizedCg.SynchronizedPlaybackReady || synchronizedCg.SynchronizedElapsedSeconds < 0d)
            && Time.realtimeSinceStartupAsDouble < musicReadyDeadline)
            yield return 0f;
        Require(synchronizedCg.SynchronizedPlaybackReady, "CG music preparation timed out");
        Require(root.MusicSource.clip.name == "StartToHorror", "CG2 did not start after fade");
        SceneManager.LoadScene("NM_Bedroom1");
        yield return 0.5f;
        Require(root.MusicSource.clip == null, "Bedroom should have no ordinary BGM");
        Require(Mathf.Approximately(MusicVolumeController2D.CurrentVolume, 0.42f), "Music preference reset");
        Require(Mathf.Approximately(SoundVolumeController2D.CurrentVolume, 0.37f), "Sound preference reset");
        Require(Mathf.Approximately(EnemyVolumeController2D.CurrentVolume, 0.61f), "Enemy preference reset");
        AudioManager.instance.ReportChasing();
        yield return 0.6f;
        var manager = new SerializedObject(AudioManager.instance);
        var chase = (AudioSource)manager.FindProperty("ChaseMusicSource").objectReferenceValue;
        Require(chase.clip != null && chase.volume > 0f, "Chase source failed to fade in");
        SoundEffectManager.instance.ClickSFX();
        SoundEffectManager.instance.PlayRespawnSFX();
        SceneManager.LoadScene("Ending Animation");
        yield return 0.4f;
        Require(PersistentMusicController2D.Instance == root && root.MusicSource.clip == null, "Ending global BGM interference");
        var localManagers = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<AudioManager>(true)).ToArray();
        Require(localManagers.Length > 0, "Ending Timeline's local Audio was destroyed");
        Require(SceneManager.GetActiveScene().GetRootGameObjects()
            .Any(r => r.GetComponentInChildren<PlayableDirector>(true) != null), "Ending Timeline missing");
        foreach (var director in SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<PlayableDirector>(true)))
        {
            if (director.playableAsset == null) continue;
            foreach (var output in director.playableAsset.outputs)
            {
                if (output.outputTargetType != typeof(AudioSource)) continue;
                var source = director.GetGenericBinding(output.sourceObject) as AudioSource;
                Require(source != null && source.outputAudioMixerGroup != null
                    && source.outputAudioMixerGroup.name == "Enemy", "Ending monster/heartbeat Timeline routing incorrect");
            }
        }
        SceneManager.LoadScene("ChooseEnding");
        yield return 0.4f;
        Require(root.MusicSource.clip == null, "ChooseEnding global BGM interference");
        Require(SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<AudioSource>(true))
            .Any(s => s.clip != null && s.clip.name == "HeartBeat" && s.loop
                && s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "Enemy"), "Choice heartbeat lost or incorrectly routed");
        Require(UnityEngine.Object.FindObjectsByType<PersistentMusicController2D>(FindObjectsSortMode.None).Length == 1,
            "Multiple persistent audio roots");
        SceneManager.LoadScene("Cutscene3");
        yield return 0.3f;
        Require(root.MusicSource.clip != null && root.MusicSource.clip.name == "HappyEnding", "Ending exit cue failed");

        // Missing audio is a warning, not an infinite black screen. Change only this runtime scene instance.
        void RemoveCue(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Cutscene2") UnityEngine.Object.FindAnyObjectByType<SceneMusicCue2D>().musicClip = null;
        }
        SceneManager.sceneLoaded += RemoveCue;
        SceneManager.LoadScene("Cutscene2");
        yield return 0.3f;
        SceneManager.sceneLoaded -= RemoveCue;
        synchronizedCg = UnityEngine.Object.FindAnyObjectByType<CutsceneController>();
        Require(synchronizedCg.SynchronizedPlaybackReady && synchronizedCg.SynchronizedElapsedSeconds > 0d,
            "Missing music did not fall back to realtime");
        Require(root.MusicSource.clip == null, "Missing cue retained stale BGM");

        // Cancel an actual future start, then allow enough time for it to have fired.
        var cue = UnityEngine.Object.FindAnyObjectByType<SceneMusicCue2D>();
        cue.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/StartToHorror.wav");
        cue.fadeOutDuration = 0f;
        var scheduled = root.PrepareSynchronizedMusic(cue);
        while (!scheduled.IsReady) yield return 0f;
        Require(scheduled.IsReady && scheduled.Elapsed < 0d, "Expected a future scheduled start");
        SceneManager.LoadScene("NM_Bedroom1");
        yield return 0.4f;
        Require(scheduled.IsCancelled && root.MusicSource.clip == null && !root.MusicSource.isPlaying,
            "Leaving scene failed to cancel scheduled CG music");
        Debug.Log("CG_SYNC_STARTUP_FALLBACK_CANCELLATION_OK");
    }

    private static void CheckRouting(GameObject prefab)
    {
        void Check(UnityEngine.Object owner, string field, string expected)
        {
            using var data = new SerializedObject(owner);
            var source = data.FindProperty(field).objectReferenceValue as AudioSource;
            Require(source != null && source.outputAudioMixerGroup != null
                && source.outputAudioMixerGroup.name == expected, prefab.name + "/" + field + " should use " + expected);
        }
        var music = prefab.GetComponent<AudioManager>();
        Check(music, "BGMSource", "Music");
        Check(music, "ChaseMusicSource", "Music");
        Check(music, "EnemySpotAudioSource", "Enemy");
        var sfx = prefab.GetComponent<SoundEffectManager>();
        foreach (string field in new[] { "AudioSource", "HoverAudioSource", "WalkAudioSource" }) Check(sfx, field, "Sound");
        foreach (string field in new[] { "DadWalkAudioSource", "MomWalkAudioSource", "JumpscareAudioSource", "HeartbeatAudioSource" }) Check(sfx, field, "Enemy");
        foreach (var ambient in prefab.GetComponentsInChildren<RandomSoundPlayer>(true)) Check(ambient, "audioSource", "Enemy");
    }

    private static void CheckSliderBindings(GameObject root)
    {
        foreach (var slider in root.GetComponentsInChildren<Slider>(true))
        {
            if (slider.name != "Sound Slider" && slider.name != "Music Slider") continue;
            int handlers = (slider.GetComponent<MusicVolumeSlider2D>() != null ? 1 : 0)
                + (slider.GetComponent<SoundVolumeSlider2D>() != null ? 1 : 0)
                + (slider.GetComponent<EnemyVolumeSlider2D>() != null ? 1 : 0);
            Require(handlers == 1, "Slider must have exactly one volume handler: " + root.name + "/" + slider.name);
            Require(slider.onValueChanged.GetPersistentEventCount() == 0, "Duplicate legacy volume callback: " + root.name);
        }
    }

    private static void CheckVolumeControls()
    {
        var mixer = Resources.Load<AudioMixer>("GameplayAudio");
        var options = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/OptionCanva.prefab"));
        try
        {
            var music = options.GetComponentInChildren<MusicVolumeSlider2D>(true).GetComponent<Slider>();
            var sound = options.GetComponentInChildren<SoundVolumeSlider2D>(true).GetComponent<Slider>();
            var enemy = options.GetComponentInChildren<EnemyVolumeSlider2D>(true).GetComponent<Slider>();
            foreach (var slider in new[] { music, sound, enemy })
                for (Transform t = slider.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            MusicVolumeController2D.SetVolume(1);
            SoundVolumeController2D.SetVolume(1);
            EnemyVolumeController2D.SetVolume(1);
            var sliders = new[] { music, sound, enemy };
            var parameters = new[] { "MusicVolume", "SoundVolume", "EnemyVolume" };
            for (int i = 0; i < sliders.Length; i++)
            {
                sliders[i].value = 0;
                for (int j = 0; j < parameters.Length; j++)
                {
                    Require(mixer.GetFloat(parameters[j], out float db), "Missing mixer parameter " + parameters[j]);
                    Require(Mathf.Abs(db - (i == j ? -80f : 0f)) < 0.01f, "Volume categories interfere: " + parameters[j]);
                }
                sliders[i].value = 1;
            }
            music.value = 0.42f;
            sound.value = 0.37f;
            enemy.value = 0.61f;
            options.SetActive(false);
            options.SetActive(true);
            Require(Mathf.Approximately(music.value, 0.42f) && Mathf.Approximately(sound.value, 0.37f)
                && Mathf.Approximately(enemy.value, 0.61f), "Reopening options reset sliders");
            Debug.Log("AUDIO_VOLUME_ROUTING_OK");
        }
        finally { UnityEngine.Object.Destroy(options); }
    }
}
