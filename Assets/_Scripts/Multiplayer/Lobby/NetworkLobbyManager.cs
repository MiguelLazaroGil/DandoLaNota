using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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

    // Convierte cualquier contraseña a un Hash SHA256 de 64 caracteres para superar la restricción de UGS
    public static string HashPassword(string rawPassword)
    {
        if (string.IsNullOrEmpty(rawPassword)) return null;

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawPassword));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }

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

            string hashedPassword = HashPassword(password);
            bool hasPassword = !string.IsNullOrEmpty(hashedPassword);

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false, // Se mantiene descubrible para permitir unión por nombre desde la pestaña privada
                Password = hasPassword ? hashedPassword : null,
                Data = new Dictionary<string, DataObject>
                {
                    { "JOIN_CODE", new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
                    { "HAS_PASSWORD", new DataObject(DataObject.VisibilityOptions.Public, hasPassword ? "true" : "false") }
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

    public async Task<bool> JoinLobbyAsync(Lobby lobby, string password = null)
    {
        try
        {
            string hashedPassword = HashPassword(password);
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions { Password = hashedPassword };

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

    public async void LeaveOrDestroyLobby()
    {
        if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);

        if (!string.IsNullOrEmpty(_currentLobbyId))
        {
            try
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
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

        // Destruir el estado del lobby para evitar que persista como zombi al volver al menú
        if (LobbyPlayerState.Instance != null)
        {
            Destroy(LobbyPlayerState.Instance.gameObject);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        LobbyCameraManager.Instance?.ShowLobbyCamera();
        LobbyUIManager.Instance?.ReturnToMainPanel();
    }
    public async Task MarkLobbyAsInGameAsync()
    {
        Debug.Log("Marking as in game");
        if (string.IsNullOrEmpty(_currentLobbyId) || !NetworkManager.Singleton.IsServer) return;

        try
        {
            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
            {
                { "IS_IN_GAME", new DataObject(DataObject.VisibilityOptions.Public, "true") }
            }
            };

            await LobbyService.Instance.UpdateLobbyAsync(_currentLobbyId, options);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Aviso al actualizar estado de lobby a En Partida: {e.Message}");
        }
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