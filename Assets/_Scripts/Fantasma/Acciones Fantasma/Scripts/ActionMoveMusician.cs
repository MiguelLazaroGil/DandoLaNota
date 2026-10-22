using UnityEngine;

// 6. Levantarse y moverse con WASD (Solo Players, gasta mucha energía continua)
[CreateAssetMenu(menuName = "Fantasma/Acciones/6. Mover Musico (Solo Player)")]
public class ActionMoveMusician : SabotageAction
{
    public override void Execute(GhostEntity ghost, Musician target)
    {
        target.SetPlayingState(Musician.PlayingState.PlayingBadly);
        Debug.Log("te mueves jiji");
    }
}