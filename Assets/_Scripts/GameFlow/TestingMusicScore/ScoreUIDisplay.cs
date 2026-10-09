using UnityEngine;
using UnityEngine.UI;

public class ScoreUIDisplay : MonoBehaviour
{
    [Header("--- REFERENCIAS ---")]
    [SerializeField] private MusicScoreManager scoreManager;
    [SerializeField] private AudienceManager audienceManager;

    [Header("--- TEXTOS DE VISUALIZACIÓN ---")]
    [SerializeField] private Text rhythmText;
    [SerializeField] private Text qualityText;
    [SerializeField] private Text hasInputText;
    [SerializeField] private Text musicianQualityText;
    [SerializeField] private Text pseudoScoreText;
    [SerializeField] private Text audienceText;

    private void Start()
    {
        if (scoreManager == null) scoreManager = FindFirstObjectByType<MusicScoreManager>();
        if (audienceManager == null) audienceManager = FindFirstObjectByType<AudienceManager>();
    }

    private void Update()
    {
        if (scoreManager != null)
        {
            if (rhythmText != null)
                rhythmText.text = $"Ritmo Actual: {scoreManager.CurrentRhythm:F1} / 200";

            if (qualityText != null)
                qualityText.text = $"Calidad General: {scoreManager.CurrentQuality:F2}";

            if (hasInputText != null)
                hasInputText.text = $"Estado Input: {(scoreManager.HasActiveInput ? "<color=green>RECIBIENDO</color>" : "<color=red>DESCONTROLANDO (NO INPUT)</color>")}";
        }

        if (audienceManager != null)
        {
            if (musicianQualityText != null)
                musicianQualityText.text = $"Media Músicos: {audienceManager.AverageMusicianQuality:F2}";

            if (pseudoScoreText != null)
                pseudoScoreText.text = $"Pseudo-Score: {audienceManager.PseudoScore:F1} / 100";

            if (audienceText != null)
                audienceText.text = $"Audiencia: {audienceManager.CurrentAudience:F0}";
        }
    }
}