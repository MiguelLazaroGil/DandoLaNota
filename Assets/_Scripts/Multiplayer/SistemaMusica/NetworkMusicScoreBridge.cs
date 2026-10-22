using Unity.Netcode;
using UnityEngine;

public class NetworkMusicScoreBridge : NetworkBehaviour
{
    [SerializeField] private MusicScoreManager localScoreManager;

    // Variables de red sincronizadas automáticamente del Servidor a todos los Clientes
    public NetworkVariable<float> NetRhythm = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> NetQuality = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        NetRhythm.OnValueChanged += (prev, current) => ApplyToLocalScore();
        NetQuality.OnValueChanged += (prev, current) => ApplyToLocalScore();
    }

    private void Update()
    {
        if (IsServer && localScoreManager != null)
        {
            // El servidor actualiza las variables de red con el estado actual del juego
            NetRhythm.Value = localScoreManager.CurrentRhythm;
            NetQuality.Value = localScoreManager.CurrentQuality;
        }
    }

    // El Director (Cliente) envía el resultado de su trayectoria al Servidor
    [Rpc(SendTo.Server)]
    public void SubmitDirectorInputRpc(float rhythm, float quality)
    {
        if (localScoreManager != null)
        {
            localScoreManager.ReceiveDirectorInput(rhythm, quality);
        }
    }

    private void ApplyToLocalScore()
    {
        // En los clientes que no son el servidor, forzamos la lectura del estado sincronizado
        if (!IsServer && localScoreManager != null)
        {
            // Se puede usar un método de reflexión o añadir un Setter simple en MusicScoreManager
            localScoreManager.ReceiveDirectorInput(NetRhythm.Value, NetQuality.Value);
        }
    }
}