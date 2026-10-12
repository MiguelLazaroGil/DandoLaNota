
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[System.Serializable]
public class MusicianAudio
{
    public string name;
    public EventReference audioEvent;

    [Header("3D Audio Position")]
    public Transform musicianTransform;

    [HideInInspector] public EventInstance instance;
    [HideInInspector] public float currentPitch = 1f;
    [HideInInspector] public float targetPitch = 1f;
}

public class OrchestraAudioManager : MonoBehaviour
{
    [Header("Score System")]
    [SerializeField] private MusicScoreManager musicScoreManager;

    [Header("Instruments")]
    [SerializeField] private List<MusicianAudio> musicians = new();

    [Header("Desynchronization")]
    [SerializeField, Range(0f, 0.5f)]
    private float maxDeviation = 0.20f;

    [SerializeField, Min(0.1f)]
    private float changeInterval = 2f;

    [SerializeField, Min(0.1f)]
    private float pitchSmoothing = 1.5f;

    private float changeTimer;
    private bool isPlaying;

    private void Start()
    {
        StartOrchestra();
    }

    private void Update()
    {
        if (!isPlaying || musicScoreManager == null)
            return;

        changeTimer += Time.deltaTime;

        if (changeTimer >= changeInterval)
        {
            changeTimer = 0f;
            CalculateNewPitches();
        }

        UpdatePitches();
    }

    private void StartOrchestra()
    {
        if (musicScoreManager == null)
        {
            Debug.LogError("MusicScoreManager is missing.");
            return;
        }

        foreach (MusicianAudio musician in musicians)
        {
            if (musician.audioEvent.IsNull)
                continue;

            musician.instance = RuntimeManager.CreateInstance(musician.audioEvent);

            if (musician.musicianTransform != null)
            {
                RuntimeManager.AttachInstanceToGameObject(musician.instance,musician.musicianTransform.gameObject);
            }

            musician.currentPitch = 1f;
            musician.targetPitch = 1f;

            musician.instance.setPitch(1f);
            musician.instance.start();
        }

        isPlaying = true;
        CalculateNewPitches();
    }

    private void CalculateNewPitches()
    {
        float score = musicScoreManager.CurrentRhythm;

        // 100 = perfect synchronization.
        // 0 and 200 = maximum deviation.
        float desynchronization =
            Mathf.Abs(score - 100f) / 100f;

        float deviation = desynchronization * maxDeviation;

        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            musician.targetPitch = Random.Range(
                1f - deviation,
                1f + deviation
            );
        }
    }

    private void UpdatePitches()
    {
        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            musician.currentPitch = Mathf.MoveTowards(
                musician.currentPitch,
                musician.targetPitch,
                pitchSmoothing * Time.deltaTime
            );

            musician.instance.setPitch(musician.currentPitch);
        }
    }

    // Call this when a valid conductor baton beat is detected.
    public void SynchronizeOrchestra()
    {
        if (!isPlaying)
            return;

        // Use the first valid event as the timeline reference.
        EventInstance reference = default;

        foreach (MusicianAudio musician in musicians)
        {
            if (musician.instance.isValid())
            {
                reference = musician.instance;
                break;
            }
        }

        if (!reference.isValid())
            return;

        if (reference.getTimelinePosition(out int position)
            != FMOD.RESULT.OK)
            return;

        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            musician.instance.setTimelinePosition(position);
        }

        // Reset playback speeds so that
        // all musicians become synchronized again.
        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            musician.currentPitch = 1f;
            musician.targetPitch = 1f;
            musician.instance.setPitch(1f);
        }

        changeTimer = 0f;

        Debug.Log("Orchestra synchronized.");
    }

    private void OnDestroy()
    {
        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            musician.instance.stop(
                FMOD.Studio.STOP_MODE.IMMEDIATE
            );

            musician.instance.release();
        }
    }
}
