using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Paneles Principales")]
    [SerializeField] private GameObject preLobbyWhole;
    [SerializeField] private GameObject publicLobbyPanel;
    [SerializeField] private GameObject privateLobbyPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("Nombre de Jugador (Global)")]
    [SerializeField] private TMP_InputField nameInputField;

    [Header("Panel Público")]
    [SerializeField] private TMP_InputField publicCreateLobbyNameInput;
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyEntryPrefab;

    [Header("Panel Privado")]
    [SerializeField] private TMP_InputField privateLobbyNameInput;
    [SerializeField] private TMP_InputField privatePasswordInput;

    [Header("Panel de Sala (En Lobby)")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private LoadSceneMultiplayer sceneLoader;

    private static string _sessionKey;
    private string PlayerNameKey
    {
        get
        {
            if (string.IsNullOrEmpty(_sessionKey))
            {
                int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                _sessionKey = $"PlayerName_{processId}";
            }
            return _sessionKey;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        string defaultName = "Jugador_" + Random.Range(100, 999);
        string savedName = PlayerPrefs.GetString(PlayerNameKey, defaultName);
        nameInputField.text = savedName;

        nameInputField.onValueChanged.AddListener(OnPlayerNameInputChanged);

        ReturnToMainPanel();

        if (LobbyPlayerState.Instance != null)
        {
            LobbyPlayerState.Instance.ConnectedPlayers.OnListChanged += OnPlayerListChanged;
        }
    }

    private void OnDestroy()
    {
        if (LobbyPlayerState.Instance != null)
        {
            LobbyPlayerState.Instance.ConnectedPlayers.OnListChanged -= OnPlayerListChanged;
        }
    }

    public string GetCurrentPlayerName()
    {
        if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
        {
            return nameInputField.text;
        }
        return "Jugador_" + Random.Range(100, 999);
    }

    private void OnPlayerNameInputChanged(string newName)
    {
        PlayerPrefs.SetString(PlayerNameKey, newName);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && LobbyPlayerState.Instance != null)
        {
            LobbyPlayerState.Instance.SetPlayerNameRpc(newName);
        }
    }

    public void ShowPublicTab()
    {
        preLobbyWhole.SetActive(true);
        publicLobbyPanel.SetActive(true);
        privateLobbyPanel.SetActive(false);
        roomPanel.SetActive(false);
        OnClickRefreshLobbies();
    }

    public void ShowPrivateTab()
    {
        preLobbyWhole.SetActive(true);
        publicLobbyPanel.SetActive(false);
        privateLobbyPanel.SetActive(true);
        roomPanel.SetActive(false);
    }

    public void ReturnToMainPanel()
    {
        roomPanel.SetActive(false);
        ShowPublicTab();
    }

    private void EnterRoomPanel(bool isHost)
    {
        preLobbyWhole.SetActive(false);
        publicLobbyPanel.SetActive(false);
        privateLobbyPanel.SetActive(false);
        roomPanel.SetActive(true);
        startGameButton.gameObject.SetActive(isHost);
    }

    public async void OnClickCreatePublicLobby()
    {
        string currentName = GetCurrentPlayerName();
        PlayerPrefs.SetString(PlayerNameKey, currentName);

        string lobbyName = string.IsNullOrEmpty(publicCreateLobbyNameInput.text)
            ? "Sala de " + currentName
            : publicCreateLobbyNameInput.text;

        bool success = await NetworkLobbyManager.Instance.CreateLobbyAndHostAsync(
            lobbyName: lobbyName,
            maxPlayers: 8,
            isPrivate: false,
            password: null
        );

        if (success)
        {
            EnterRoomPanel(isHost: true);
        }
    }

    public async void OnClickRefreshLobbies()
    {
        List<Lobby> lobbies = await NetworkLobbyManager.Instance.GetPublicLobbiesAsync();

        foreach (Transform child in lobbyListContainer) Destroy(child.gameObject);

        foreach (Lobby lobby in lobbies)
        {
            if (lobby.Data != null && lobby.Data.TryGetValue("HAS_PASSWORD", out var passwordData) && passwordData.Value == "true")
            {
                continue;
            }

            bool isInGame = lobby.Data != null && lobby.Data.TryGetValue("IS_IN_GAME", out var inGameData) && inGameData.Value == "true";
            string statusTag = isInGame ? " [EN PARTIDA]" : "";

            GameObject entry = Instantiate(lobbyEntryPrefab, lobbyListContainer);
            entry.GetComponentInChildren<TMP_Text>().text = $"{lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers}){statusTag}";

            entry.GetComponent<Button>().onClick.AddListener(async () =>
            {
                PlayerPrefs.SetString(PlayerNameKey, GetCurrentPlayerName());

                bool joined = await NetworkLobbyManager.Instance.JoinLobbyAsync(lobby);
                if (joined)
                {
                    EnterRoomPanel(isHost: false);
                }
            });
        }
    }

    public async void OnClickJoinOrCreatePrivateLobby()
    {
        string currentName = GetCurrentPlayerName();
        PlayerPrefs.SetString(PlayerNameKey, currentName);

        string lobbyName = privateLobbyNameInput.text.Trim();
        string password = privatePasswordInput.text.Trim();

        if (string.IsNullOrEmpty(lobbyName))
        {
            Debug.LogWarning("Debes escribir un nombre para el lobby.");
            return;
        }

        // 1. Buscar si la sala ya existe entre las salas activas
        List<Lobby> availableLobbies = await NetworkLobbyManager.Instance.GetPublicLobbiesAsync();
        Lobby targetLobby = availableLobbies.Find(l => l.Name.Equals(lobbyName, System.StringComparison.OrdinalIgnoreCase));

        if (targetLobby != null)
        {
            bool joined = await NetworkLobbyManager.Instance.JoinLobbyAsync(targetLobby, password);
            if (joined)
            {
                EnterRoomPanel(isHost: false);
                return;
            }
        }

        // 2. Si no existe, crearla
        bool created = await NetworkLobbyManager.Instance.CreateLobbyAndHostAsync(
            lobbyName: lobbyName,
            maxPlayers: 4,
            isPrivate: false,
            password: password
        );

        if (created)
        {
            EnterRoomPanel(isHost: true);
        }
    }

    public void OnClickToggleRole()
    {
        if (LobbyPlayerState.Instance != null && LobbyPlayerState.Instance.IsSpawned)
        {
            LobbyPlayerState.Instance.RequestToggleRoleRpc();
        }
    }

    public void OnClickLeaveLobby()
    {
        NetworkLobbyManager.Instance.LeaveOrDestroyLobby();
    }

    public void OnClickStartGame()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            LobbyCameraManager.Instance?.HideLobbyCamera();
            LobbyPlayerState.Instance.ResolveRolesBeforeStart();
            Debug.Log("Intentando hacer lo de marcar como in game");
            _=  NetworkLobbyManager.Instance.MarkLobbyAsInGameAsync();
            Debug.Log("Se supone uqe mandado lo de ingame");
            sceneLoader.LoadScene();
        }
    }

    private void OnPlayerListChanged(NetworkListEvent<PlayerLobbyData> changeEvent)
    {
        foreach (Transform child in playerListContainer) Destroy(child.gameObject);

        foreach (PlayerLobbyData player in LobbyPlayerState.Instance.ConnectedPlayers)
        {
            GameObject item = Instantiate(playerEntryPrefab, playerListContainer);
            string pingDisplay = player.ClientId == NetworkManager.Singleton.LocalClientId ? "Host" : $"{player.PingMs} ms";
            string roleDisplay = player.Role == PlayerRole.Director ? "DIRECTOR" : "FANTASMA";

            item.GetComponentInChildren<TMP_Text>().text = $"{player.PlayerName} [{roleDisplay}] | Ping: {pingDisplay}";

            Button forceDirectorBtn = item.GetComponentInChildren<Button>();
            if (forceDirectorBtn != null)
            {
                forceDirectorBtn.gameObject.SetActive(NetworkManager.Singleton.IsServer);
                ulong targetId = player.ClientId;
                forceDirectorBtn.onClick.AddListener(() =>
                {
                    LobbyPlayerState.Instance.ForceSetDirectorRpc(targetId);
                });
            }
        }
    }
}