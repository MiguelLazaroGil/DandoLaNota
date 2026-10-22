using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNetworkSetup : NetworkBehaviour
{

    [SerializeField] private List<MonoBehaviour> scripts = new();
    [SerializeField] private List<GameObject> gameobjects = new();
    [SerializeField] private List<Rigidbody> rigidbodies = new();
    [SerializeField] private PlayerMeshHidder playerMeshHidder;
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            this.name = gameObject.name + "jugador activo";

            foreach (var script in scripts)
            {
                script.enabled = true;
            }
            foreach (var go in gameobjects)
            {
                go.SetActive(true);
            }
            playerMeshHidder.HideMeshes();

            // 2. Ocultar la c�mara est�tica del Lobby/Men�
            LobbyCameraManager.Instance?.HideLobbyCamera();

            // 3. Posicionar en un SpawnPoint de la sala
            PositionPlayerAtSpawnPoint();
        }
        else
        {

            foreach (var script in scripts)
            {
                script.enabled = false;
            }
            foreach (var go in gameobjects)
            {
                go.SetActive(false);
            }
            // Para los dem�s jugadores, desactivamos la f�sica pura para que mande la red
            foreach (var rb in rigidbodies)
            {
                rb.isKinematic = true;
            }
            playerMeshHidder.ShowMeshes();


        }
    }
    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            // Al salir de la sala o destruirse el avatar local, reactivar la c�mara est�tica del men�
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