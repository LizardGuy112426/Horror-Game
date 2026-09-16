using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit one-time migration; never runs automatically on editor startup.</summary>
public static class PersistentAudioAuthoring
{
    public const string AudioPath = "Assets/Resources/PersistentAudioSystem.prefab";
    private static readonly Dictionary<string, string> Music = new()
    {
        { "MainMenu", "MainMenu.wav" }, { "Cutscene", "First.wav" },
        { "Cutscene2", "StartToHorror.wav" }, { "Cutscene3", "HappyEnding.wav" },
        { "NewsIntro", "BadEnding.wav" }, { "Happy_Kitchen", "BirthdaySong.wav" },
        { "Void", "Void.wav" }
    };

    [MenuItem("Tools/Horror Game/Migrate Persistent Audio")]
    public static void Migrate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        CreatePrefab();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (PersistentMusicController2D.UsesLocalEndingAudio(name)) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true))
                .Where(c => c != null).ToArray();
            var roots = components.OfType<AudioManager>().Select(c => c.gameObject)
                .Concat(components.OfType<SoundEffectManager>().Select(c => c.gameObject)).Distinct().ToArray();
            var removed = new HashSet<Object>();
            foreach (GameObject root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    removed.Add(t.gameObject);
                    foreach (Component c in t.GetComponents<Component>()) if (c != null) removed.Add(c);
                }
            }
            SceneAudioEventRelay2D relay = null;
            var musicSources = new List<AudioSource>();
            if (Music.TryGetValue(name, out string clipName))
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + clipName);
                if (clip == null) throw new InvalidOperationException("Missing " + clipName);
                musicSources.AddRange(components.OfType<AudioSource>()
                    .Where(s => !removed.Contains(s) && s.clip == clip));
                var cue = components.OfType<SceneMusicCue2D>().FirstOrDefault();
                if (cue == null)
                {
                    cue = new GameObject("Scene Music").AddComponent<SceneMusicCue2D>();
                    cue.musicClip = clip;
                    cue.loop = musicSources.Count > 0 ? musicSources[0].loop : name == "MainMenu";
                    cue.volume = musicSources.Count > 0 ? musicSources[0].volume : 1f;
                    cue.fadeOutDuration = name == "Cutscene2" ? 1f : 0f;
                    cue.fadeInDuration = 0f;
                    cue.restartOnEnter = true;
                }
                foreach (var source in musicSources) removed.Add(source);
            }
            // Retarget persistent UnityEvents before deleting their original scene objects.
            foreach (Component component in components)
            {
                if (removed.Contains(component)) continue;
                using var data = new SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object target = property.objectReferenceValue;
                    if (target == null) continue;
                    if (target is AudioManager || target is SoundEffectManager)
                    {
                        if (!property.propertyPath.EndsWith(".m_Target"))
                            throw new InvalidOperationException($"Review typed audio reference: {path}/{component.name}/{property.propertyPath}");
                        if (relay == null) relay = new GameObject("Audio Events").AddComponent<SceneAudioEventRelay2D>();
                        string prefix = property.propertyPath.Substring(0, property.propertyPath.Length - "m_Target".Length);
                        var method = data.FindProperty(prefix + "m_MethodName");
                        if (target is AudioManager && method.stringValue == "ChangeVolume")
                            method.stringValue = "ChangeMusicVolume";
                        if (typeof(SceneAudioEventRelay2D).GetMethod(method.stringValue) == null)
                            throw new InvalidOperationException("Missing relay method " + method.stringValue);
                        property.objectReferenceValue = relay;
                        data.FindProperty(prefix + "m_TargetAssemblyTypeName").stringValue = typeof(SceneAudioEventRelay2D).AssemblyQualifiedName;
                    }
                    else if (removed.Contains(target))
                    {
                        if (component is MainMenuControl && property.propertyPath == "NormalAudioSource")
                            property.objectReferenceValue = null; // Unused legacy field.
                        else throw new InvalidOperationException($"Review audio binding before removal: {path}/{component.name}/{property.propertyPath}");
                    }
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (AudioSource source in musicSources) Object.DestroyImmediate(source);
            foreach (GameObject root in roots) Object.DestroyImmediate(root);
            if (roots.Length > 0 || Music.ContainsKey(name) || relay != null)
                EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("PERSISTENT_AUDIO_MIGRATION_OK");
    }

    private static void CreatePrefab()
    {
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Audio.prefab");
        GameObject root = Object.Instantiate(original);
        root.name = "PersistentAudioSystem";
        try
        {
            var controller = root.AddComponent<PersistentMusicController2D>();
            var manager = root.GetComponent<AudioManager>();
            var sfx = root.GetComponent<SoundEffectManager>();
            var managerData = new SerializedObject(manager);
            AudioSource bgm = (AudioSource)managerData.FindProperty("BGMSource").objectReferenceValue;
            Set(controller, "musicSource", bgm);
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            bgm.clip = null;
            var sfxData = new SerializedObject(sfx);
            AudioSource template = (AudioSource)sfxData.FindProperty("AudioSource").objectReferenceValue;
            foreach (string field in new[] { "HoverAudioSource", "WalkAudioSource" })
            {
                var source = new GameObject(field).AddComponent<AudioSource>();
                source.transform.SetParent(root.transform, false);
                source.outputAudioMixerGroup = template.outputAudioMixerGroup;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                Set(sfx, field, source);
            }
            Set(sfx, "RespawnSFX", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Respawn.wav"));
            foreach (RandomSoundPlayer ambient in root.GetComponentsInChildren<RandomSoundPlayer>(true)) ambient.enabled = false;
            ConfigureMixerRouting(root);
            PrefabUtility.SaveAsPrefabAsset(root, AudioPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void ConfigureMixerRouting(GameObject root)
    {
        var mixer = Resources.Load<AudioMixer>("GameplayAudio");
        if (mixer == null) throw new InvalidOperationException("Missing GameplayAudio mixer");
        void Route(Object owner, string field, string groupName)
        {
            using var data = new SerializedObject(owner);
            var source = data.FindProperty(field).objectReferenceValue as AudioSource;
            if (source == null) throw new InvalidOperationException("Missing audio source " + field);
            source.outputAudioMixerGroup = mixer.FindMatchingGroups(groupName).Single(g => g.name == groupName);
        }
        var music = root.GetComponent<AudioManager>();
        Route(music, "BGMSource", "Music");
        Route(music, "ChaseMusicSource", "Music");
        Route(music, "EnemySpotAudioSource", "Enemy");
        var sfx = root.GetComponent<SoundEffectManager>();
        foreach (string field in new[] { "AudioSource", "HoverAudioSource", "WalkAudioSource" })
            Route(sfx, field, "Sound");
        foreach (string field in new[] { "DadWalkAudioSource", "MomWalkAudioSource", "JumpscareAudioSource", "HeartbeatAudioSource" })
            Route(sfx, field, "Enemy");
        foreach (var ambient in root.GetComponentsInChildren<RandomSoundPlayer>(true))
            Route(ambient, "audioSource", "Enemy");
    }

    private static void Set(Object target, string field, Object value)
    {
        using var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
