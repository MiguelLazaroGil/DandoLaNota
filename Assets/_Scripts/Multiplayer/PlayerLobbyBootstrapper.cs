using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerLobbyBootstrapper : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Solo el servidor/host decide e instancia el avatar definitivo
        if (IsServer)
        {
            StartCoroutine(SpawnAvatarRoutine());
        }
    }

    private IEnumerator SpawnAvatarRoutine()
    {
        // Esperar a que el gestor de estado exista Y que Netcode haya completado su Spawn en red
        while (LobbyPlayerState.Instance == null || !LobbyPlayerState.Instance.IsSpawned)
        {
            yield return null;
        }

        // Esperar también a que el spawner de avatares esté disponible
        while (LobbyAvatarSpawner.Instance == null)
        {
            yield return null;
        }

        // Ahora es 100% seguro escribir en la NetworkList y spawnear el avatar
        PlayerRole role = LobbyPlayerState.Instance.GetOrAssignInitialRole(OwnerClientId);
        LobbyAvatarSpawner.Instance.SpawnAvatarForClient(OwnerClientId, role);

        // El Bootstrapper cumple su función y se destruye en la red
        GetComponent<NetworkObject>().Despawn(true);
    }
}