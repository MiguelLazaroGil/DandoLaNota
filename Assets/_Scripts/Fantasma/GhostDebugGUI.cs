using UnityEngine;

/// <summary>
/// Panel de debug temporal (sustitúyelo por tu UI). Para ocultarlo, desmarca o quita este componente.
/// </summary>
public class GhostDebugGUI : MonoBehaviour, IGhostSystemPart
{
    private GhostEntity ghost;
    private GhostTargeting targeting;

    private void Awake()
    {
        ghost = GetComponent<GhostEntity>();
        targeting = GetComponent<GhostTargeting>();
    }

    private void OnGUI()
    {
        if (ghost == null) return;

        Musician targeted = targeting != null ? targeting.TargetedMusician : null;

        GUILayout.BeginArea(new Rect(10, 10, 420, 220));
        GUILayout.Label($"Energía: {ghost.currentEnergy:0} / {ghost.maxEnergy:0}");

        if (ghost.IsStunned)
        {
            GUILayout.Label("ATURDIDO (cooldown)");
        }
        else if (ghost.CurrentMusician == null)
        {
            if (targeted != null)
                GUILayout.Label($"[E] Poseer a {targeted.name} (coste {ghost.possessInitialCost:0})");
        }
        else
        {
            GUILayout.Label(ghost.IsExiting ? "Saliendo..." : "[E] Salir del músico   |   [Tab] mantener: rueda de acciones");

            if (ghost.ActiveContinuousAction != null)
                GUILayout.Label($"Acción activa: {ghost.ActiveContinuousAction.actionName} (elígela otra vez para pararla)");

            MusicianMovement movement = ghost.CurrentMusician.Movement;
            if (movement != null && movement.IsControlled)
                GUILayout.Label("WASD: mover   |   Ratón: girar");
        }

        GUILayout.EndArea();
    }
}