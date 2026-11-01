using UnityEngine;

public class CreditsScroller : MonoBehaviour
{
    [Header("Configuración de Velocidad")]
    [Tooltip("Velocidad a la que se desplazan los créditos hacia arriba.")]
    [SerializeField] private float scrollSpeed = 50f;

    [Header("Límite")]
    [Tooltip("Posición máxima en Y donde se detendrán los créditos.")]
    [SerializeField] private float maxYPosition = 2000f;

    private RectTransform rectTransform;
    private Vector2 initialAnchoredPosition;
    private bool isScrolling = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Guardamos la posición inicial en el primer arranque
            initialAnchoredPosition = rectTransform.anchoredPosition;
        }
    }

    void OnEnable()
    {
        // Cada vez que se activa el objeto, se coloca en el inicio y empieza a reproducir
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = initialAnchoredPosition;
        }
        isScrolling = true;
    }

    void OnDisable()
    {
        // Cada vez que se desactiva, se asegura de restablecer la posición inicial
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = initialAnchoredPosition;
        }
        isScrolling = false;
    }

    void Update()
    {
        if (!isScrolling || rectTransform == null) return;

        // Desplaza el RectTransform hacia arriba en el eje Y
        Vector2 currentPosition = rectTransform.anchoredPosition;
        currentPosition.y += scrollSpeed * Time.deltaTime;
        rectTransform.anchoredPosition = currentPosition;

        // Comprueba si ha llegado al límite para detener el scroll
        if (currentPosition.y >= maxYPosition)
        {
            isScrolling = false;
            OnCreditsFinished();
        }
    }

    private void OnCreditsFinished()
    {
        // Aquí puedes cargar otra escena o mostrar un menú cuando terminen los créditos
        Debug.Log("Los créditos han finalizado.");
    }
}