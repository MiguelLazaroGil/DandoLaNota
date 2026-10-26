using UnityEngine;

public class SoftNeckConstraint : MonoBehaviour
{
    [Header("Referencia Base")]
    [Tooltip("El cuerpo o la base sobre la cual rota el cuello.")]
    [SerializeField] private Transform baseTransform;

    [Header("Límites del Cuello (Grados)")]
    [Tooltip("Límite máximo hacia arriba y abajo (Eje X).")]
    [SerializeField] private float maxVerticalAngle = 45f;
    
    [Tooltip("Límite máximo hacia la izquierda y derecha (Eje Y).")]
    [SerializeField] private float maxHorizontalAngle = 60f;
    
    [Tooltip("Límite de torsión o giro sobre sí mismo (Eje Z).")]
    [SerializeField] private float maxTwistAngle = 15f;

    [Header("Flexibilidad")]
    [Range(1f, 50f)]
    [SerializeField] private float flexibility = 20f; // Qué tan elástico o firme se siente al llegar al límite

    void LateUpdate()
    {
        if (baseTransform == null) return;

        // 1. Obtenemos la rotación actual del objeto relativa a la base
        Quaternion localRot = Quaternion.Inverse(baseTransform.rotation) * transform.rotation;
        
        // 2. Convertimos a ángulos de Euler para poder limitar cada eje por separado
        Vector3 euler = localRot.eulerAngles;
        
        euler.x = NormalizeAngle(euler.x);
        euler.y = NormalizeAngle(euler.y);
        euler.z = NormalizeAngle(euler.z);

        // 3. Aplicamos los límites tipo cuello (Clamp)
        euler.x = Mathf.Clamp(euler.x, -maxVerticalAngle, maxVerticalAngle);
        euler.y = Mathf.Clamp(euler.y, -maxHorizontalAngle, maxHorizontalAngle);
        euler.z = Mathf.Clamp(euler.z, -maxTwistAngle, maxTwistAngle);

        // 4. Reconstruimos la rotación limitada deseada
        Quaternion targetLocalRot = Quaternion.Euler(euler);
        Quaternion targetWorldRot = baseTransform.rotation * targetLocalRot;

        // 5. Aplicamos la rotación con una suave interpolación para darle efecto de flexibilidad
        transform.rotation = Quaternion.Slerp(transform.rotation, targetWorldRot, Time.deltaTime * flexibility);
    }

    // Función auxiliar para normalizar los ángulos de Unity (0-360 a -180-180)
    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}