using UnityEngine;
using UnityEngine.UI;

public class FakeInputSender : MonoBehaviour
{
    [Header("--- REFERENCIAS ---")]
    [SerializeField] private MusicScoreManager scoreManager;

    [Header("--- INPUTS DE DIRECTOR ---")]
    [SerializeField] private Slider rhythmSlider;
    [SerializeField] private Slider qualitySlider;
    [SerializeField] private Toggle continuousInputToggle;
    [SerializeField] private Button sendInputButton;

    [Header("--- EVENTO NEGATIVO DE FANTASMA ---")]
    [SerializeField] private Slider negativeSeveritySlider;
    [SerializeField] private Button triggerNegativeButton;

    [Header("--- TEXTOS DE VALOR SLIDER (OPCIONAL) ---")]
    [SerializeField] private Text rhythmValueText;
    [SerializeField] private Text qualityValueText;
    [SerializeField] private Text negativeSeverityValueText;

    private void Start()
    {
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<MusicScoreManager>();

        if (sendInputButton != null)
            sendInputButton.onClick.AddListener(SendSingleInput);

        if (triggerNegativeButton != null)
            triggerNegativeButton.onClick.AddListener(SendNegativeEvent);

        // Configurar rangos por código por seguridad
        if (rhythmSlider != null) { rhythmSlider.minValue = 0f; rhythmSlider.maxValue = 200f; }
        if (qualitySlider != null) { qualitySlider.minValue = 0f; qualitySlider.maxValue = 1f; }
        if (negativeSeveritySlider != null) { negativeSeveritySlider.minValue = 0f; negativeSeveritySlider.maxValue = 100f; }
    }

    private void Update()
    {
        // Actualizar textos auxiliares de lectura de sliders
        if (rhythmValueText != null && rhythmSlider != null)
            rhythmValueText.text = $"Ritmo Target: {rhythmSlider.value:F0}";

        if (qualityValueText != null && qualitySlider != null)
            qualityValueText.text = $"Calidad Target: {qualitySlider.value:F2}";

        if (negativeSeverityValueText != null && negativeSeveritySlider != null)
            negativeSeverityValueText.text = $"Gravedad Evento: {negativeSeveritySlider.value:F0}";

        // Enviar input continuo cada frame si el Toggle está activo
        if (continuousInputToggle != null && continuousInputToggle.isOn)
        {
            SendSingleInput();
        }
    }

    public void SendSingleInput()
    {
        if (scoreManager == null) return;

        float rhythm = rhythmSlider != null ? rhythmSlider.value : 100f;
        float quality = qualitySlider != null ? qualitySlider.value : 1f;

        scoreManager.ReceiveDirectorInput(rhythm, quality);
    }

    public void SendNegativeEvent()
    {
        if (scoreManager == null) return;

        float severity = negativeSeveritySlider != null ? negativeSeveritySlider.value : 50f;
        scoreManager.TriggerNegativeEvent(severity);
    }
}