using UnityEngine;

// 1. Tocar a destiempo
[CreateAssetMenu(menuName = "Fantasma/Acciones/1. Tocar a Destiempo")]
public class ActionOffbeat : SabotageAction
{
    protected override void Execute(Musician target)
    {
        target.SetPlayingState(Musician.PlayingState.PlayingBadly);
        Debug.Log($"{target.name} está tocando a destiempo (sutil)");
    }
}