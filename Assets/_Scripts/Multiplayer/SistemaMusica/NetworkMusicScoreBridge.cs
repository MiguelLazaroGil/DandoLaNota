using Unity.Netcode;
using UnityEngine;

public class NetworkMusicScoreBridge : NetworkBehaviour
{
    public static NetworkMusicScoreBridge Instance { get; private set; }

    [Header("Referencias a Gestores Locales")]
    [SerializeField] private MusicScoreManager scoreManager;
    [SerializeField] private AudienceManager audienceManager;

    // --- VARIABLES DE RED (Sincronizadas desde el Servidor hacia todos los clientes) ---
    private readonly NetworkVariable<float> netRhythm = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> netQuality = new NetworkVariable<float>(
        1.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> netDirectorQuality = new NetworkVariable<float>(
        1.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> netAudience = new NetworkVariable<float>(
        50f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> netPseudoScore = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (scoreManager == null) scoreManager = FindFirstObjectByType<MusicScoreManager>();
        if (audienceManager == null) audienceManager = FindFirstObjectByType<AudienceManager>();
    }

    // ========================================================================
    // 1. RECEPCIÓN DE INPUT DEL DIRECTOR (Host o Cliente)
    // ========================================================================

    /// <summary>
    /// Invocado por RhythmTracker desde cualquier cliente/host para enviar la batuta al Servidor.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void SubmitDirectorInputRpc(float rhythm, float quality)
    {
        if (scoreManager != null)
        {
            scoreManager.ReceiveDirectorInput(rhythm, quality);
        }
    }

    // ========================================================================
    // 2. SINCRONIZACIÓN DE ESTADO (Servidor -> Clientes)
    // ========================================================================

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        // Solo el Servidor actualiza las NetworkVariables con el resultado de su simulación
        if (IsServer)
        {
            if (scoreManager != null)
            {
                netRhythm.Value = scoreManager.CurrentRhythm;
                netQuality.Value = scoreManager.CurrentQuality;
                netDirectorQuality.Value = scoreManager.CurrentDirectorQuality;
            }

            if (audienceManager != null)
            {
                netAudience.Value = audienceManager.CurrentAudience;
                netPseudoScore.Value = audienceManager.PseudoScore;
            }
        }
    }

    private void LateUpdate()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        // Los clientes remotos sobrescriben sus gestores locales con los valores sincronizados por red
        if (!IsServer)
        {
            if (scoreManager != null)
            {
                scoreManager.ApplyNetworkSync(netRhythm.Value, netQuality.Value, netDirectorQuality.Value);
            }

            if (audienceManager != null)
            {
                audienceManager.ApplyNetworkSync(netAudience.Value, netPseudoScore.Value);
            }
        }
    }
}