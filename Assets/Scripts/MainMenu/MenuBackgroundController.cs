using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Controls the main menu background video transitions.
/// Attach this to the GameObject that holds your VideoPlayer.
/// </summary>
public class MenuBackgroundController : MonoBehaviour
{
    [System.Serializable]
    public class ButtonVideoSet
    {
        [Tooltip("Must match the 'Button Id' set on the MenuButtonHoverTrigger script")]
        public string buttonId;

        [Header("Hover In (Default -> Button)")]
        public VideoClip hoverInClip;
        [Range(0.1f, 3f)] public float hoverInSpeed = 1f;

        [Header("Hover Out (Button -> Default)")]
        public VideoClip hoverOutClip;
        [Range(0.1f, 3f)] public float hoverOutSpeed = 1f;
    }

    [Header("References")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Default idle state")]
    [Tooltip("Optional looping clip shown when nothing is hovered. Leave empty to just freeze on last frame of hover-out video.")]
    [SerializeField] private VideoClip defaultIdleClip;
    [SerializeField] private bool defaultIdleLoops = true;

    [Header("Per-Button Video Data")]
    [SerializeField] private List<ButtonVideoSet> buttonVideos = new List<ButtonVideoSet>();

    private Dictionary<string, ButtonVideoSet> _lookup;
    private string _currentHoveredId = null;

    private void Awake()
    {
        _lookup = new Dictionary<string, ButtonVideoSet>();
        foreach (var set in buttonVideos)
        {
            if (!string.IsNullOrEmpty(set.buttonId) && !_lookup.ContainsKey(set.buttonId))
                _lookup.Add(set.buttonId, set);
        }

        videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void Start()
    {
        PlayDefaultIdle();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    public void OnButtonHoverEnter(string buttonId)
    {
        if (!_lookup.TryGetValue(buttonId, out var set) || set.hoverInClip == null)
            return;

        _currentHoveredId = buttonId;
        PlayClip(set.hoverInClip, set.hoverInSpeed, loop: false, freezeOnLastFrame: true);
    }

    public void OnButtonHoverExit(string buttonId)
    {
        // Ignore if the player already moved to hover a different button
        if (_currentHoveredId != buttonId)
            return;

        _currentHoveredId = null;

        if (_lookup.TryGetValue(buttonId, out var set) && set.hoverOutClip != null)
        {
            PlayClip(set.hoverOutClip, set.hoverOutSpeed, loop: false, freezeOnLastFrame: defaultIdleClip == null);
        }
        else
        {
            PlayDefaultIdle();
        }
    }

    private void PlayDefaultIdle()
    {
        if (defaultIdleClip != null)
        {
            PlayClip(defaultIdleClip, 1f, loop: defaultIdleLoops, freezeOnLastFrame: !defaultIdleLoops);
        }
    }

    private void PlayClip(VideoClip clip, float speed, bool loop, bool freezeOnLastFrame)
    {
        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.playbackSpeed = speed;
        videoPlayer.isLooping = loop;
        videoPlayer.Play();

        // Tag whether this particular play-through should freeze at the end
        _freezeOnLastFrame = freezeOnLastFrame;
    }

    private bool _freezeOnLastFrame;

    private void OnVideoFinished(VideoPlayer source)
    {
        if (_freezeOnLastFrame)
        {
            source.Pause(); // Holds on the last rendered frame
        }
        else if (_currentHoveredId == null)
        {
            // A hover-out finished and no idle clip was set/loop disabled elsewhere
            PlayDefaultIdle();
        }
    }
}