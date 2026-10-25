using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using MLG.SimpleSceneTool;

public class PlayerVisibilityController : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private string ghostLayerName = "Ghost";
    [SerializeField] private SimpleScene lobbyScene;

    public override void OnNetworkSpawn()
    {
        // Solo el cliente local debe ajustar su propia cámara de pantalla
        if (!IsOwner) return;

        UpdateCameraVisibility();

        // Escuchar cuando Netcode cambie de escena para actualizar la máscara
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Al terminar de cargar cualquier escena (del lobby a partida o viceversa)
        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && IsOwner)
        {
            UpdateCameraVisibility();
        }
    }

    public void UpdateCameraVisibility()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null) return;

        int ghostLayerIndex = LayerMask.NameToLayer(ghostLayerName);
        if (ghostLayerIndex == -1)
        {
            Debug.LogWarning($"La capa '{ghostLayerName}' no existe en la lista de Layers de Unity.");
            return;
        }

        // Detectar si estamos en el lobby o en la escena de juego
        // (Ajusta la comprobación según si usas índices o nombres de escena)
        bool isInGameScene = SceneManager.GetActiveScene().buildIndex != lobbyScene.Index;
        Debug.Log("Estamos en la escena " + SceneManager.GetActiveScene().buildIndex + " y la escena de lobby es " + lobbyScene.Index+ " por lo que isInGameScene es"+ isInGameScene);

        PlayerRole myRole = GetLocalPlayerRole();

        if (isInGameScene && myRole == PlayerRole.Director)
        {
            // EN PARTIDA + DIRECTOR: Apagar la capa "Ghost" en la cámara local (Operación Bitwise)
            playerCamera.cullingMask &= ~(1 << ghostLayerIndex);
        }
    }

    private PlayerRole GetLocalPlayerRole()
    {
        if (LobbyPlayerState.Instance != null)
        {
            foreach (var player in LobbyPlayerState.Instance.ConnectedPlayers)
            {
                if (player.ClientId == NetworkManager.Singleton.LocalClientId)
                {
                    return player.Role;
                }
            }
        }
        return PlayerRole.Ghost;
    }
}