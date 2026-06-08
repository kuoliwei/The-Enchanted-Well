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

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        baseVolume = audioSource.volume;
    }

    private void OnEnable()
    {
        catManager.OnAnyoneConfirmedPresent += HandleAnyoneConfirmedPresent;
        catManager.OnEveryoneLeft += HandleEveryoneLeft;
    }

    private void OnDisable()
    {
        catManager.OnAnyoneConfirmedPresent -= HandleAnyoneConfirmedPresent;
        catManager.OnEveryoneLeft -= HandleEveryoneLeft;
    }

    private void HandleAnyoneConfirmedPresent()
    {
        if (state != State.Idle) return;

        state = State.Playing;
        StopActiveCoroutine();
        activeCoroutine = StartCoroutine(PlayThenFadeOut());
    }

    private void HandleEveryoneLeft()
    {
        switch (state)
        {
            case State.Playing:
                StopActiveCoroutine();
                activeCoroutine = StartCoroutine(FadeOutAndStop());
                break;

            case State.WaitingForClear:
                state = State.Idle;
                break;
        }
    }

    private IEnumerator PlayThenFadeOut()
    {
        audioSource.clip = rippleClip;
        audioSource.volume = baseVolume;
        audioSource.Play();

        yield return new WaitForSeconds(playDuration);

        activeCoroutine = StartCoroutine(FadeOutAndStop());
    }

    private IEnumerator FadeOutAndStop()
    {
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
