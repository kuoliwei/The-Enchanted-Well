using UnityEngine;
using UnityEngine.Video;
using System;

/// <summary>
/// Unified interface for cat animation playback (VideoPlayer or FrameSequence)
/// </summary>
public interface ICatFrameSource
{
    /// <summary>
    /// Play a clip/sequence by name
    /// </summary>
    void Play(string clipName);

    /// <summary>
    /// Stop playback
    /// </summary>
    void Stop();

    /// <summary>
    /// Whether playback is currently active
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Fired when clip/sequence reaches the end
    /// </summary>
    event Action OnClipFinished;

    /// <summary>
    /// Enable/disable looping behavior
    /// </summary>
    bool IsLooping { get; set; }
}
