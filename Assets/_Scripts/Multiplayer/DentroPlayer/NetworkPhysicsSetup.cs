using Unity.Netcode;
using UnityEngine;

public class NetworkPhysicsSetup : NetworkBehaviour
{
    [SerializeField] private Rigidbody[] playerRigidbodies;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Para los demás jugadores, desactivamos la física pura para que mande la red
            foreach (var rb in playerRigidbodies)
            {
                rb.isKinematic = true;
            }
        }
    }
}