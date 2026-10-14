
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class MusicClockFMOD : MonoBehaviour
{
    public static MusicClockFMOD Instance { get; private set; }

    [Header("FMOD")]
    [SerializeField] private EventReference clockEvent;

    [Header("Configuracion")]
    [SerializeField] private bool metronomoActivado = true;

    // Duracion exacta del loop musical, en segundos.
    [SerializeField, Min(0.01f)]
    private float duracionLoop = 19.2f;

    [Header("Automatic Metronome Volume")]
    [SerializeField] private bool automaticVolume = true;

    [SerializeField, Range(1f, 100f)]
    private float maxScoreDeviation = 60f;

    [SerializeField, Min(0.1f)]
    private float volumeFadeSpeed = 1.5f;

    private float currentVolume = 0f;
    [Header("Score System")]
    [SerializeField] private MusicScoreManager musicScoreManager;
    private EventInstance clockInstance;
    private bool reproduciendo;

    private double tiempoAnterior;
    private int loopsCompletados;
    private bool primeraLectura;

    public bool EstaReproduciendo => reproduciendo;

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
    if (!reproduciendo || !automaticVolume || musicScoreManager == null)
        return;

    float scoreDifference =
        Mathf.Abs(musicScoreManager.CurrentRhythm - 100f);

    // Calculamos el volumen según la distancia respecto al ritmo perfecto.
    float targetVolume = Mathf.Clamp01(
        scoreDifference / maxScoreDeviation
    );

    // Suavizamos los cambios de volumen.
    currentVolume = Mathf.MoveTowards(
        currentVolume,
        targetVolume,
        volumeFadeSpeed * Time.deltaTime
    );

    if (clockInstance.isValid())
    {
        clockInstance.setVolume(currentVolume);
    }
}

    public void IniciarReloj()
    {
        DetenerReloj();

        if (clockEvent.IsNull)
        {
            Debug.LogError("Asigna Clock1 en el Inspector.");
            return;
        }

        clockInstance = RuntimeManager.CreateInstance(clockEvent);

        clockInstance.setParameterByName(
            "ActiveMetronome",
            metronomoActivado ? 1f : 0f
        );

        FMOD.RESULT resultado = clockInstance.start();

        if (resultado != FMOD.RESULT.OK)
        {
            Debug.LogError("Error al iniciar Clock1: " + resultado);
            clockInstance.release();
            clockInstance.clearHandle();
            return;
        }

        tiempoAnterior = 0;
        loopsCompletados = 0;
        primeraLectura = true;
        reproduciendo = true;

        Debug.Log("MusicClock iniciado.");
    }

    public bool TryObtenerTiempo(out double segundos)
    {
        segundos = 0;

        if (!reproduciendo || !clockInstance.isValid())
            return false;

        FMOD.RESULT resultado = clockInstance.getTimelinePosition(
            out int milisegundos
        );

        if (resultado != FMOD.RESULT.OK)
            return false;

        double tiempoActual = milisegundos / 1000.0;

        if (!primeraLectura)
        {
            // Detectamos cuando la timeline retrocede por el loop.
            if (tiempoActual < tiempoAnterior - 0.1)
            {
                loopsCompletados++;
            }
        }

        primeraLectura = false;
        tiempoAnterior = tiempoActual;

        // Convertimos el tiempo del loop en tiempo continuo.
        segundos = loopsCompletados * (double)duracionLoop
                   + tiempoActual;

        return true;
    }

    public bool TryObtenerPosicionTimeline(out int milisegundos)
    {
        milisegundos = 0;

        if (!reproduciendo || !clockInstance.isValid())
            return false;

        return clockInstance.getTimelinePosition(
            out milisegundos
        ) == FMOD.RESULT.OK;
    }

    public void ActivarMetronomo(bool activar)
    {
        metronomoActivado = activar;

        if (clockInstance.isValid())
        {
            clockInstance.setParameterByName(
                "ActiveMetronome",
                metronomoActivado ? 1f : 0f
            );
        }
    }

    public void DetenerReloj()
    {
        if (clockInstance.isValid())
        {
            clockInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            clockInstance.release();
            clockInstance.clearHandle();
        }

        reproduciendo = false;
    }

    private void OnDestroy()
    {
        DetenerReloj();

        if (Instance == this)
            Instance = null;
    }
}
