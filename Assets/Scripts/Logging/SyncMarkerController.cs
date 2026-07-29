using System.Collections;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Generates short sync beeps in code and writes START/END sync markers for video alignment.
/// </summary>
public class SyncMarkerController : MonoBehaviour
{
    private const string GeneratedObjectName = "SyncMarkerController";

    [Header("Beep")]
    [SerializeField] private bool playAudioBeeps = true;
    [SerializeField] private float beepFrequencyHz = 1000f;
    [SerializeField] private float beepDurationSeconds = 0.2f;
    [SerializeField] private float beepVolume = 1.0f;
    [SerializeField] private int sampleRate = 44100;
    [SerializeField] private float endBeepGapSeconds = 0.08f;

    private Logger logger;
    private AudioSource audioSource;
    private AudioClip beepClip;
    private Coroutine beepSequenceCoroutine;

    public static SyncMarkerController GetOrCreate()
    {
        SyncMarkerController existing = FindObjectOfType<SyncMarkerController>();
        if (existing != null)
        {
            return existing;
        }

        GameObject markerObject = new GameObject(GeneratedObjectName);
        return markerObject.AddComponent<SyncMarkerController>();
    }

    public void PlayCountdownBeep()
    {
        PlayBeep();
    }

    public void MarkStart(Logger explicitLogger, InteractionCondition condition, string details)
    {
        LogMarker("START", explicitLogger, condition, details);
    }

    public void MarkEnd(Logger explicitLogger, InteractionCondition condition, string details)
    {
        LogMarker("END", explicitLogger, condition, details);
        PlayBeepSequence(3);
    }

    private void Awake()
    {
        EnsureAudioSource();
    }

    private void OnDestroy()
    {
        if (beepClip != null)
        {
            Destroy(beepClip);
            beepClip = null;
        }
    }

    private void LogMarker(string markerName, Logger explicitLogger, InteractionCondition condition, string details)
    {
        float timestamp = Time.time;
        Logger resolvedLogger = explicitLogger != null ? explicitLogger : ResolveLogger();
        if (resolvedLogger != null)
        {
            resolvedLogger.LogSyncMarker(markerName, condition, details, timestamp);
            return;
        }

        Debug.Log($"SyncMarker, {markerName}, {timestamp.ToString("0.000", CultureInfo.InvariantCulture)}");
    }

    private void PlayBeep()
    {
        if (!playAudioBeeps)
        {
            return;
        }

        EnsureAudioSource();
        EnsureBeepClip();
        if (audioSource != null && beepClip != null)
        {
            audioSource.PlayOneShot(beepClip, Mathf.Clamp01(beepVolume));
        }
    }

    private void PlayBeepSequence(int count)
    {
        if (!playAudioBeeps || count <= 0)
        {
            return;
        }

        if (beepSequenceCoroutine != null)
        {
            StopCoroutine(beepSequenceCoroutine);
        }

        beepSequenceCoroutine = StartCoroutine(PlayBeepSequenceCoroutine(count));
    }

    private IEnumerator PlayBeepSequenceCoroutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            PlayBeep();
            if (i < count - 1)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, beepDurationSeconds) + Mathf.Max(0f, endBeepGapSeconds));
            }
        }

        beepSequenceCoroutine = null;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1.0f;
        audioSource.priority = 0;
    }

    private void EnsureBeepClip()
    {
        if (beepClip != null)
        {
            return;
        }

        int resolvedSampleRate = Mathf.Clamp(sampleRate, 8000, 96000);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, beepDurationSeconds) * resolvedSampleRate));
        float frequency = Mathf.Max(20f, beepFrequencyHz);
        float[] samples = new float[sampleCount];
        int fadeSampleCount = Mathf.Min(sampleCount / 2, Mathf.CeilToInt(0.01f * resolvedSampleRate));

        for (int i = 0; i < sampleCount; i++)
        {
            float envelope = 1f;
            if (fadeSampleCount > 0 && i < fadeSampleCount)
            {
                envelope = i / (float)fadeSampleCount;
            }
            else if (fadeSampleCount > 0 && i >= sampleCount - fadeSampleCount)
            {
                envelope = (sampleCount - i - 1) / (float)fadeSampleCount;
            }

            float phase = 2f * Mathf.PI * frequency * i / resolvedSampleRate;
            samples[i] = Mathf.Sin(phase) * envelope;
        }

        beepClip = AudioClip.Create("GeneratedSyncBeep", sampleCount, 1, resolvedSampleRate, false);
        beepClip.SetData(samples, 0);
    }

    private Logger ResolveLogger()
    {
        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }

        return logger;
    }
}
