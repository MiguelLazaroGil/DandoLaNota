using UnityEngine;
using TMPro; // Necesario para usar TextMeshPro

public class TextManagerDisplay : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("Referencia al componente TextMeshProUGUI.")]
    [SerializeField] private TextMeshProUGUI tmpText;

    [Header("Configuración de Texto")]
    [Tooltip("El texto fijo que aparecerá antes del valor (ej: 'Puntuación: ').")]
    [SerializeField] private string labelText = "Puntuación: ";
    [Tooltip("Formato numérico opcional (ej: 'F0' para enteros, 'F2' para 2 decimales).")]
    [SerializeField] private string numberFormat = "F0";
    [SerializeField] AudienceManager audienceManager;
    void Start()
    {
        // Si no se asignó en el Inspector, lo busca automáticamente en el mismo objeto
        if (tmpText == null)
        {
            tmpText = GetComponent<TextMeshProUGUI>();
        }
        if (audienceManager == null)
        {
            audienceManager = FindAnyObjectByType<AudienceManager>();
            if (audienceManager == null)
            {
                gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        // 1. OBTENER EL VALOR DESDE TU MANAGER
        float valueFromManager = GetValueFromManager();

        // 2. ACTUALIZAR EL TEXTO CONCATENADO
        if (tmpText != null)
        {
            tmpText.text = labelText + valueFromManager.ToString(numberFormat);
        }
    }

    private float GetValueFromManager()
    {

        return audienceManager.currentAudience;
    }
}