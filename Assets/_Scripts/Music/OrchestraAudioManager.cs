using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using Unity.Netcode;

public class OrchestraAudioManager : MonoBehaviour
{
    public static OrchestraAudioManager Instance { get; private set; }

    [Header("Score System")]
    [SerializeField] private MusicScoreManager musicScoreManager;

    [Header("Music Clock")]
    [SerializeField] private MusicClockFMOD musicClock;

    [Header("Cancion de este nivel")]
    [Tooltip("BPM reales de la cancion. RhythmTracker los lee automaticamente.")]
    [SerializeField, Min(1f)] private float songBpm = 100f;

    [Tooltip("Duracion exacta del loop musical en segundos, la misma que en FMOD.")]
    [SerializeField, Min(0.1f)] private float loopDuration = 19.2f;

    [Header("Instrument Groups")]
    [Tooltip("Un InstrumentGroup por tipo de instrumento (Trompetas, Violines, etc.).")]
    [SerializeField] private List<InstrumentGroup> instrumentGroups = new();

    [Header("Desynchronization")]
    [SerializeField, Range(0f, 0.5f)]
    private float maxDeviation = 0.20f;

    [SerializeField, Min(0.1f)]
    private float changeInterval = 2f;

    [SerializeField, Min(0.1f)]
    private float pitchSmoothing = 1.5f;

    [Header("Progressive Synchronization")]
    [Tooltip("Correccion de velocidad por cada segundo de desfase.")]
    [SerializeField, Min(0f)]
    private float synchronizationStrength = 0.35f;

    [Tooltip("Maxima variacion de pitch para corregir el desfase.")]
    [SerializeField, Range(0f, 0.2f)]
    private float maxSynchronizationPitch = 0.08f;

    [Tooltip("Distancia respecto a 100 a partir de la cual comienza la recuperacion.")]
    [SerializeField, Range(1f, 100f)]
    private float synchronizationScoreRange = 50f;

    [Tooltip("Margen temporal considerado sincronizado.")]
    [SerializeField, Min(0f)]
    private float synchronizationDeadZone = 0.015f;

    [Header("Calidad global de la orquesta (lectura)")]
    [SerializeField, Range(0f, 1f)]
    private float orchestraQuality = 1f;

    private float changeTimer;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public float SongBpm => songBpm;
    public float SongLoopDuration => loopDuration;
    public float OrchestraQuality => orchestraQuality;
    public float GlobalQuality => musicScoreManager != null
        ? musicScoreManager.CurrentQuality
        : orchestraQuality;

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

        // Si los grupos son hijos del manager, permite descubrirlos automaticamente.
        if (instrumentGroups.Count == 0)
            instrumentGroups.AddRange(GetComponentsInChildren<InstrumentGroup>(true));
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
        UpdateOrchestraQuality();
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

        if (musicClock == null)
            musicClock = MusicClockFMOD.Instance;

        if (musicClock == null)
        {
            Debug.LogError("MusicClockFMOD is missing.");
            return;
        }

        // La duracion del loop se configura aqui, una sola vez por cancion.
        // Es la misma que usa el manager para resincronizar los grupos.
        musicClock.ConfigurarDuracionLoop(loopDuration);

        // El reloj siempre empieza antes que los grupos instrumentales.
        musicClock.IniciarReloj();

        if (!musicClock.EstaReproduciendo)
        {
            Debug.LogError("MusicClock could not start.");
            return;
        }

        int gruposIniciados = 0;

        foreach (InstrumentGroup group in instrumentGroups)
        {
            if (group != null && group.StartGroup())
                gruposIniciados++;
        }

        if (gruposIniciados == 0)
        {
            Debug.LogError("No instrument groups could start.");
            musicClock.DetenerReloj();
            return;
        }

        isPlaying = true;
        changeTimer = 0f;

        // Alineamos inicialmente los grupos con el reloj.
        SynchronizeOrchestra();

        CalculateNewPitches();
        UpdateOrchestraQuality();

        Debug.Log("Orchestra and MusicClock started.");
    }

    public void StopOrchestra()
    {
        foreach (InstrumentGroup group in instrumentGroups)
        {
            if (group != null)
                group.StopGroup();
        }

        if (musicClock != null)
            musicClock.DetenerReloj();

        isPlaying = false;
        changeTimer = 0f;

        orchestraQuality = 1f;
        if (musicScoreManager != null)
            musicScoreManager.SetOrchestraQuality(1f);

        Debug.Log("Orchestra and MusicClock stopped.");
    }

    private void CalculateNewPitches()
    {
        float score = musicScoreManager.CurrentRhythm;

        // 100 = sincronizacion perfecta; 0 y 200 = desviacion maxima.
        float desynchronization = Mathf.Abs(score - 100f) / 100f;
        float deviation = desynchronization * maxDeviation;

        foreach (InstrumentGroup group in instrumentGroups)
        {
            if (group != null && group.IsPlaying)
                group.CalculateNewPitch(deviation);
        }
    }

    private void UpdatePitches()
    {
        if (musicClock == null || loopDuration <= 0f)
            return;

        if (!musicClock.TryObtenerPosicionTimeline(out int clockPosition))
            return;

        float clockTime = clockPosition / 1000f;
        float scoreDifference = Mathf.Abs(musicScoreManager.CurrentRhythm - 100f);

        // El grupo se resincroniza mas rapido cuanto mas cerca esta el director de 100.
        float synchronizationFactor = 1f - Mathf.Clamp01(
            scoreDifference / synchronizationScoreRange
        );

        foreach (InstrumentGroup group in instrumentGroups)
        {
            if (group == null || !group.IsPlaying)
                continue;

            group.UpdateGroup(
                Time.deltaTime,
                clockTime,
                loopDuration,
                synchronizationFactor,
                synchronizationStrength,
                maxSynchronizationPitch,
                synchronizationDeadZone,
                pitchSmoothing
            );
        }
    }

    private void UpdateOrchestraQuality()
    {
        float totalQuality = 0f;
        int totalMusicians = 0;

        // Media ponderada por cantidad de musicos, no por numero de grupos.
        foreach (InstrumentGroup group in instrumentGroups)
        {
            if (group == null || !group.IsPlaying)
                continue;

            totalQuality += group.GetTotalMusicianQuality();
            totalMusicians += group.GetMusicianCount();
        }

        orchestraQuality = totalMusicians > 0
            ? totalQuality / totalMusicians
            : 1f;

        // Actualizamos la calidad global del MusicScoreManager.
        // RhythmTracker sigue controlando exclusivamente la calidad de direccion.
        musicScoreManager.SetOrchestraQuality(orchestraQuality);
    }

    // Sincronizacion manual inmediata con el reloj maestro FMOD.
    public void SynchronizeOrchestra()
    {
        if (!isPlaying || musicClock == null)
            return;

        if (!musicClock.TryObtenerPosicionTimeline(out int position))
        {
            Debug.LogWarning("Could not read MusicClock timeline.");
            return;
        }

        foreach (InstrumentGroup group in instrumentGroups)
        {
            if (group != null && group.IsPlaying)
                group.SynchronizeWithClock(position);
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
