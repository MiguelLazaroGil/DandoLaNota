using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbyPlayerState : NetworkBehaviour
{
    public static LobbyPlayerState Instance { get; private set; }

    public NetworkList<PlayerLobbyData> ConnectedPlayers;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ConnectedPlayers = new NetworkList<PlayerLobbyData>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ConnectedPlayers.Clear();
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            StartCoroutine(UpdatePingCoroutine());
        }

        if (IsClient)
        {
            // Nada más aparecer en la red, enviamos nuestro nombre de la UI al servidor
            string myName = LobbyUIManager.Instance != null ? LobbyUIManager.Instance.GetCurrentPlayerName() : "";
            SetPlayerNameRpc(myName);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            ConnectedPlayers.Clear();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (LobbyAvatarSpawner.Instance != null)
        {
            LobbyAvatarSpawner.Instance.DespawnAvatarForClient(clientId);
        }

        for (int i = 0; i < ConnectedPlayers.Count; i++)
        {
            if (ConnectedPlayers[i].ClientId == clientId)
            {
                ConnectedPlayers.RemoveAt(i);
                break;
            }
        }
    }

    // --- REGISTRO Y ACTUALIZACIÓN DE JUGADOR MEDIANTE RPC ---
    [Rpc(SendTo.Server)]
    public void SetPlayerNameRpc(string playerName, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        string finalName = string.IsNullOrWhiteSpace(playerName) ? "" : playerName;

        // 1. Si el jugador ya existe en la lista, actualizamos su nombre
        for (int i = 0; i < ConnectedPlayers.Count; i++)
        {
            if (ConnectedPlayers[i].ClientId == senderId)
            {
                var data = ConnectedPlayers[i];
                data.PlayerName = finalName;
                ConnectedPlayers[i] = data; // Replicación automática vía NetworkList
                return;
            }
        }

        // 2. Si no existe, determinamos su rol e instanciamos su entrada por primera vez
        bool hasDirector = false;
        foreach (var p in ConnectedPlayers)
        {
            if (p.Role == PlayerRole.Director)
            {
                hasDirector = true;
                break;
            }
        }

        PlayerRole initialRole = hasDirector ? PlayerRole.Ghost : PlayerRole.Director;

        // Añadir a la lista con el nombre directamente
        ConnectedPlayers.Add(new PlayerLobbyData(senderId, finalName, 0, initialRole));

        // Spawnear avatar del personaje en el lobby
        if (LobbyAvatarSpawner.Instance != null)
        {
            LobbyAvatarSpawner.Instance.SpawnAvatarForClient(senderId, initialRole);
        }
    }

    // --- CAMBIAR ROL (DIRECTOR / FANTASMA) ---
    [Rpc(SendTo.Server)]
    public void RequestToggleRoleRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < ConnectedPlayers.Count; i++)
        {
            if (ConnectedPlayers[i].ClientId == senderId)
            {
                var data = ConnectedPlayers[i];
                data.Role = (data.Role == PlayerRole.Ghost) ? PlayerRole.Director : PlayerRole.Ghost;
                ConnectedPlayers[i] = data;

                if (LobbyAvatarSpawner.Instance != null)
                {
                    LobbyAvatarSpawner.Instance.SpawnAvatarForClient(senderId, data.Role);
                }
                break;
            }
        }
    }

    // --- FORZAR DIRECTOR DESDE EL HOST ---
    [Rpc(SendTo.Server)]
    public void ForceSetDirectorRpc(ulong targetClientId)
    {
        for (int i = 0; i < ConnectedPlayers.Count; i++)
        {
            var data = ConnectedPlayers[i];
            PlayerRole newRole = (data.ClientId == targetClientId) ? PlayerRole.Director : PlayerRole.Ghost;

            if (data.Role != newRole)
            {
                data.Role = newRole;
                ConnectedPlayers[i] = data;

                if (LobbyAvatarSpawner.Instance != null)
                {
                    LobbyAvatarSpawner.Instance.SpawnAvatarForClient(data.ClientId, newRole);
                }
            }
        }
    }

    // --- RESOLUCIÓN DE ROLES ANTES DE CARGAR ESCENA DE JUEGO ---
    public void ResolveRolesBeforeStart()
    {
        if (!IsServer) return;

        List<int> directorIndices = new List<int>();

        for (int i = 0; i < ConnectedPlayers.Count; i++)
        {
            if (ConnectedPlayers[i].Role == PlayerRole.Director)
            {
                directorIndices.Add(i);
            }
        }

        if (directorIndices.Count == 0)
        {
            int randomIndex = Random.Range(0, ConnectedPlayers.Count);
            var data = ConnectedPlayers[randomIndex];
            data.Role = PlayerRole.Director;
            ConnectedPlayers[randomIndex] = data;
        }
        else if (directorIndices.Count > 1)
        {
            int chosenDirectorIndex = directorIndices[Random.Range(0, directorIndices.Count)];

            for (int i = 0; i < ConnectedPlayers.Count; i++)
            {
                var data = ConnectedPlayers[i];
                data.Role = (i == chosenDirectorIndex) ? PlayerRole.Director : PlayerRole.Ghost;
                ConnectedPlayers[i] = data;
            }
        }
    }

    // --- MEDICIÓN DE PING (RTT) ---
    private IEnumerator UpdatePingCoroutine()
    {
        var wait = new WaitForSeconds(1.0f);

        while (IsServer)
        {
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;

            for (int i = 0; i < ConnectedPlayers.Count; i++)
            {
                var data = ConnectedPlayers[i];
                if (data.ClientId == NetworkManager.Singleton.LocalClientId)
                {
                    data.PingMs = 0;
                }
                else
                {
                    data.PingMs = transport.GetCurrentRtt(data.ClientId);
                }
                ConnectedPlayers[i] = data;
            }

            yield return wait;
        }
    }
}