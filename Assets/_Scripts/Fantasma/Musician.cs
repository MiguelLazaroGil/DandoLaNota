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

    [System.NonSerialized]
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
    // SINCRONIZACIÓN DE SABOTAJES (ONLINE Y OFFLINE)
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
    // GOLPE DEL DIRECTOR Y RECUPERACIÓN
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
        ProcessHitLocalClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ProcessHitLocalClientRpc()
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
        if (IsNetworkActive && IsServer)
        {
            netPossessorClientId.Value = ulong.MaxValue;
        }

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

    public void SetPossessorClientId(ulong clientId)
    {
        if (IsNetworkActive && IsServer)
        {
            netPossessorClientId.Value = clientId;
        }
    }
}