using Unity.Netcode;
using UnityEngine;

public class GameSpawnManager : NetworkBehaviour
{
    [Header("Prefabs de Personaje")]
    [SerializeField] private GameObject directorPrefab;
    [SerializeField] private GameObject ghostPrefab;

    public override void OnNetworkSpawn()
    {
        // Solo el Servidor/Host engendra (spawn) los avatares para cada cliente
        if (!IsServer) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        foreach (var player in LobbyPlayerState.Instance.ConnectedPlayers)
        {
            GameObject prefabToSpawn = (player.Role == PlayerRole.Director) ? directorPrefab : ghostPrefab;

            // Determinar posición de aparición
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (spawnPoints.Length > 0)
            {
                int index = (int)(player.ClientId % (ulong)spawnPoints.Length);
                spawnPos = spawnPoints[index].transform.position;
                spawnRot = spawnPoints[index].transform.rotation;
            }

            GameObject spawnedPlayer = Instantiate(prefabToSpawn, spawnPos, spawnRot);

            // Asignar el control del NetworkObject al cliente correspondiente
            spawnedPlayer.GetComponent<NetworkObject>().SpawnWithOwnership(player.ClientId);
        }
    }
}