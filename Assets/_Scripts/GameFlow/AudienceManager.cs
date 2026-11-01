using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class AudienceManager : MonoBehaviour
{
    [Header("--- REFERENCIAS ---")]
    [Tooltip("Referencia al gestor de puntuación global de la música.")]
    [SerializeField] private MusicScoreManager scoreManager;
    [SerializeField] private OrchestraAudioManager orchestraManager;

    [Header("Eventos de Umbral")]
    [SerializeField] public UnityEvent onAudienceReach20;
    [SerializeField] public UnityEvent onAudienceReach50;
    [SerializeField] public UnityEvent onAudienceReach80;

    [Header("--- INTERVALO DE ACTUALIZACIÓN DE AUDIENCIA ---")]
    [Tooltip("Tiempo en segundos entre cada refresco de la audiencia (ej: 0.5s para no refrescar constantemente cada frame).")]
    [SerializeField] private float audienceUpdateInterval = 0.5f;

    [Header("--- ESTADO EN VIVO (LECTURA) ---")]
    [Tooltip("Puntuación simulada (0 a 100) calculada a partir del ritmo y la calidad individual.")]
    [SerializeField] private float pseudoScore = 100f;

    [Tooltip("Cantidad actual de audiencia presente en el espectáculo.")]
    [SerializeField] public float currentAudience = 50f;

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
    private List<IMusician> activeMusicians = new List<IMusician>();
    private bool simulacionActiva = false;

    public float CurrentAudience => currentAudience;
    public float PseudoScore => pseudoScore;
    public float AverageMusicianQuality => averageMusicianQuality;

    private void Start()
    {
        currentAudience = initialAudience;

        if (orchestraManager != null)
        {
            var _activeMusicians = orchestraManager.gameObject.GetComponentsInChildren<IMusician>();
            activeMusicians = activeMusicians.ToList<IMusician>();
        }

        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<MusicScoreManager>();
        }
        if (orchestraManager == null)
        {
            orchestraManager = OrchestraAudioManager.Instance;
        }
    }

    private void Update()
    {
        if (!simulacionActiva || Time.timeScale == 0f)
            return;

        float dt = Time.deltaTime;

        CalculateAverageMusicianQuality();
        CalculatePseudoScore();

        intervalTimer += dt;
        if (intervalTimer >= audienceUpdateInterval)
        {
            float previousAudience = currentAudience;

            UpdateAudience(intervalTimer);

            CheckAudienceThresholds(previousAudience, currentAudience);

            intervalTimer = 0f;
        }
    }

    private void CheckAudienceThresholds(float prev, float curr)
    {
        CheckThreshold(prev, curr, 20f, onAudienceReach20);
        CheckThreshold(prev, curr, 50f, onAudienceReach50);
        CheckThreshold(prev, curr, 80f, onAudienceReach80);
    }

    private void CheckThreshold(float prev, float curr, float threshold, UnityEvent evt)
    {
        // Se activa al cruzar la barrera (subiendo desde abajo o bajando desde arriba)
        if ((prev < threshold && curr >= threshold) || (prev > threshold && curr <= threshold))
        {
            evt?.Invoke();
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
        if (orchestraManager == null)
        {
            averageMusicianQuality = 1f;
            return;
        }

        averageMusicianQuality = Mathf.Clamp01(orchestraManager.OrchestraQuality);
    }

    private void CalculatePseudoScore()
    {
        if (scoreManager == null) return;

        float rhythmFitness = 1f - (Mathf.Abs(scoreManager.CurrentRhythm - 100f) / 100f);
        rhythmFitness = Mathf.Clamp01(rhythmFitness);

        float normalizedScore = (rhythmFitness * rhythmWeight) + (averageMusicianQuality * musicianQualityWeight);
        pseudoScore = normalizedScore * 100f;
    }

    public void ActivarSimulacion(bool activar)
    {
        simulacionActiva = activar;
        intervalTimer = 0f;
    }

    private void UpdateAudience(float stepTime)
    {
        float audienceRatio = Mathf.Clamp01(currentAudience / maxAudience);

        float gainExtremeFactor = Mathf.Max(minExtremeSpeedFactor, 1f - audienceRatio);
        float lossExtremeFactor = Mathf.Max(minExtremeSpeedFactor, audienceRatio);

        if (pseudoScore >= neutralThresholdScore)
        {
            float performanceBonus = (pseudoScore - neutralThresholdScore) / (100f - neutralThresholdScore);
            float percentageGain = currentAudience * baseGainPercentage * performanceBonus;
            float rawGainRate = Mathf.Max(minAbsoluteChangeRate, percentageGain);
            float finalGain = rawGainRate * gainExtremeFactor * stepTime;
            currentAudience += finalGain;
        }
        else
        {
            float performancePenalty = (neutralThresholdScore - pseudoScore) / neutralThresholdScore;
            float percentageLoss = currentAudience * baseLossPercentage * performancePenalty;
            float rawLossRate = Mathf.Max(minAbsoluteChangeRate, percentageLoss);
            float finalLoss = rawLossRate * lossExtremeFactor * stepTime;
            currentAudience -= finalLoss;
        }

        float naturalLoss = currentAudience * naturalFatigueDecay * stepTime;
        currentAudience -= naturalLoss;

        currentAudience = Mathf.Clamp(currentAudience, 0f, maxAudience);
    }

    public void ApplyNetworkSync(float audience, float score)
    {
        currentAudience = audience;
        pseudoScore = score;
    }
}