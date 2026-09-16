using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any GameObject to have it randomly play one of several sounds
/// at randomized intervals, each with its own independent chance of playing.
/// Useful for ambient horror SFX: creaks, whispers, drips, distant noises, etc.
/// Volume is controlled by the mixer group assigned to this component's AudioSource.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RandomSoundPlayer : MonoBehaviour
{
    [System.Serializable]
    public class RandomSoundEntry
    {
        public AudioClip clip;

        [Tooltip("Chance (0-1) this specific sound plays when it's this entry's turn to be rolled.")]
        [Range(0f, 1f)] public float probability = 1f;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Pitch is randomized between these two values each time this clip plays. Set both to 1 for no variation.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    }

    [Header("Sounds")]
    [SerializeField] private List<RandomSoundEntry> sounds = new List<RandomSoundEntry>();

    [Header("Timing")]
    [Tooltip("A new random wait time (between Min and Max) is picked after each check.")]
    [SerializeField] private float minInterval = 5f;
    [SerializeField] private float maxInterval = 15f;
    [SerializeField] private bool playOnStart = true;

    [Header("Output")]
    [SerializeField] private AudioSource audioSource;

    private float timer;
    private readonly List<RandomSoundEntry> candidateBuffer = new List<RandomSoundEntry>();

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (playOnStart)
            ScheduleNextCheck();
        else
            timer = float.MaxValue;
    }

    private void Update()
    {
        if (sounds.Count == 0)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            TryPlayRandomSound();
            ScheduleNextCheck();
        }
    }

    /// <summary>Call this if you want to force a fresh interval roll (e.g. after enabling this component at runtime).</summary>
    public void ScheduleNextCheck()
    {
        timer = Random.Range(minInterval, maxInterval);
    }

    private void TryPlayRandomSound()
    {
        candidateBuffer.Clear();

        foreach (RandomSoundEntry entry in sounds)
        {
            if (entry.clip == null)
                continue;

            if (Random.value <= entry.probability)
                candidateBuffer.Add(entry);
        }

        // It's fine for nothing to play this tick — that's part of the randomness (natural silence gaps).
        if (candidateBuffer.Count == 0)
            return;

        RandomSoundEntry chosen = candidateBuffer[Random.Range(0, candidateBuffer.Count)];
        audioSource.pitch = Random.Range(chosen.pitchRange.x, chosen.pitchRange.y);

        audioSource.PlayOneShot(chosen.clip, chosen.volume);
    }
}
