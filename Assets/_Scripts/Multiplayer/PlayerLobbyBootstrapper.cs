using Unity.Netcode;
using UnityEngine;

public class PlayerLobbyBootstrapper : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Solo el servidor/host decide e instancia el avatar definitivo
        if (IsServer)
        {
            // 1. Consultar o asignar el rol inicial para este jugador
            PlayerRole role = LobbyPlayerState.Instance != null
                ? LobbyPlayerState.Instance.GetOrAssignInitialRole(OwnerClientId)
                : PlayerRole.Director;

            // 2. Instanciar el avatar real (Director o Fantasma)
            if (LobbyAvatarSpawner.Instance != null)
            {
                LobbyAvatarSpawner.Instance.SpawnAvatarForClient(OwnerClientId, role);
            }

            // 3. El Bootstrapper cumple su función y se destruye en la red
            GetComponent<NetworkObject>().Despawn(true);
        }
    }
}