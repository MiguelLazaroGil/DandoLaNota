using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class CanvasGroupFadeCurve : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("CanvasGroup que se va a animar. Si se deja vacío, se buscará automáticamente en este GameObject.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Configuración de la Curva")]
    [Tooltip("Eje X: Tiempo normalizado (de 0 a 1).\nEje Y: Valor del Alfa (de 0 a 1).")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Duración total de la animación en segundos.")]
    [SerializeField] private float duration = 1.0f;

    [Tooltip("Si está activo, el fundido se ejecuta automáticamente al activarse / arrancar la escena.")]
    [SerializeField] private bool playOnAwake = true;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        // Si no se asignó en el inspector, lo obtenemos del componente
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (playOnAwake)
        {
            PlayFade();
        }
    }

    /// <summary>
    /// Inicia o reinicia la animación de fundido de alfa basada en la curva.
    /// </summary>
    public void PlayFade()
    {
        if (canvasGroup == null) return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Usamos unscaledDeltaTime por si el juego está pausado (Time.timeScale = 0)
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            // Evaluamos la curva: el eje Y dictará directamente el valor de alfa (0.0 a 1.0)
            canvasGroup.alpha = fadeCurve.Evaluate(normalizedTime);

            yield return null;
        }

        // Aseguramos el valor final exacto al terminar
        canvasGroup.alpha = fadeCurve.Evaluate(1f);
    }
}