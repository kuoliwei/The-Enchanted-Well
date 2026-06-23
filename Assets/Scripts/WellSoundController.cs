using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WellSoundController : MonoBehaviour
{
    private enum State
    {
        Idle,           // no one present, ready to trigger
        Playing,        // sound is playing at full volume
        WaitingForClear // sound has stopped (timeout or early leave), waiting for everyone to leave before re-arming
    }

    [Header("Detection Source")]
    [SerializeField] private CatManager catManager;

    [Header("Clip")]
    [SerializeField] private AudioClip rippleClip;

    [Header("Playback Settings")]
    [SerializeField] private float playDuration = 10f;
    [SerializeField] private float fadeOutDuration;

    private AudioSource audioSource;
    private State state = State.Idle;
    private Coroutine activeCoroutine;
    private float baseVolume;
    private bool pendingIdleAfterFade = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        baseVolume = audioSource.volume;
    }

    private void OnEnable()
    {
        catManager.OnAnyoneConfirmedPresent += HandleAnyoneConfirmedPresent;
        catManager.OnEveryonePermanentlyLeft += HandleEveryonePermanentlyLeft;
    }

    private void OnDisable()
    {
        catManager.OnAnyoneConfirmedPresent -= HandleAnyoneConfirmedPresent;
        catManager.OnEveryonePermanentlyLeft -= HandleEveryonePermanentlyLeft;
    }

    private void HandleAnyoneConfirmedPresent()
    {
        Debug.Log($"[Sound] OnAnyoneConfirmedPresent triggered, current state: {state}");
        if (state != State.Idle) return;

        state = State.Playing;
        Debug.Log($"[Sound] State changed to Playing, starting PlayThenFadeOut");
        StopActiveCoroutine();
        activeCoroutine = StartCoroutine(PlayThenFadeOut());
    }

    private void HandleEveryonePermanentlyLeft()
    {
        Debug.Log($"[Sound] OnEveryonePermanentlyLeft triggered, current state: {state}");
        switch (state)
        {
            case State.Playing:
                Debug.Log($"[Sound] State is Playing, stopping current coroutine and starting FadeOutAndStop");
                pendingIdleAfterFade = true;
                StopActiveCoroutine();
                activeCoroutine = StartCoroutine(FadeOutAndStop());
                break;

            case State.WaitingForClear:
                Debug.Log($"[Sound] State is WaitingForClear, changing to Idle");
                state = State.Idle;
                break;

            case State.Idle:
                Debug.Log($"[Sound] State is already Idle, no action needed");
                break;
        }
    }

    private IEnumerator PlayThenFadeOut()
    {
        Debug.Log($"[Sound] PlayThenFadeOut START");
        audioSource.clip = rippleClip;
        audioSource.volume = baseVolume;
        audioSource.Play();

        yield return new WaitForSeconds(playDuration);

        Debug.Log($"[Sound] PlayThenFadeOut END (10s timeout), starting FadeOutAndStop");
        activeCoroutine = StartCoroutine(FadeOutAndStop());
    }

    private IEnumerator FadeOutAndStop()
    {
        Debug.Log($"[Sound] FadeOutAndStop START, state changing to WaitingForClear");
        state = State.WaitingForClear;

        float startVolume = audioSource.volume;
        float elapsed = 0f;
        while (fadeOutDuration > 0f && elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        audioSource.Stop();
        audioSource.volume = baseVolume;
        activeCoroutine = null;

        if (pendingIdleAfterFade)
        {
            pendingIdleAfterFade = false;
            state = State.Idle;
            Debug.Log($"[Sound] FadeOutAndStop was triggered by permanent departure, state changing straight to Idle");
        }

        Debug.Log($"[Sound] FadeOutAndStop END, state is now: {state}");
    }

    private void StopActiveCoroutine()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }
}
