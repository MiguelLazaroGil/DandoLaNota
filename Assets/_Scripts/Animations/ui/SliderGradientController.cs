using UnityEngine;
using UnityEngine.UI;

public class SliderGradientController : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("Referencia al componente Slider.")]
    [SerializeField] private Slider slider;
    [Tooltip("Referencia a la imagen de relleno (Fill) del slider.")]
    [SerializeField] private Image fillImage;

    [Header("Configuración de Color")]
    [Tooltip("Gradiente de color que cambiará según el porcentaje del slider (0 = inicio, 1 = final).")]
    [SerializeField] private Gradient colorGradient;
    [SerializeField] AudienceManager audienceManager;

    void Start()
    {
        // Si no se asignó en el Inspector, los busca automáticamente
        if (slider == null) slider = GetComponent<Slider>();

        if (fillImage == null && slider.fillRect != null)
        {
            fillImage = slider.fillRect.GetComponent<Image>();
        }
        if (audienceManager == null)
        {
            audienceManager = FindAnyObjectByType<AudienceManager>();
            if (audienceManager == null)
            {
                gameObject.SetActive(false);
            }
        }
        // Configuramos los límites exactos de 1 a 100
        slider.minValue = 1f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true; // Opcional: marca esto si quieres que sean solo números enteros
    }

    void Update()
    {
        // 1. OBTENER EL VALOR DESDE TU MANAGER
        // Reemplaza esta línea con la llamada real a tu manager (ej. GameManager.Instance.PlayerHealth)
        float valueFromManager = GetValueFromManager();

        // 2. ACTUALIZAR EL VALOR DEL SLIDER
        slider.value = valueFromManager;

        // 3. ACTUALIZAR EL COLOR DEL GRADIENTE
        UpdateSliderColor();
    }

    private void UpdateSliderColor()
    {
        if (fillImage == null || colorGradient == null) return;

        // slider.normalizedValue devuelve un valor entre 0.0 y 1.0 basado en el min y max
        float normalizedValue = slider.normalizedValue;

        // Evaluamos el gradiente con ese porcentaje y aplicamos el color al fill
        fillImage.color = colorGradient.Evaluate(normalizedValue);
    }

    private float GetValueFromManager()
    {

        return audienceManager.PseudoScore;
    }
}