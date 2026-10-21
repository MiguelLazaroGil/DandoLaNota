using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNetworkSetup : NetworkBehaviour
{
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private List<MonoBehaviour> otherScripts = new List<MonoBehaviour>();
    [SerializeField] private List<GameObject> otherGameobjects = new List<GameObject>();
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            this.name = gameObject.name + "jugador activo";
            // Activar componentes locales solo para el jugador que controla este personaje
            if (playerCamera != null) playerCamera.enabled = true;
            if (audioListener != null) audioListener.enabled = true;
            if (playerInput != null) playerInput.enabled = true;
            foreach (var script in otherScripts)
            {
                script.enabled = true;
            }
            foreach (var go in otherGameobjects)
            {
                go.SetActive(true);
            }
            // 2. Ocultar la cámara estática del Lobby/Menú
            LobbyCameraManager.Instance?.HideLobbyCamera();

            // 3. Posicionar en un SpawnPoint de la sala
            PositionPlayerAtSpawnPoint();
        }
        else
        {
            // Desactivar en copias de otros jugadores para evitar conflicto de cámaras/inputs
            if (playerCamera != null) playerCamera.enabled = false;
            if (audioListener != null) audioListener.enabled = false;
            if (playerInput != null) playerInput.enabled = false;
            foreach (var script in otherScripts)
            {
                script.enabled = false;
            }
            foreach(var go in otherGameobjects)
            {
                go.SetActive(false);
            }
        }
    }
    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            // Al salir de la sala o destruirse el avatar local, reactivar la cámara estática del menú
            LobbyCameraManager.Instance?.ShowLobbyCamera();
        }
    }
    private void PositionPlayerAtSpawnPoint()
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spawnPoints.Length > 0)
        {
            int index = (int)(OwnerClientId % (ulong)spawnPoints.Length);
            Transform targetPoint = spawnPoints[index].transform;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = targetPoint.position;
            transform.rotation = targetPoint.rotation;

            if (cc != null) cc.enabled = true;
        }
    }
}