using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnPosition : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Solo el servidor o el dueño debe calcular/aplicar la posición inicial
        if (IsOwner)
        {
            // Buscamos los puntos de spawn etiquetados en la escena
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

            if (spawnPoints.Length > 0)
            {
                // Seleccionar un punto basado en el ClientId para que no aparezcan pegados
                int index = (int)(OwnerClientId % (ulong)spawnPoints.Length);
                Transform targetSpawn = spawnPoints[index].transform;

                // Desactivar CharacterController/Rigidbody temporalmente para posicionar bien
                CharacterController cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                transform.position = targetSpawn.position;
                transform.rotation = targetSpawn.rotation;

                if (cc != null) cc.enabled = true;
            }
        }
    }
}