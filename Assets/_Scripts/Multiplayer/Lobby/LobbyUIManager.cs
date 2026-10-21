using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Paneles")]
    [SerializeField] private GameObject mainLobbyPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("Campos de Entrada")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_InputField createLobbyNameInput;
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private Toggle isPrivateToggle;

    [Header("Contenedores de Lista UI")]
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyEntryPrefab;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerEntryPrefab;

    [Header("Botones")]
    [SerializeField] private Button startGameButton;

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

    public void ReturnToMainPanel()
    {
        mainLobbyPanel.SetActive(true);
        roomPanel.SetActive(false);
    }

    public async void OnClickCreateLobby()
    {
        string currentName = GetCurrentPlayerName();
        PlayerPrefs.SetString(PlayerNameKey, currentName);

        string lobbyName = string.IsNullOrEmpty(createLobbyNameInput.text) ? "Sala de " + currentName : createLobbyNameInput.text;
        string password = createPasswordInput.text;
        bool isPrivate = isPrivateToggle.isOn;

        bool success = await NetworkLobbyManager.Instance.CreateLobbyAndHostAsync(lobbyName, 4, isPrivate, password);
        if (success)
        {
            mainLobbyPanel.SetActive(false);
            roomPanel.SetActive(true);
            startGameButton.gameObject.SetActive(true);
        }
    }

    public async void OnClickRefreshLobbies()
    {
        List<Lobby> lobbies = await NetworkLobbyManager.Instance.GetPublicLobbiesAsync();

        foreach (Transform child in lobbyListContainer) Destroy(child.gameObject);

        foreach (Lobby lobby in lobbies)
        {
            GameObject entry = Instantiate(lobbyEntryPrefab, lobbyListContainer);
            entry.GetComponentInChildren<TMP_Text>().text = $"{lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers})";

            entry.GetComponent<Button>().onClick.AddListener(async () =>
            {
                PlayerPrefs.SetString(PlayerNameKey, GetCurrentPlayerName());

                bool joined = await NetworkLobbyManager.Instance.JoinLobbyAsync(lobby);
                if (joined)
                {
                    mainLobbyPanel.SetActive(false);
                    roomPanel.SetActive(true);
                    startGameButton.gameObject.SetActive(false);
                }
            });
        }
    }

    public void OnClickToggleRole()
    {
        LobbyPlayerState.Instance.RequestToggleRoleRpc();
    }

    public void OnClickLeaveLobby()
    {
        NetworkLobbyManager.Instance.LeaveOrDestroyLobby();
    }

    private void OnPlayerListChanged(NetworkListEvent<PlayerLobbyData> changeEvent)
    {
        foreach (Transform child in playerListContainer) Destroy(child.gameObject);

        foreach (PlayerLobbyData player in LobbyPlayerState.Instance.ConnectedPlayers)
        {
            GameObject item = Instantiate(playerEntryPrefab, playerListContainer);
            string pingDisplay = player.ClientId == NetworkManager.Singleton.LocalClientId ? "Host" : $"{player.PingMs} ms";
            string roleDisplay = player.Role == PlayerRole.Director ? " DIRECTOR" : " FANTASMA";

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

    public void OnClickStartGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            LobbyCameraManager.Instance?.HideLobbyCamera();
            LobbyPlayerState.Instance.ResolveRolesBeforeStart();
            NetworkManager.Singleton.SceneManager.LoadScene("GameLevelScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}