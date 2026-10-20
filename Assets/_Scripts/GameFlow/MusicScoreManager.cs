using UnityEngine;

public class MusicScoreManager : MonoBehaviour
{
    [Header("--- ESTADO GENERAL EN VIVO (LECTURA) ---")]
    [Tooltip("Ritmo actual de la obra (0 a 200). 100 es el ritmo perfecto.")]
    [SerializeField] private float currentRhythm = 100f;

    [Tooltip("Calidad global de la direccion y de todos los musicos (0 a 1).")]
    [SerializeField] private float currentQuality = 1.0f;

    [Header("--- CONTROL DE RITMO (0 a 200) ---")]
    [Tooltip("Velocidad a la que el ritmo se descontrola y se aleja del 100 por segundo cuando NO hay input.")]
    [SerializeField] private float noInputRhythmDriftSpeed = 0.5f;

    [Tooltip("Velocidad de respuesta/transición suave del ritmo hacia el valor indicado por el director.")]
    [SerializeField] private float rhythmLerpSpeed = 0.4f;

    [Header("--- CORRECCIÓN DE RITMO POR ACIERTOS ---")]
    [Tooltip("Corrección inicial del ritmo por cada pulso acertado.")]
    [SerializeField] private float baseCorrection = 2f;

    [Tooltip("Corrección adicional por cada acierto consecutivo.")]
    [SerializeField] private float streakBonus = 0.5f;

    [Tooltip("Corrección máxima que puede aplicarse por pulso.")]
    [SerializeField] private float maxCorrection = 7f;

    [Header("--- CONTROL DE CALIDAD GENERAL (0 a 1) ---")]
    [Tooltip("Pérdida de calidad general por segundo cuando NO se recibe ningún input.")]
    [SerializeField] private float noInputQualityDecayRate = 0.04f;

    [Tooltip("Velocidad de respuesta/transición suave de la calidad hacia el valor indicado por el director.")]
    [SerializeField] private float qualityLerpSpeed = 0.5f;

    [Tooltip("Factor de atenuación para eventos negativos (0.1 a 1). A menor calidad actual, menor es la bajada absoluta.")]
    [SerializeField, Range(0.1f, 1f)] private float eventDiminishingFactor = 0.5f;

    [Header("--- CALIDAD DIRECCION Y MUSICOS (LECTURA) ---")]
    [Tooltip("Calidad suavizada de BPM y metrónomo recibida desde RhythmTracker (0 a 1).")]
    [SerializeField, Range(0f, 1f)] private float currentDirectorQuality = 1f;

    [Tooltip("Media de las calidades individuales recibida desde OrchestraAudioManager (0 a 1).")]
    [SerializeField, Range(0f, 1f)] private float orchestraQuality = 1f;

    [Header("--- TIMING DE INPUTS ---")]
    [Tooltip("Tiempo máximo en segundos sin recibir datos antes de considerar que el director ha dejado de dar marcas.")]
    [SerializeField] private float inputTimeoutDuration = 4f;

    // Estado interno
    private float targetRhythm = 100f;
    private float targetQuality = 1.0f;
    private float timeSinceLastInput = 999f;
    private bool hasActiveInput = false;

    // Propiedades públicas de acceso
    public float CurrentRhythm => currentRhythm;
    public float CurrentQuality => currentQuality;
    public float CurrentDirectorQuality => currentDirectorQuality;
    public float OrchestraQuality => orchestraQuality;
    public bool HasActiveInput => hasActiveInput;

    private void Awake()
    {
        targetRhythm = currentRhythm;
        currentDirectorQuality = Mathf.Clamp01(currentQuality);
        orchestraQuality = Mathf.Clamp01(orchestraQuality);
        currentQuality = Mathf.Clamp01(currentDirectorQuality * orchestraQuality);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        timeSinceLastInput += dt;
        hasActiveInput = timeSinceLastInput < inputTimeoutDuration;

        UpdateRhythmAndQuality(dt);
    }

    /// <summary>
    /// Recibe las marcas del director/batuta.
    /// </summary>
    public void ReceiveDirectorInput(float rhythm, float quality)
    {
        targetRhythm = Mathf.Clamp(rhythm, 0f, 200f);
        targetQuality = Mathf.Clamp01(quality);
        timeSinceLastInput = 0f;
    }

    /// <summary>
    /// Recibe la media de las calidades de todos los musicos activos.
    /// </summary>
    public void SetOrchestraQuality(float quality)
    {
        orchestraQuality = Mathf.Clamp01(quality);
        currentQuality = Mathf.Clamp01(currentDirectorQuality * orchestraQuality);
    }

    /// <summary>
    /// Acerca progresivamente el ritmo a 100 según la racha de aciertos.
    /// </summary>
    public void CorrectRhythm(int streak)
    {
        float correction = Mathf.Min(
            baseCorrection + Mathf.Max(0, streak - 1) * streakBonus,
            maxCorrection
        );

        targetRhythm = Mathf.MoveTowards(
            targetRhythm,
            100f,
            correction
        );

        timeSinceLastInput = 0f;
        hasActiveInput = true;
    }

    /// <summary>
    /// Impacto repentino de eventos negativos en la calidad general (efecto disminuyente).
    /// </summary>
    public void TriggerNegativeEvent(float severity0To100)
    {
        float severityNormalized = Mathf.Clamp01(severity0To100 / 100f);
        float qualityLoss = currentDirectorQuality * severityNormalized * eventDiminishingFactor;
        currentDirectorQuality = Mathf.Clamp01(currentDirectorQuality - qualityLoss);
        currentQuality = Mathf.Clamp01(currentDirectorQuality * orchestraQuality);
    }

    private void UpdateRhythmAndQuality(float dt)
    {
        if (hasActiveInput)
        {
            currentRhythm = Mathf.Lerp(currentRhythm, targetRhythm, dt * rhythmLerpSpeed);
            currentDirectorQuality = Mathf.Lerp(currentDirectorQuality, targetQuality, dt * qualityLerpSpeed);
        }
        else
        {
            // Deriva del ritmo al no haber marcas
            if (currentRhythm >= 100f)
            {
                currentRhythm += noInputRhythmDriftSpeed * dt;
            }
            else
            {
                currentRhythm -= noInputRhythmDriftSpeed * dt;
            }
            currentRhythm = Mathf.Clamp(currentRhythm, 0f, 200f);

            // Evita que al volver a recibir input se recupere un objetivo desactualizado
            targetRhythm = currentRhythm;

            // Degradación continua de calidad general
            currentDirectorQuality = Mathf.Max(0f, currentDirectorQuality - (noInputQualityDecayRate * dt));
        }

        // Calidad global = calidad de direccion (BPM x fase) x calidad de los musicos.
        currentQuality = Mathf.Clamp01(currentDirectorQuality * orchestraQuality);
    }
}
