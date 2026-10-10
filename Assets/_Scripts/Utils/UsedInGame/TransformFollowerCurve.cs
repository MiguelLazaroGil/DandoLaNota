using UnityEngine;

public class TransformFollowerCurve : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("El Transform al que este objeto va a seguir.")]
    [SerializeField] private Transform target;
    [Tooltip("Desplazamiento respecto al objetivo (calculado en su espacio local para que rote con él).")]
    [SerializeField] private Vector3 offset = Vector3.zero;

    [Header("Configuración de la Curva")]
    [Tooltip("Eje X: Distancia normalizada (0 = muy cerca, 1 = muy lejos o más allá de la distancia máxima).\nEje Y: Multiplicador de velocidad.")]
    [SerializeField] private AnimationCurve followCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Distancia máxima considerada para normalizar la curva (1 en el eje X de la curva).")]
    [SerializeField] private float maxDistance = 10f;

    [Header("Opciones de Movimiento")]
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;
    [SerializeField] private float baseSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null) return;

        // Calculamos la posición de destino exacta aplicando el offset respecto al objetivo
        Vector3 targetPosition = target.TransformPoint(offset);

        // 1. Calculamos la distancia actual hacia el punto de destino con offset
        float currentDistance = Vector3.Distance(transform.position, targetPosition);

        // 2. Normalizamos la distancia entre 0 y 1 para la curva
        float normalizedDistance = Mathf.Clamp01(currentDistance / maxDistance);

        // 3. Evaluamos la curva para obtener el multiplicador de velocidad
        float curveFactor = followCurve.Evaluate(normalizedDistance);

        // 4. Aplicamos el seguimiento de posición
        if (followPosition)
        {
            float currentSpeed = baseSpeed * curveFactor;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);
        }

        // 5. Aplicamos el seguimiento de rotación opcional
        if (followRotation)
        {
            float rotationSpeed = baseSpeed * curveFactor;
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, rotationSpeed * Time.deltaTime);
        }
    }
}