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

        // 2. Si el cliente se une tarde y estamos en la escena de juego,
        // esperamos a que el cliente confirme que ha terminado de cargar la escena por completo
        if (SceneManager.GetActiveScene().buildIndex != 0)
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
            while (!clientSceneReady && timeout < 10f)
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

        // 4. Asignar rol (en partida en curso, como ya hay Director, devolverá Fantasma)
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