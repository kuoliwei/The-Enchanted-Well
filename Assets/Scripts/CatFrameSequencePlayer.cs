using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// Plays cat animation from PNG frame sequences instead of VideoPlayer.
/// Each frame sequence lives in its own folder (e.g., Assets/VideoClips/cat_name_frames/).
///
/// Mirrors VideoPlayer's role in the existing pipeline: plays ONE named sequence
/// start-to-finish and fires OnClipFinished when done. Playlist traversal (which
/// sequence comes next, whether to loop back to the first) stays owned by
/// CatVideoPlayerController, exactly like it already owns that logic for VideoPlayer
/// via loopPointReached. IsLooping therefore defaults to false, same as vp.isLooping = false.
/// </summary>
public class CatFrameSequencePlayer : MonoBehaviour, ICatFrameSource
{
    [Header("Frame Sequence Settings")]
    [SerializeField] private float fps = 15f;

    private RawImage rawImage;
    private Dictionary<string, Texture2D[]> frameCache = new Dictionary<string, Texture2D[]>();
    private string currentSequenceName;
    private int currentFrameIndex = 0;
    private float frameTimer = 0f;
    private bool isPlaying = false;
    private bool isLooping = false;

    public event Action OnClipFinished;

    public bool IsPlaying => isPlaying;
    public bool IsLooping
    {
        get => isLooping;
        set => isLooping = value;
    }

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        if (rawImage == null)
        {
            Debug.LogError("[CatFrameSequencePlayer] RawImage component not found on this GameObject");
        }
    }

    /// <summary>
    /// Eagerly load every given sequence into the cache up front (synchronous).
    /// Call this once when a cat spawns, passing its FULL playlist, so the
    /// expensive Resources.LoadAll cost for later clips is paid immediately
    /// (while the cat is popping up) instead of mid-playback at the exact
    /// moment a clip transition happens — a synchronous multi-hundred-frame
    /// texture load there is long enough to visibly flash black on Windows,
    /// which looks identical to the old VideoPlayer loop-black-flash bug
    /// even though the underlying cause is completely different.
    /// </summary>
    public void PreloadSequences(string[] sequenceNames)
    {
        if (sequenceNames == null)
            return;

        float startTime = Time.realtimeSinceStartup;

        foreach (var name in sequenceNames)
        {
            if (string.IsNullOrEmpty(name))
                continue;

            if (!frameCache.ContainsKey(name))
                LoadFrameSequence(name);
        }

        float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
        Debug.Log($"[CatFrameSequencePlayer] PreloadSequences 耗時 {elapsedMs:F0} ms（{sequenceNames.Length} 段序列）");
    }

    /// <summary>
    /// Play a frame sequence by name (folder name without _frames suffix)
    /// E.g., "三花" will load from "Assets/VideoClips/三花_frames/"
    /// </summary>
    public void Play(string sequenceName)
    {
        if (string.IsNullOrEmpty(sequenceName))
        {
            Debug.LogError("[CatFrameSequencePlayer] Sequence name is empty");
            return;
        }

        currentSequenceName = sequenceName;
        currentFrameIndex = 0;
        frameTimer = 0f;
        isPlaying = true;

        // Load frames if not cached
        if (!frameCache.ContainsKey(sequenceName))
        {
            LoadFrameSequence(sequenceName);
        }

        // Display first frame
        if (frameCache.ContainsKey(sequenceName) && frameCache[sequenceName].Length > 0)
        {
            rawImage.texture = frameCache[sequenceName][0];
        }
    }

    public void Stop()
    {
        isPlaying = false;
        currentFrameIndex = 0;
        frameTimer = 0f;
    }

    private void Update()
    {
        if (!isPlaying || !frameCache.ContainsKey(currentSequenceName))
            return;

        Texture2D[] frames = frameCache[currentSequenceName];
        if (frames.Length == 0)
            return;

        frameTimer += Time.deltaTime;
        float frameInterval = 1f / fps;

        if (frameTimer >= frameInterval)
        {
            frameTimer -= frameInterval;
            currentFrameIndex++;

            if (currentFrameIndex >= frames.Length)
            {
                if (isLooping)
                {
                    currentFrameIndex = 0;
                }
                else
                {
                    isPlaying = false;
                    OnClipFinished?.Invoke();
                    return;
                }
            }

            rawImage.texture = frames[currentFrameIndex];
        }
    }

    private void LoadFrameSequence(string sequenceName)
    {
        string folderPath = $"VideoClips/{sequenceName}_frames";
        Texture2D[] frames = Resources.LoadAll<Texture2D>(folderPath);

        if (frames.Length == 0)
        {
            Debug.LogError($"[CatFrameSequencePlayer] No frames found in {folderPath}. " +
                $"Make sure PNG files are in Assets/Resources/{folderPath}/");
            return;
        }

        // Sort frames by name to ensure correct order (0001.png, 0002.png, etc.)
        System.Array.Sort(frames, (a, b) => a.name.CompareTo(b.name));

        frameCache[sequenceName] = frames;
        Debug.Log($"[CatFrameSequencePlayer] Loaded {frames.Length} frames from {folderPath}");
    }

    /// <summary>
    /// Clear cached frames (useful for memory management if switching between many sequences)
    /// </summary>
    public void ClearCache()
    {
        frameCache.Clear();
    }
}
