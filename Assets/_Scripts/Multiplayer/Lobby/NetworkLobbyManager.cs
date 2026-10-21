using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class NetworkLobbyManager : MonoBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    private string _currentLobbyId;
    private Coroutine _heartbeatCoroutine;

    private async void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeServicesAsync();
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private async Task InitializeServicesAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;

        InitializationOptions options = new InitializationOptions();
#if UNITY_EDITOR
        options.SetProfile($"Profile_{Random.Range(1000, 9999)}");
#endif
        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    // --- CREAR SALA (HOST) ---
    public async Task<bool> CreateLobbyAndHostAsync(string lobbyName, int maxPlayers, bool isPrivate, string password = null)
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Password = string.IsNullOrEmpty(password) ? null : password,
                Data = new Dictionary<string, DataObject>
                {
                    { "JOIN_CODE", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            _currentLobbyId = lobby.Id;

            _heartbeatCoroutine = StartCoroutine(LobbyHeartbeatCoroutine(lobby.Id, 15f));

            NetworkManager.Singleton.StartHost();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al crear el lobby: {e.Message}");
            return false;
        }
    }

    // --- BUSCAR SALAS PÚBLICAS ---
    public async Task<List<Lobby>> GetPublicLobbiesAsync()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al buscar lobbies: {e.Message}");
            return new List<Lobby>();
        }
    }

    // --- UNIRSE A SALA (CLIENTE) ---
    public async Task<bool> JoinLobbyAsync(Lobby lobby, string password = null)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions { Password = password };
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, options);
            _currentLobbyId = joinedLobby.Id;

            string joinCode = joinedLobby.Data["JOIN_CODE"].Value;

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();

            utp.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al unirse al lobby: {e.Message}");
            return false;
        }
    }

    // --- SALIR O CERRAR SALA ---
    public async void LeaveOrDestroyLobby()
    {
        if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);

        if (!string.IsNullOrEmpty(_currentLobbyId))
        {
            try
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(_currentLobbyId);
                }
                else
                {
                    string playerId = AuthenticationService.Instance.PlayerId;
                    await LobbyService.Instance.RemovePlayerAsync(_currentLobbyId, playerId);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Aviso al salir del lobby: {e.Message}");
            }
        }

        _currentLobbyId = null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        LobbyCameraManager.Instance?.ShowLobbyCamera();
        LobbyUIManager.Instance?.ReturnToMainPanel();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId || !NetworkManager.Singleton.IsConnectedClient)
        {
            LeaveOrDestroyLobby();
        }
    }

    private IEnumerator LobbyHeartbeatCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }
}