using UnityEngine;

public class MusicScoreManager : MonoBehaviour
{
    [Header("--- ESTADO GENERAL EN VIVO (LECTURA) ---")]
    [Tooltip("Ritmo actual de la obra (0 a 200). 100 es el ritmo perfecto.")]
    [SerializeField] private float currentRhythm = 100f;

    [Tooltip("Calidad general del ritmo y dirección (0 a 1).")]
    [SerializeField] private float currentQuality = 1.0f;

    [Header("--- CONTROL DE RITMO (0 a 200) ---")]
    [Tooltip("Velocidad a la que el ritmo se descontrola y se aleja del 100 por segundo cuando NO hay input.")]
    [SerializeField] private float noInputRhythmDriftSpeed = 0.5f;

    [Tooltip("Velocidad de respuesta/transición suave del ritmo hacia el valor indicado por el director.")]
    [SerializeField] private float rhythmLerpSpeed = 0.4f;

    [Header("--- CONTROL DE CALIDAD GENERAL (0 a 1) ---")]
    [Tooltip("Pérdida de calidad general por segundo cuando NO se recibe ningún input.")]
    [SerializeField] private float noInputQualityDecayRate = 0.04f;

    [Tooltip("Velocidad de respuesta/transición suave de la calidad hacia el valor indicado por el director.")]
    [SerializeField] private float qualityLerpSpeed = 0.5f;

    [Tooltip("Factor de atenuación para eventos negativos (0.1 a 1). A menor calidad actual, menor es la bajada absoluta.")]
    [SerializeField, Range(0.1f, 1f)] private float eventDiminishingFactor = 0.5f;

    [Header("--- TIMING DE INPUTS ---")]
    [Tooltip("Tiempo máximo en segundos sin recibir datos antes de considerar que el director ha dejado de dar marcas.")]
    [SerializeField] private float inputTimeoutDuration = 0.4f;

    // Estado interno
    private float targetRhythm = 100f;
    private float targetQuality = 1.0f;
    private float timeSinceLastInput = 999f;
    private bool hasActiveInput = false;

    // Propiedades públicas de acceso
    public float CurrentRhythm => currentRhythm;
    public float CurrentQuality => currentQuality;
    public bool HasActiveInput => hasActiveInput;

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
    /// Impacto repentino de eventos negativos en la calidad general (efecto disminuyente).
    /// </summary>
    public void TriggerNegativeEvent(float severity0To100)
    {
        float severityNormalized = Mathf.Clamp01(severity0To100 / 100f);
        float qualityLoss = currentQuality * severityNormalized * eventDiminishingFactor;
        currentQuality = Mathf.Clamp01(currentQuality - qualityLoss);
    }

    private void UpdateRhythmAndQuality(float dt)
    {
        if (hasActiveInput)
        {
            currentRhythm = Mathf.Lerp(currentRhythm, targetRhythm, dt * rhythmLerpSpeed);
            currentQuality = Mathf.Lerp(currentQuality, targetQuality, dt * qualityLerpSpeed);
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

            // Degradación continua de calidad general
            currentQuality = Mathf.Max(0f, currentQuality - (noInputQualityDecayRate * dt));
        }
    }
}