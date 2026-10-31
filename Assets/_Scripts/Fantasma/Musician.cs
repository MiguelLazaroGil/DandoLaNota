using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Musician : NetworkBehaviour
{
    public enum PlayingState { PlayingWell, PlayingBadly, Sabotaging }
    public PlayingState currentState = PlayingState.PlayingWell;
    public MusicianQuality quality;

    [Header("Configuración de Posesión")]
    public Transform cameraFirstPersonPoint;
    public Transform sheetMusicTransform;
    public AudioSource musicianAudio;
    public List<SabotageAction> availableActions = new List<SabotageAction>();

    [ReadOnly]
    public SabotageAction currentAction = null;

    public GhostEntity PossessingGhost { get; set; }

    // --- VARIABLES DE RED ---
    private readonly NetworkVariable<ulong> netPossessorClientId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    public bool IsAvailable => currentAction == null && (IsNetworkActive ? netPossessorClientId.Value == ulong.MaxValue : PossessingGhost == null);
    public bool IsPossessed => IsNetworkActive ? netPossessorClientId.Value != ulong.MaxValue : PossessingGhost != null;

    public Transform CameraPoint => cameraFirstPersonPoint != null ? cameraFirstPersonPoint : transform;
    public MusicianMovement Movement { get; private set; }

    private void Awake()
    {
        Movement = GetComponent<MusicianMovement>();
        if (quality == null) quality = GetComponent<MusicianQuality>();
    }

    public SabotageAction GetAction(int index)
    {
        if (index < 0 || index >= availableActions.Count) return null;
        return availableActions[index];
    }

    public void SetPlayingState(PlayingState newState) => currentState = newState;

    public void PlaySound(AudioClip clip)
    {
        if (musicianAudio && clip) musicianAudio.PlayOneShot(clip);
    }

    public void SwapToFakeInstrument(GameObject fakePrefab) { /* Lógica visual */ }
    public void ShowPhoneUI(bool show) { /* Lógica visual */ }

    // ========================================================================
    // POSESIÓN Y TRANSFERENCIA DE OWNERSHIP
    // ========================================================================

    /// <summary>
    /// Inicia la solicitud de posesión desde el fantasma local.
    /// </summary>
    public void RequestPossess(GhostEntity ghost)
    {
        if (IsNetworkActive)
        {
            RequestPossessServerRpc(NetworkManager.Singleton.LocalClientId, ghost);
        }
        else
        {
            // Lógica local Offline
            PossessingGhost = ghost;
            SetPlayingState(PlayingState.PlayingWell);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestPossessServerRpc(ulong requestingClientId, NetworkBehaviourReference ghostRef)
    {
        if (!IsAvailable) return;

        // 1. TRANSFERIR OWNERSHIP: Asignar la propiedad del NetworkObject al cliente que posee
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.ChangeOwnership(requestingClientId);
        }

        // 2. Registrar el ID del cliente en la NetworkVariable del Servidor
        netPossessorClientId.Value = requestingClientId;

        // 3. Notificar a los clientes para enlazar las referencias locales
        NotifyPossessClientRpc(requestingClientId, ghostRef);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyPossessClientRpc(ulong requestingClientId, NetworkBehaviourReference ghostRef)
    {
        if (ghostRef.TryGet(out GhostEntity ghost))
        {
            PossessingGhost = ghost;
            SetPlayingState(PlayingState.PlayingWell);

            // Si somos el cliente que ha tomado posesión, configuramos nuestro estado local
            if (IsNetworkActive && NetworkManager.Singleton.LocalClientId == requestingClientId)
            {
                ghost.SetPossessedMusicianFromNetwork(this);
            }
        }
    }

    // ========================================================================
    // SINCRONIZACIÓN DE SABOTAJES
    // ========================================================================

    public void StartSabotage(int actionIndex)
    {
        if (actionIndex < 0 || actionIndex >= availableActions.Count) return;

        if (IsNetworkActive)
        {
            StartSabotageServerRpc(actionIndex);
        }
        else
        {
            ApplySabotageLocal(actionIndex, true);
        }
    }

    public void StopSabotage(int actionIndex)
    {
        if (actionIndex < 0 || actionIndex >= availableActions.Count) return;

        if (IsNetworkActive)
        {
            StopSabotageServerRpc(actionIndex);
        }
        else
        {
            ApplySabotageLocal(actionIndex, false);
        }
    }

    [Rpc(SendTo.Server)]
    private void StartSabotageServerRpc(int actionIndex)
    {
        StartSabotageClientRpc(actionIndex);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartSabotageClientRpc(int actionIndex)
    {
        ApplySabotageLocal(actionIndex, true);
    }

    [Rpc(SendTo.Server)]
    private void StopSabotageServerRpc(int actionIndex)
    {
        StopSabotageClientRpc(actionIndex);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StopSabotageClientRpc(int actionIndex)
    {
        ApplySabotageLocal(actionIndex, false);
    }

    private void ApplySabotageLocal(int actionIndex, bool isStarting)
    {
        SabotageAction action = GetAction(actionIndex);
        if (action == null) return;

        if (isStarting)
        {
            action.ApplySabotagge(this);
            SetPlayingState(PlayingState.Sabotaging);
        }
        else
        {
            action.StopSabotagge(this);
            SetPlayingState(PlayingState.PlayingWell);
        }
    }

    // ========================================================================
    // GOLPE DEL DIRECTOR Y RESTAURACIÓN DE OWNERSHIP
    // ========================================================================

    public void OnHitByConductor()
    {
        if (IsNetworkActive)
        {
            OnHitByConductorServerRpc();
        }
        else
        {
            ProcessHitLocal();
        }
    }

    [Rpc(SendTo.Server)]
    private void OnHitByConductorServerRpc()
    {
        ProcessHitClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ProcessHitClientRpc()
    {
        ProcessHitLocal();
    }

    private void ProcessHitLocal()
    {
        if (IsAvailable)
        {
            Debug.Log("¡El Director golpeó a un músico inocente! Penalización para el director.");
            return;
        }

        if (PossessingGhost != null)
        {
            PossessingGhost.GetHitByConductor();
        }

        RestoreMusician();
    }

    public void RestoreMusician()
    {
        if (IsNetworkActive)
        {
            if (IsServer)
            {
                ProcessRestoreOnServer();
            }
            else
            {
                RequestRestoreServerRpc();
            }
        }
        else
        {
            ProcessRestoreLocal();
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestRestoreServerRpc()
    {
        ProcessRestoreOnServer();
    }

    private void ProcessRestoreOnServer()
    {
        // 1. Limpiar la variable de red
        netPossessorClientId.Value = ulong.MaxValue;

        // 2. DEVOLVER OWNERSHIP AL SERVIDOR: Quitar la propiedad al cliente
        if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId != NetworkManager.ServerClientId)
        {
            NetworkObject.RemoveOwnership();
        }

        // 3. Notificar a todos los clientes para limpiar su estado local
        NotifyRestoreClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyRestoreClientRpc()
    {
        ProcessRestoreLocal();
    }

    private void ProcessRestoreLocal()
    {
        if (currentAction != null)
        {
            currentAction.StopSabotagge(this);
        }

        currentAction = null;
        PossessingGhost = null;

        SetPlayingState(PlayingState.PlayingWell);
        EventosRecuperarse();
    }

    private void EventosRecuperarse()
    {
        Debug.Log("ya no estoy controlado, vuelvo a tocar normal: " + this.name);
    }
}