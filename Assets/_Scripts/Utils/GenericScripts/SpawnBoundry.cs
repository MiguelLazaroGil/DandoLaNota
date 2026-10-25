using MLG.DelayedActionsTool;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class SpawnBoundary : MonoBehaviour
{
    [Tooltip("Distancia máxima del radio (ej: 200 metros).")]
    [SerializeField] private float maxDistance = 200f;

    private SphereCollider boundaryTrigger;

    void Awake()
    {
        boundaryTrigger = GetComponent<SphereCollider>();
        boundaryTrigger.isTrigger = true;
        boundaryTrigger.radius = maxDistance; // Ajusta el radio automáticamente
    }

    private void OnTriggerExit(Collider other)
    {
        // Cuando un objeto abandona la esfera de los 200 metros...
        ReturnToSpawn returnScript = other.GetComponent<ReturnToSpawn>();

        if (returnScript != null)
        {
            if (!returnScript.enabled) return;
            returnScript.TeleportToSpawn();
        }
    }

}