using UnityEngine;

// Mover al músico: mientras está activa, el jugador camina con WASD y gira con el ratón
// con el cuerpo del músico. Se apaga eligiéndola otra vez en la rueda o al salir del músico.
// El input lo gestiona GhostController; esta acción solo "desbloquea" el cuerpo.
[CreateAssetMenu(menuName = "Fantasma/Acciones/Mover Músico")]
public class ActionMoveMusician : SabotageAction
{
    // Esta acción SIEMPRE tiene que ser continua (si no, nadie la pararía) y solo la puede usar el jugador
    private void OnValidate()
    {
        isContinuous = true;
        playerOnly = true;
    }

    public override void Execute(Musician target)
    {
        if (target.Movement == null)
        {
            Debug.LogWarning($"{target.name} no tiene el componente MusicianMovement, no se puede mover.");
            return;
        }

        target.Movement.SetControlled(true);
        ApplyNegativeEffect(target); // Si se levanta de la silla, toca peor
    }

    public override void Stop(Musician target)
    {
        if (target.Movement == null) return;

        target.Movement.SetControlled(false);
        StopNegativeEffect(target);
    }
}
