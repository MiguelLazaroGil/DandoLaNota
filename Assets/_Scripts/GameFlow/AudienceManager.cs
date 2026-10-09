using System.Collections.Generic;
using UnityEngine;

public class AudienceManager : MonoBehaviour
{
    [Header("--- REFERENCIAS ---")]
    [Tooltip("Referencia al gestor de puntuación global de la música.")]
    [SerializeField] private MusicScoreManager scoreManager;

    [Header("--- INTERVALO DE ACTUALIZACIÓN DE AUDIENCIA ---")]
    [Tooltip("Tiempo en segundos entre cada refresco de la audiencia (ej: 0.5s para no refrescar constantemente cada frame).")]
    [SerializeField] private float audienceUpdateInterval = 0.5f;

    [Header("--- ESTADO EN VIVO (LECTURA) ---")]
    [Tooltip("Puntuación simulada (0 a 100) calculada a partir del ritmo y la calidad individual.")]
    [SerializeField] private float pseudoScore = 100f;

    [Tooltip("Cantidad actual de audiencia presente en el espectáculo.")]
    [SerializeField] private float currentAudience = 50f;

    [Tooltip("Media actual calculada entre todos los músicos activos.")]
    [SerializeField] private float averageMusicianQuality = 1.0f;

    [Header("--- CONFIGURACIÓN BASE DE AUDIENCIA ---")]
    [Tooltip("Audiencia inicial al comenzar el nivel.")]
    [SerializeField] private float initialAudience = 50f;

    [Tooltip("Máximo número posible de personas en la audiencia.")]
    [SerializeField] private float maxAudience = 100f;

    [Header("--- CÁLCULO DE PSEUDO-SCORE ---")]
    [Tooltip("Peso del ritmo (0 a 1) en la pseudo-score final.")]
    [SerializeField, Range(0f, 1f)] private float rhythmWeight = 0.5f;

    [Tooltip("Peso de la calidad media de los músicos (0 a 1) en la pseudo-score final.")]
    [SerializeField, Range(0f, 1f)] private float musicianQualityWeight = 0.5f;

    [Header("--- DINÁMICA PORCENTUAL DE AUDIENCIA ---")]
    [Tooltip("Umbral de Pseudo-Score (0 a 100). Por encima se gana audiencia; por debajo, se pierde.")]
    [SerializeField] private float neutralThresholdScore = 85f;

    [Tooltip("Porcentaje de la audiencia actual ganado por segundo a rendimiento máximo (ej: 0.05 = 5%/s).")]
    [SerializeField, Range(0f, 1f)] private float baseGainPercentage = 0.05f;

    [Tooltip("Porcentaje de la audiencia actual perdido por segundo a rendimiento pésimo (ej: 0.08 = 8%/s).")]
    [SerializeField, Range(0f, 1f)] private float baseLossPercentage = 0.08f;

    [Tooltip("Mínimo absoluto de espectadores ganados/perdidos por segundo para evitar que se atasque con audiencia muy baja.")]
    [SerializeField] private float minAbsoluteChangeRate = 0.5f;

    [Tooltip("Desgaste natural del público por segundo (0.01 = 1%/s). Garantiza la presión continua 'ad infinitum'.")]
    [SerializeField, Range(0f, 0.1f)] private float naturalFatigueDecay = 0.005f;

    [Header("--- RALENTIZACIÓN EN EXTREMOS ---")]
    [Tooltip("Multiplicador mínimo de velocidad cuando la audiencia está rozando el 0 o el máximo.")]
    [SerializeField, Range(0.01f, 0.5f)] private float minExtremeSpeedFactor = 0.1f;

    // Estado interno
    private float intervalTimer = 0f;

    //TODO: cambiar esto que es n
    [RequireInterface(typeof(IMusician))]
    public List<MonoBehaviour> _activeMusicians = new List<MonoBehaviour>();
    private List<IMusician> activeMusicians => _activeMusicians.ConvertAll(mb => mb as IMusician);

    public float CurrentAudience => currentAudience;
    public float PseudoScore => pseudoScore;
    public float AverageMusicianQuality => averageMusicianQuality;

    private void Start()
    {
        currentAudience = initialAudience;

        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<MusicScoreManager>();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // La pseudo-score se refresca visualmente cada frame
        CalculateAverageMusicianQuality();
        CalculatePseudoScore();

        // La audiencia se actualiza por pulsos/intervalos parametrizados
        intervalTimer += dt;
        if (intervalTimer >= audienceUpdateInterval)
        {
            UpdateAudience(intervalTimer);
            intervalTimer = 0f;
        }
    }

    public void RegisterMusician(IMusician musician)
    {
        if (musician != null && !activeMusicians.Contains(musician))
        {
            activeMusicians.Add(musician);
        }
    }

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

        float rhythmFitness = 1f - (Mathf.Abs(scoreManager.CurrentRhythm - 100f) / 100f);
        rhythmFitness = Mathf.Clamp01(rhythmFitness);

        float normalizedScore = (rhythmFitness * rhythmWeight) + (averageMusicianQuality * musicianQualityWeight);
        pseudoScore = normalizedScore * 100f;
    }

    private void UpdateAudience(float stepTime)
    {
        // Ratio de audiencia actual (0.0 a 1.0)
        float audienceRatio = Mathf.Clamp01(currentAudience / maxAudience);

        // Factores de atenuación en límites:
        // - Al acercarse a maxAudience (ratio -> 1), la ganancia cae progresivamente hacia minExtremeSpeedFactor.
        // - Al acercarse a 0 (ratio -> 0), la pérdida cae progresivamente hacia minExtremeSpeedFactor.
        float gainExtremeFactor = Mathf.Max(minExtremeSpeedFactor, 1f - audienceRatio);
        float lossExtremeFactor = Mathf.Max(minExtremeSpeedFactor, audienceRatio);

        if (pseudoScore >= neutralThresholdScore)
        {
            // Rendimiento positivo (0.0 a 1.0)
            float performanceBonus = (pseudoScore - neutralThresholdScore) / (100f - neutralThresholdScore);

            // Tasa porcentual + Suelo mínimo absoluto
            float percentageGain = currentAudience * baseGainPercentage * performanceBonus;
            float rawGainRate = Mathf.Max(minAbsoluteChangeRate, percentageGain);

            // Aplicar atenuación al acercarse al máximo
            float finalGain = rawGainRate * gainExtremeFactor * stepTime;
            currentAudience += finalGain;
        }
        else
        {
            // Rendimiento negativo (0.0 a 1.0)
            float performancePenalty = (neutralThresholdScore - pseudoScore) / neutralThresholdScore;

            // Tasa porcentual + Suelo mínimo absoluto
            float percentageLoss = currentAudience * baseLossPercentage * performancePenalty;
            float rawLossRate = Mathf.Max(minAbsoluteChangeRate, percentageLoss);

            // Aplicar atenuación al acercarse a 0
            float finalLoss = rawLossRate * lossExtremeFactor * stepTime;
            currentAudience -= finalLoss;
        }

        // Desgaste natural continuo (fatiga/desinterés constante por el mero paso del tiempo)
        float naturalLoss = currentAudience * naturalFatigueDecay * stepTime;
        currentAudience -= naturalLoss;

        currentAudience = Mathf.Clamp(currentAudience, 0f, maxAudience);
    }
}