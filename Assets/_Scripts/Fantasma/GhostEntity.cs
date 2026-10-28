using System.Collections;
using Unity.VisualScripting.Antlr3.Runtime;
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

    // Acción continua que se está ejecutando ahora mismo (null si no hay ninguna)
    public SabotageAction ActiveContinuousAction => activeContinuousAction;

    // Eventos para que otros scripts (GhostController, UI, audio...) reaccionen
    // sin necesidad de SendMessage.
    public event System.Action<Musician> MusicianPossessed;
    public event System.Action<Musician> MusicianLeft;

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

        // Compatibilidad: OnUpdateAction está marcado como obsoleto, pero si alguna acción
        // antigua lo sigue usando, se llama igual. Si ya no lo usa ninguna, puedes borrar este bloque.
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
        target.SetPlayingState(Musician.PlayingState.PlayingWell); // Empieza fingiendo que toca bien

        MusicianPossessed?.Invoke(target);
        return true;
    }

    // Devuelve true si la acción se ha ejecutado / iniciado
    public bool ExecuteSabotage(SabotageAction action)
    {
        if (action == null || CurrentMusician == null || IsExiting) return false;

        if (!action.isContinuous)
        {
            if (!ConsumeEnergy(action.energyCost)) return false;
            CurrentMusician.currentAction = action;
            action.Execute(CurrentMusician);
            return true;
        }

        if (activeContinuousAction == action) return true; // Ya estaba activa

        StopContinuousAction(); // Solo puede haber una acción continua a la vez
        activeContinuousAction = action;
        CurrentMusician.currentAction=action;
        action.Execute(CurrentMusician);
        return true;
    }

    // Para la acción continua activa (el controller lo llama al volver a elegirla en la rueda,
    // y se llama solo al salir del músico)
    public void StopContinuousAction()
    {
        if (activeContinuousAction == null) return;

        SabotageAction action = activeContinuousAction;
        activeContinuousAction = null;

        action.Stop(CurrentMusician);
#pragma warning disable CS0618 // Compatibilidad con acciones antiguas que aún sobrescriban StopAction
        action.StopAction(this, CurrentMusician);
#pragma warning restore CS0618
        CurrentMusician.currentAction = null;
    }

    // Salir anticipadamente (tarda un tiempo, si el director te da mientras sales, estás cooked)
    public void RequestVoluntaryExit()
    {
        if (CurrentMusician != null && !IsExiting)
            StartCoroutine(VoluntaryExitRoutine()); // perdón miguel
    }

    // perdon miguel esto me lo ha hecho el chati, yo no se corrutinas ni delayed actions :c
    // (Una corrutina es una función que se puede "pausar": el yield return de abajo
    //  espera exitCastTime segundos y luego continúa por donde iba.)
    private IEnumerator VoluntaryExitRoutine()
    {
        IsExiting = true;
        Debug.Log("Saliendo del músico... ¡No dejes que el director te golpee!");
        yield return new WaitForSeconds(exitCastTime);

        if (CurrentMusician != null) // Si no nos golpeó el director durante la espera  // MIG: esto sobra poque Paras todas las corrutinas cuando te golpean, asi que esto no se ejecuta
        {
            CurrentMusician.RestoreMusician(); //esto es lo que hará que el musico recupere su buena musica etc
            LeaveMusician(); //esto no afectará al musico
        }
        IsExiting = false;
    }

    // Cuando el director te pilla y te golpea
    public void GetHitByConductor()
    {
        StopAllCoroutines(); // Cancela tu huida si estabas intentando salir
        IsExiting = false;
        LeaveMusician(); 
        StartCoroutine(StunCooldownRoutine());
    }

    private void LeaveMusician() //ya no accede al musico, todo lo que hacía ahora lo hace el musico en si mismo
    {
        if (CurrentMusician == null) return;

        Musician oldMusician = CurrentMusician;
        CurrentMusician = null;
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