using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LobbyAvatarSpawner : NetworkBehaviour
{
    public static LobbyAvatarSpawner Instance { get; private set; }

    [Header("Prefabs del Lobby")]
    [SerializeField] private GameObject directorPrefab;
    [SerializeField] private GameObject ghostPrefab;

    private readonly Dictionary<ulong, GameObject> _spawnedAvatars = new Dictionary<ulong, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SpawnAvatarForClient(ulong clientId, PlayerRole role)
    {
        if (!IsServer) return;

        // Si ya existe un avatar para este cliente, lo eliminamos primero
        DespawnAvatarForClient(clientId);

        GameObject prefabToSpawn = (role == PlayerRole.Director) ? directorPrefab : ghostPrefab;
        Vector3 spawnPos = GetSpawnPosition(clientId);

        GameObject avatarInstance = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

        // Spawnear en red otorgando autoridad al cliente
        avatarInstance.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);

        _spawnedAvatars[clientId] = avatarInstance;
    }

    public void DespawnAvatarForClient(ulong clientId)
    {
        if (!IsServer) return;

        if (_spawnedAvatars.TryGetValue(clientId, out GameObject currentAvatar))
        {
            if (currentAvatar != null)
            {
                var netObj = currentAvatar.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
            }
            _spawnedAvatars.Remove(clientId);
        }
    }

    private Vector3 GetSpawnPosition(ulong clientId)
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spawnPoints.Length > 0)
        {
            int index = (int)(clientId % (ulong)spawnPoints.Length);
            return spawnPoints[index].transform.position;
        }
        return Vector3.zero;
    }
}