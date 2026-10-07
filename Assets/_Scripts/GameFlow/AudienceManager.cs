using System.Collections.Generic;
using UnityEngine;

public class AudienceManager : MonoBehaviour
{
    [Header("--- REFERENCIAS ---")]
    [Tooltip("Referencia al gestor de puntuación global de la música.")]
    [SerializeField] private MusicScoreManager scoreManager;

    [Header("--- ESTADO EN VIVO (LECTURA) ---")]
    [Tooltip("Puntuación simulada (0 a 100) calculada a partir del ritmo actual y la media de calidad individual de los músicos.")]
    [SerializeField] private float pseudoScore = 100f;

    [Tooltip("Cantidad actual de audiencia presente en el espectáculo.")]
    [SerializeField] private float currentAudience = 500f;

    [Tooltip("Media actual calculada entre todos los músicos activos.")]
    [SerializeField] private float averageMusicianQuality = 1.0f;

    [Header("--- CONFIGURACIÓN DE AUDIENCIA ---")]
    [Tooltip("Audiencia inicial al comenzar el nivel.")]
    [SerializeField] private float initialAudience = 500f;

    [Tooltip("Máximo número de personas en la audiencia.")]
    [SerializeField] private float maxAudience = 1000f;

    [Header("--- CÁLCULO DE PSEUDO-SCORE ---")]
    [Tooltip("Peso del ritmo (0 a 1) en la pseudo-score final.")]
    [SerializeField, Range(0f, 1f)] private float rhythmWeight = 0.5f;

    [Tooltip("Peso de la calidad media de los músicos (0 a 1) en la pseudo-score final.")]
    [SerializeField, Range(0f, 1f)] private float musicianQualityWeight = 0.5f;

    [Header("--- DINÁMICA DE AUDIENCIA ---")]
    [Tooltip("Umbral de Pseudo-Score (0 a 100). Por encima se gana audiencia; por debajo, se pierde.")]
    [SerializeField] private float neutralThresholdScore = 65f;

    [Tooltip("Tasa de ganancia de espectadores por segundo cuando la música es buena.")]
    [SerializeField] private float audienceGainRate = 15f;

    [Tooltip("Tasa de pérdida de espectadores por segundo cuando la música es mala.")]
    [SerializeField] private float audienceLossRate = 20f;

    private readonly List<IMusician> activeMusicians = new List<IMusician>();

    public float CurrentAudience => currentAudience;
    public float PseudoScore => pseudoScore;
    public float AverageMusicianQuality => averageMusicianQuality;

    private void Start()
    {
        currentAudience = initialAudience;

        if (scoreManager == null)
        {
            scoreManager = GetComponentInChildren<MusicScoreManager>();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        CalculateAverageMusicianQuality();
        CalculatePseudoScore();
        UpdateAudience(dt);
    }

    /// <summary>
    /// Registra un músico para incluirlo en la media de calidad.
    /// </summary>
    public void RegisterMusician(IMusician musician)
    {
        if (musician != null && !activeMusicians.Contains(musician))
        {
            activeMusicians.Add(musician);
        }
    }

    /// <summary>
    /// Desregistra a un músico (ej. si es expulsado o destruido).
    /// </summary>
    public void UnregisterMusician(IMusician musician)
    {
        if (musician != null)
        {
            activeMusicians.Remove(musician);
        }
    }

    private void CalculateAverageMusicianQuality()
    {
        if (activeMusicians.Count == 0)
        {
            // Si no hay músicos registrados, se mantiene en 1.0 por defecto
            averageMusicianQuality = 1.0f;
            return;
        }

        float totalQuality = 0f;
        int validCount = 0;

        for (int i = activeMusicians.Count - 1; i >= 0; i--)
        {
            if (activeMusicians[i] == null)
            {
                activeMusicians.RemoveAt(i);
                continue;
            }

            totalQuality += Mathf.Clamp01(activeMusicians[i].IndividualQuality);
            validCount++;
        }

        averageMusicianQuality = validCount > 0 ? (totalQuality / validCount) : 1.0f;
    }

    private void CalculatePseudoScore()
    {
        if (scoreManager == null) return;

        // 1. Calcular precisión del ritmo (100 = perfecto -> 1.0, 0 ó 200 -> 0.0)
        float rhythmFitness = 1f - (Mathf.Abs(scoreManager.CurrentRhythm - 100f) / 100f);
        rhythmFitness = Mathf.Clamp01(rhythmFitness);

        // 2. Pseudo-Score normalizada (0.0 a 1.0) usando la media individual de los músicos
        float normalizedScore = (rhythmFitness * rhythmWeight) + (averageMusicianQuality * musicianQualityWeight);

        // 3. Escalar a rango 0-100 para facilidaf de depuración e UI
        pseudoScore = normalizedScore * 100f;
    }

    private void UpdateAudience(float dt)
    {
        if (pseudoScore >= neutralThresholdScore)
        {
            // Ganancia proporcional a cuánto se supera el umbral neutro
            float performanceBonus = (pseudoScore - neutralThresholdScore) / (100f - neutralThresholdScore);
            currentAudience += performanceBonus * audienceGainRate * dt;
        }
        else
        {
            // Pérdida proporcional a cuánto se cae por debajo del umbral neutro
            float performancePenalty = (neutralThresholdScore - pseudoScore) / neutralThresholdScore;
            currentAudience -= performancePenalty * audienceLossRate * dt;
        }

        currentAudience = Mathf.Clamp(currentAudience, 0f, maxAudience);
    }
}