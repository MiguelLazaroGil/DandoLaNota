using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GhostEntity : NetworkBehaviour
{
    [Header("Energía / Maná")]
    public float maxEnergy = 100f;
    public float currentEnergy = 50f;
    public float baseRegenRate = 5f;
    public float regenScalingPerMinute = 3f;
    public float possessInitialCost = 20f;

    [Header("Tiempos y Cooldowns")]
    public float exitCastTime = 1.5f;
    public float stunCooldownTime = 8f;

    public Musician CurrentMusician { get; private set; }
    public bool IsStunned { get; private set; }
    public bool IsExiting { get; private set; }

    public SabotageAction ActiveContinuousAction => activeContinuousAction;

    /// <summary>True si este fantasma es el de ESTA máquina (siempre true en offline).
    /// Sirve para que los efectos visuales solo los vea el jugador que posee.</summary>
    public bool IsLocallyControlled => !IsNetworkActive || IsOwner;

    public event System.Action<Musician> MusicianPossessed;
    public event System.Action<Musician> MusicianLeft;

    private SabotageAction activeContinuousAction;
    private float matchTime;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    private void Update()
    {
        // En multijugador, solo la máquina que controla a este fantasma ejecuta su Update local
        if (IsNetworkActive && !IsOwner) return;

        matchTime += Time.deltaTime;

        if (!IsStunned && activeContinuousAction == null)
        {
            float currentRegen = baseRegenRate + (regenScalingPerMinute * (matchTime / 60f));
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + currentRegen * Time.deltaTime);
        }

        if (activeContinuousAction != null && CurrentMusician != null)
        {
#pragma warning disable CS0618
            activeContinuousAction.OnUpdateAction(this, CurrentMusician);
#pragma warning restore CS0618
        }
    }

    public bool ConsumeEnergy(float amount)
    {
        if (currentEnergy < amount) return false;
        currentEnergy -= amount;
        return true;
    }

    public bool TryPossess(Musician target)
    {
        if (target == null || !target.IsAvailable) return false;
        if (IsStunned || CurrentMusician != null || target.IsPossessed) return false;
        if (!ConsumeEnergy(possessInitialCost)) return false;

        CurrentMusician = target;
        target.PossessingGhost = this;

        if (IsNetworkActive)
        {
            target.SetPossessorClientId(NetworkManager.Singleton.LocalClientId);
        }

        target.SetPlayingState(Musician.PlayingState.PlayingWell);

        MusicianPossessed?.Invoke(target);
        return true;
    }

    public bool ExecuteSabotage(SabotageAction action)
    {
        if (action == null || CurrentMusician == null || IsExiting) return false;

        int actionIndex = CurrentMusician.availableActions.IndexOf(action);
        if (actionIndex == -1) return false;

        if (!action.isContinuous)
        {
            if (!ConsumeEnergy(action.energyCost)) return false;

            CurrentMusician.StartSabotage(actionIndex);
            return true;
        }

        if (activeContinuousAction == action) return true;

        StopContinuousAction();
        activeContinuousAction = action;

        CurrentMusician.StartSabotage(actionIndex);
        return true;
    }

    public void StopContinuousAction()
    {
        if (activeContinuousAction == null || CurrentMusician == null) return;

        int actionIndex = CurrentMusician.availableActions.IndexOf(activeContinuousAction);
        if (actionIndex != -1)
        {
            CurrentMusician.StopSabotage(actionIndex);
        }

        activeContinuousAction = null;
    }

    public void RequestVoluntaryExit()
    {
        if (CurrentMusician != null && !IsExiting)
            StartCoroutine(VoluntaryExitRoutine());
    }

    private IEnumerator VoluntaryExitRoutine()
    {
        IsExiting = true;
        Debug.Log("Saliendo del músico... ¡No dejes que el director te golpee!");
        yield return new WaitForSeconds(exitCastTime);

        if (CurrentMusician != null)
        {
            CurrentMusician.RestoreMusician();
            LeaveMusician();
        }
        IsExiting = false;
    }

    public void GetHitByConductor()
    {
        StopAllCoroutines();
        IsExiting = false;
        LeaveMusician();
        StartCoroutine(StunCooldownRoutine());
    }

    private void LeaveMusician()
    {
        if (CurrentMusician == null) return;

        Musician oldMusician = CurrentMusician;
        CurrentMusician = null;
        activeContinuousAction = null;

        MusicianLeft?.Invoke(oldMusician);
    }

    private IEnumerator StunCooldownRoutine()
    {
        IsStunned = true;
        Debug.Log("El director te ha echado a golpes. En cooldown...");
        yield return new WaitForSeconds(stunCooldownTime);
        IsStunned = false;
    }
}