using System.Collections;
using UnityEngine;

public class GhostEntity : MonoBehaviour
{

    [Header("Energía / Maná")]
    public float maxEnergy = 100f;
    public float currentEnergy = 50f;
    public float baseRegenRate = 5f;
    public float regenScalingPerMinute = 3f; // Va en aumento para que el director acabe perdiendo
    public float possessInitialCost = 20f;

    [Header("Tiempos y Cooldowns")]
    public float exitCastTime = 1.5f;     // Tarda un tiempo en salir voluntariamente
    public float stunCooldownTime = 8f;   // Cooldown cuando el director te golpea

    public Musician CurrentMusician { get; private set; }
    public bool IsStunned { get; private set; }
    public bool IsExiting { get; private set; }

    private SabotageAction activeContinuousAction;
    private float matchTime;

    private void Update()
    {
        matchTime += Time.deltaTime;

        // Regenerar energía (aumenta con el tiempo de partida)
        if (!IsStunned && activeContinuousAction == null)
        {
            float currentRegen = baseRegenRate + (regenScalingPerMinute * (matchTime / 60f));
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + currentRegen * Time.deltaTime);
        }

        // Si hay una acción continua activa (ej. mover al músico)
        if (activeContinuousAction != null && CurrentMusician != null)
        {
            activeContinuousAction.OnUpdateAction(this, CurrentMusician);
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
        if (IsStunned || CurrentMusician != null || target.IsPossessed) return false;
        if (!ConsumeEnergy(possessInitialCost)) return false;

        CurrentMusician = target;
        target.PossessingGhost = this;
        target.SetPlayingState(Musician.PlayingState.PlayingWell); // Empieza fingiendo que toca bien

        SendMessage("OnPossessMusician", target, SendMessageOptions.DontRequireReceiver);
        return true;
    }

    public void ExecuteSabotage(SabotageAction action)
    {
        if (CurrentMusician == null || IsExiting) return;

        if (!action.isContinuous)
        {
            if (ConsumeEnergy(action.energyCost))
                action.Execute(this, CurrentMusician);
        }
        else
        {
            activeContinuousAction = action;
            action.Execute(this, CurrentMusician);
        }
    }

    // Salir anticipadamente (tarda un tiempo, si el director te da mientras sales, estás cooked)
    public void RequestVoluntaryExit()
    {
        if (CurrentMusician != null && !IsExiting)
            StartCoroutine(VoluntaryExitRoutine()); // perdón miguel
    }

    // perdon miguel esto me lo ha hecho el chati, yo no se corrutinas ni delayed actions :c
    private IEnumerator VoluntaryExitRoutine()
    {
        IsExiting = true;
        Debug.Log("Saliendo del músico... ¡No dejes que el director te golpee!");
        yield return new WaitForSeconds(exitCastTime);

        if (CurrentMusician != null) // Si no nos golpeó el director durante la espera
        {
            LeaveMusicianClean();
        }
        IsExiting = false;
    }

    // Cuando el director te pilla y te golpea
    public void GetHitByConductor()
    {
        StopAllCoroutines(); // Cancela tu huida si estabas intentando salir
        IsExiting = false;
        LeaveMusicianClean();
        StartCoroutine(StunCooldownRoutine());
    }

    private void LeaveMusicianClean()
    {
        if (activeContinuousAction != null)
        {
            activeContinuousAction.StopAction(this, CurrentMusician);
            activeContinuousAction = null;
        }

        Musician oldMusician = CurrentMusician;
        CurrentMusician.PossessingGhost = null;
        CurrentMusician = null;

        SendMessage("OnLeaveMusician", oldMusician, SendMessageOptions.DontRequireReceiver);
    }

    private IEnumerator StunCooldownRoutine()
    {
        IsStunned = true;
        Debug.Log("El director te ha echado a golpes. En cooldown...");
        yield return new WaitForSeconds(stunCooldownTime);
        IsStunned = false;
    }
}