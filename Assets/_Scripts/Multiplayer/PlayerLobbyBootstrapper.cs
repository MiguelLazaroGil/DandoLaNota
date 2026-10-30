using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLobbyBootstrapper : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(SpawnAvatarRoutine());
        }
    }

    private IEnumerator SpawnAvatarRoutine()
    {
        // 1. Esperar a que el gestor de estado exista y esté activo en red
        while (LobbyPlayerState.Instance == null || !LobbyPlayerState.Instance.IsSpawned)
        {
            yield return null;
        }

        // 2. Esperar confirmación de escena SOLO si es un cliente remoto (Late-joiner).
        // El Host ya tiene la escena cargada y no necesita esperar a su propio evento.
        if (SceneManager.GetActiveScene().buildIndex != 0 && OwnerClientId != NetworkManager.Singleton.LocalClientId)
        {
            bool clientSceneReady = false;

            void OnSceneEvent(SceneEvent sceneEvent)
            {
                if (sceneEvent.ClientId == OwnerClientId &&
                    sceneEvent.SceneEventType == SceneEventType.LoadComplete)
                {
                    clientSceneReady = true;
                }
            }

            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;

            float timeout = 0f;
            while (!clientSceneReady && timeout < 5f)
            {
                timeout += Time.deltaTime;
                yield return null;
            }

            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }

        // 3. Esperar a que el spawner de avatares esté disponible
        while (LobbyAvatarSpawner.Instance == null)
        {
            yield return null;
        }

        // 4. Asignar rol (Director para Host, Fantasma para el resto)
        PlayerRole role = LobbyPlayerState.Instance.GetOrAssignInitialRole(OwnerClientId);

        // 5. Instanciar el avatar en la escena de juego
        LobbyAvatarSpawner.Instance.SpawnAvatarForClient(OwnerClientId, role);

        // 6. Despawn seguro del bootstrapper
        yield return null;

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}