using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AudienceManager : MonoBehaviour
{
    [Header("--- REFERENCIAS ---")]
    [Tooltip("Referencia al gestor de puntuaci�n global de la m�sica.")]
    [SerializeField] private MusicScoreManager scoreManager;
    [SerializeField] private OrchestraAudioManager orchestraManager;

    [Header("--- INTERVALO DE ACTUALIZACI�N DE AUDIENCIA ---")]
    [Tooltip("Tiempo en segundos entre cada refresco de la audiencia (ej: 0.5s para no refrescar constantemente cada frame).")]
    [SerializeField] private float audienceUpdateInterval = 0.5f;

    [Header("--- ESTADO EN VIVO (LECTURA) ---")]
    [Tooltip("Puntuaci�n simulada (0 a 100) calculada a partir del ritmo y la calidad individual.")]
    [SerializeField] private float pseudoScore = 100f;

    [Tooltip("Cantidad actual de audiencia presente en el espect�culo.")]
    [SerializeField] private float currentAudience = 50f;

    [Tooltip("Media actual calculada entre todos los m�sicos activos.")]
    [SerializeField] private float averageMusicianQuality = 1.0f;

    [Header("--- CONFIGURACI�N BASE DE AUDIENCIA ---")]
    [Tooltip("Audiencia inicial al comenzar el nivel.")]
    [SerializeField] private float initialAudience = 50f;

    [Tooltip("M�ximo n�mero posible de personas en la audiencia.")]
    [SerializeField] private float maxAudience = 100f;

    [Header("--- C�LCULO DE PSEUDO-SCORE ---")]
    [Tooltip("Peso del ritmo (0 a 1) en la pseudo-score final.")]
    [SerializeField, Range(0f, 1f)] private float rhythmWeight = 0.5f;

    [Tooltip("Peso de la calidad media de los m�sicos (0 a 1) en la pseudo-score final.")]
    [SerializeField, Range(0f, 1f)] private float musicianQualityWeight = 0.5f;

    [Header("--- DIN�MICA PORCENTUAL DE AUDIENCIA ---")]
    [Tooltip("Umbral de Pseudo-Score (0 a 100). Por encima se gana audiencia; por debajo, se pierde.")]
    [SerializeField] private float neutralThresholdScore = 85f;

    [Tooltip("Porcentaje de la audiencia actual ganado por segundo a rendimiento m�ximo (ej: 0.05 = 5%/s).")]
    [SerializeField, Range(0f, 1f)] private float baseGainPercentage = 0.05f;

    [Tooltip("Porcentaje de la audiencia actual perdido por segundo a rendimiento p�simo (ej: 0.08 = 8%/s).")]
    [SerializeField, Range(0f, 1f)] private float baseLossPercentage = 0.08f;

    [Tooltip("M�nimo absoluto de espectadores ganados/perdidos por segundo para evitar que se atasque con audiencia muy baja.")]
    [SerializeField] private float minAbsoluteChangeRate = 0.5f;

    [Tooltip("Desgaste natural del p�blico por segundo (0.01 = 1%/s). Garantiza la presi�n continua 'ad infinitum'.")]
    [SerializeField, Range(0f, 0.1f)] private float naturalFatigueDecay = 0.005f;

    [Header("--- RALENTIZACI�N EN EXTREMOS ---")]
    [Tooltip("Multiplicador m�nimo de velocidad cuando la audiencia est� rozando el 0 o el m�ximo.")]
    [SerializeField, Range(0.01f, 0.5f)] private float minExtremeSpeedFactor = 0.1f;

    // Estado interno
    private float intervalTimer = 0f;


    private List<IMusician> activeMusicians = new List<IMusician>();

    public float CurrentAudience => currentAudience;
    public float PseudoScore => pseudoScore;
    public float AverageMusicianQuality => averageMusicianQuality;
    private bool simulacionActiva = false;


    private void Start()
    {
        currentAudience = initialAudience;
        var _activeMusicians = orchestraManager.gameObject.GetComponentsInChildren<IMusician>();
        activeMusicians = activeMusicians.ToList<IMusician>();
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
        if (orchestraManager == null)
        {
            averageMusicianQuality = 1f;
            return;
        }

        averageMusicianQuality = Mathf.Clamp01(
            orchestraManager.OrchestraQuality
        );
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
        // Ratio de audiencia actual (0.0 a 1.0)
        float audienceRatio = Mathf.Clamp01(currentAudience / maxAudience);

        // Factores de atenuaci�n en l�mites:
        // - Al acercarse a maxAudience (ratio -> 1), la ganancia cae progresivamente hacia minExtremeSpeedFactor.
        // - Al acercarse a 0 (ratio -> 0), la p�rdida cae progresivamente hacia minExtremeSpeedFactor.
        float gainExtremeFactor = Mathf.Max(minExtremeSpeedFactor, 1f - audienceRatio);
        float lossExtremeFactor = Mathf.Max(minExtremeSpeedFactor, audienceRatio);

        if (pseudoScore >= neutralThresholdScore)
        {
            // Rendimiento positivo (0.0 a 1.0)
            float performanceBonus = (pseudoScore - neutralThresholdScore) / (100f - neutralThresholdScore);

            // Tasa porcentual + Suelo m�nimo absoluto
            float percentageGain = currentAudience * baseGainPercentage * performanceBonus;
            float rawGainRate = Mathf.Max(minAbsoluteChangeRate, percentageGain);

            // Aplicar atenuaci�n al acercarse al m�ximo
            float finalGain = rawGainRate * gainExtremeFactor * stepTime;
            currentAudience += finalGain;
        }
        else
        {
            // Rendimiento negativo (0.0 a 1.0)
            float performancePenalty = (neutralThresholdScore - pseudoScore) / neutralThresholdScore;

            // Tasa porcentual + Suelo m�nimo absoluto
            float percentageLoss = currentAudience * baseLossPercentage * performancePenalty;
            float rawLossRate = Mathf.Max(minAbsoluteChangeRate, percentageLoss);

            // Aplicar atenuaci�n al acercarse a 0
            float finalLoss = rawLossRate * lossExtremeFactor * stepTime;
            currentAudience -= finalLoss;
        }

        // Desgaste natural continuo (fatiga/desinter�s constante por el mero paso del tiempo)
        float naturalLoss = currentAudience * naturalFatigueDecay * stepTime;
        currentAudience -= naturalLoss;

        currentAudience = Mathf.Clamp(currentAudience, 0f, maxAudience);
    }
}