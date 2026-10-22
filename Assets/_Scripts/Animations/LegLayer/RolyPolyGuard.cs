using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RolyPolyGuard : MonoBehaviour
{
    [Header("Referencias")]

    [SerializeField] private List<MonoBehaviour> deactivateScripts;

    [Header("Configuración de Inclinación")]
    [Tooltip("Ángulo máximo en grados que se puede inclinar antes de desactivarse.")]
    [SerializeField] private float maxTiltAngle = 50f;

    [Tooltip("Tiempo (en segundos) que el roly-poly permanecerá desactivado.")]
    [SerializeField] private float disableDuration = 2f;
    [SerializeField] private float coolDownDuration = 4f;

    private bool isDeactivated = false;
    private Coroutine cooldownCoroutine;

    void Update()
    {
        if (isDeactivated) return;

        // Medimos el ángulo de inclinación del eje vertical del personaje (transform.up) 
        // respecto a la vertical absoluta del mundo (Vector3.up). 
        // Esto cubre de forma nativa la combinación de los ejes X y Z.
        float currentTilt = Vector3.Angle(transform.up, Vector3.up);

        // Si la inclinación supera el umbral permitido
        if (currentTilt > maxTiltAngle)
        {
            TriggerDisable();
        }
    }

    void TriggerDisable()
    {
        if (deactivateScripts.Count > 0)
        {

            TriggerScripts(false);
            isDeactivated = true;

            // Si ya había una corrutina corriendo, la reiniciamos
            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }

            cooldownCoroutine = StartCoroutine(ReenableRoutine());
        }
    }

    IEnumerator ReenableRoutine()
    {
        // Espera el tiempo configurado
        yield return new WaitForSeconds(disableDuration);

        // Vuelve a activar el script de roly-poly
        TriggerScripts(true);
        yield return new WaitForSeconds(coolDownDuration);
        isDeactivated = false;

    }
    public void TriggerScripts(bool trigger)
    {
        foreach (var script in deactivateScripts)
        {
            script.enabled = trigger;
        }
    }
}