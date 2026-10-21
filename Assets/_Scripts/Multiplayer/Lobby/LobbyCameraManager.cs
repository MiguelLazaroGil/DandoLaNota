using UnityEngine;

public class LobbyCameraManager : MonoBehaviour
{
    public static LobbyCameraManager Instance { get; private set; }

    [SerializeField] private Camera lobbyCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ShowLobbyCamera();
    }

    public void ShowLobbyCamera()
    {
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);
    }

    public void HideLobbyCamera()
    {
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(false);
    }
}