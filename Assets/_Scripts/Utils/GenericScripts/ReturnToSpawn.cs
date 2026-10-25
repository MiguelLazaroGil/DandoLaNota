using UnityEngine;

public class ReturnToSpawn : MonoBehaviour
{
    private Vector3 spawnPosition;
    [SerializeField]
    private Rigidbody rb;
    public bool IsHeld { get; set; } = false;

    void Start()
    {
        // Guardamos la posición inicial exacta al arrancar
        spawnPosition = transform.position;
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    // Método público para que el Trigger pueda llamarlo
    public void TeleportToSpawn()
    {
        if (IsHeld) return;
        if (rb != null)
        {
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true; // Temporalmente cinemático para asegurar el teletransporte

            // Limpiamos inercias y velocidades para que no salga disparado al reaparecer
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;

            // Movemos el transform y la física del Rigidbody
            transform.position = spawnPosition;
            rb.position = spawnPosition;

            rb.isKinematic = wasKinematic; // Restauramos su estado físico original
        }
        else
        {
            transform.position = spawnPosition;
        }
    }
}