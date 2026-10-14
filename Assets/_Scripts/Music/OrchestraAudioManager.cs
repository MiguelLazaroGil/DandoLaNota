
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
    public static OrchestraAudioManager Instance { get; private set; }

    [Header("Score System")]
    [SerializeField] private MusicScoreManager musicScoreManager;

    [Header("Music Clock")]
    [SerializeField] private MusicClockFMOD musicClock;

    [Header("Instruments")]
    [SerializeField] private List<MusicianAudio> musicians = new();

    [Header("Desynchronization")]
    [SerializeField, Range(0f, 0.5f)]
    private float maxDeviation = 0.20f;

    [SerializeField, Min(0.1f)]
    private float changeInterval = 2f;

    [SerializeField, Min(0.1f)]
    private float pitchSmoothing = 1.5f;

    [Header("Progressive Synchronization")]
    [Tooltip("Duración exacta del loop musical en segundos.")]
    [SerializeField, Min(0.1f)]
    private float loopDuration = 19.2f;

    [Tooltip("Corrección de velocidad por cada segundo de desfase.")]
    [SerializeField, Min(0f)]
    private float synchronizationStrength = 0.35f;

    [Tooltip("Máxima variación de pitch para corregir el desfase.")]
    [SerializeField, Range(0f, 0.2f)]
    private float maxSynchronizationPitch = 0.08f;

    [Tooltip("Distancia respecto a 100 a partir de la cual comienza la recuperación.")]
    [SerializeField, Range(1f, 100f)]
    private float synchronizationScoreRange = 50f;

    [Tooltip("Margen temporal considerado sincronizado.")]
    [SerializeField, Min(0f)]
    private float synchronizationDeadZone = 0.015f;

    private float changeTimer;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    // Mantiene la sintaxis que ya utilizabas.
    public bool readytoPlay
    {
        get => isPlaying;

        set
        {
            if (value && !isPlaying)
                StartOrchestra();
            else if (!value && isPlaying)
                StopOrchestra();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

    public void StartOrchestra()
    {
        if (isPlaying)
            return;

        if (musicScoreManager == null)
        {
            Debug.LogError("MusicScoreManager is missing.");
            return;
        }

        // Buscamos el reloj si no esta asignado.
        if (musicClock == null)
            musicClock = MusicClockFMOD.Instance;

        if (musicClock == null)
        {
            Debug.LogError("MusicClockFMOD is missing.");
            return;
        }

        // El reloj siempre empieza antes que los musicos.
        musicClock.IniciarReloj();

        if (!musicClock.EstaReproduciendo)
        {
            Debug.LogError("MusicClock could not start.");
            return;
        }

        int musicosIniciados = 0;

        foreach (MusicianAudio musician in musicians)
        {
            if (musician.audioEvent.IsNull)
                continue;

            musician.instance =
                RuntimeManager.CreateInstance(musician.audioEvent);

            if (musician.musicianTransform != null)
            {
                RuntimeManager.AttachInstanceToGameObject(
                    musician.instance,
                    musician.musicianTransform.gameObject
                );
            }

            musician.currentPitch = 1f;
            musician.targetPitch = 1f;

            musician.instance.setPitch(1f);

            FMOD.RESULT resultado = musician.instance.start();

            if (resultado == FMOD.RESULT.OK)
            {
                musicosIniciados++;
            }
            else
            {
                Debug.LogError(
                    $"Could not start {musician.name}: {resultado}"
                );

                musician.instance.release();
                musician.instance = default;
            }
        }

        if (musicosIniciados == 0)
        {
            Debug.LogError("No musicians could start.");
            musicClock.DetenerReloj();
            return;
        }

        isPlaying = true;
        changeTimer = 0f;

        CalculateNewPitches();

        Debug.Log("Orchestra and MusicClock started.");
    }

    public void StopOrchestra()
    {
        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            musician.instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            musician.instance.release();

            musician.instance = default;

            musician.currentPitch = 1f;
            musician.targetPitch = 1f;
        }

        if (musicClock != null)
            musicClock.DetenerReloj();

        isPlaying = false;
        changeTimer = 0f;

        Debug.Log("Orchestra and MusicClock stopped.");
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
        if (musicClock == null || loopDuration <= 0f)
            return;

        if (!musicClock.TryObtenerPosicionTimeline(
            out int clockPosition))
            return;

        float clockTime = clockPosition / 1000f;

        float score = musicScoreManager.CurrentRhythm;

        // Cuanto más cerca de 100, mayor es la sincronización.
        float scoreDifference = Mathf.Abs(score - 100f);

        float synchronizationFactor = 1f - Mathf.Clamp01(
            scoreDifference / synchronizationScoreRange
        );

        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            if (musician.instance.getTimelinePosition(
                out int musicianPosition) != FMOD.RESULT.OK)
                continue;

            float musicianTime = musicianPosition / 1000f;

            // Calculamos el desfase considerando el loop circular.
            float timeDifference = musicianTime - clockTime;

            timeDifference = Mathf.Repeat(
                timeDifference + loopDuration * 0.5f,
                loopDuration
            ) - loopDuration * 0.5f;

            // Corrección de velocidad adicional.
            float pitchCorrection = 0f;

            if (Mathf.Abs(timeDifference) > synchronizationDeadZone)
            {
                pitchCorrection = Mathf.Clamp(
                    -timeDifference * synchronizationStrength,
                    -maxSynchronizationPitch,
                    maxSynchronizationPitch
                );

                pitchCorrection *= synchronizationFactor;
            }

            // Combinamos el pitch de desincronización
            // con el pitch de recuperación temporal.
            float finalTargetPitch =
                musician.targetPitch + pitchCorrection;

            musician.currentPitch = Mathf.MoveTowards(
                musician.currentPitch,
                finalTargetPitch,
                pitchSmoothing * Time.deltaTime
            );

            musician.instance.setPitch(musician.currentPitch);
        }
    }


    // Sincroniza los musicos con el reloj maestro FMOD.
    public void SynchronizeOrchestra()
    {
        if (!isPlaying || musicClock == null)
            return;

        if (!musicClock.TryObtenerPosicionTimeline(
            out int position))
        {
            Debug.LogWarning("Could not read MusicClock timeline.");
            return;
        }

        foreach (MusicianAudio musician in musicians)
        {
            if (!musician.instance.isValid())
                continue;

            musician.instance.setTimelinePosition(position);

            musician.currentPitch = 1f;
            musician.targetPitch = 1f;

            musician.instance.setPitch(1f);
        }

        changeTimer = 0f;

        Debug.Log("Orchestra synchronized with MusicClock.");
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        StopOrchestra();
        Instance = null;
    }
}
