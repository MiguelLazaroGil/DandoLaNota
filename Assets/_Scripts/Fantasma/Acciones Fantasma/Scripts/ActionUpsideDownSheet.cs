using UnityEngine;

// 2. Partitura al revés
[CreateAssetMenu(menuName = "Fantasma/Acciones/2. Partitura al Reves")]
public class ActionUpsideDownSheet : SabotageAction
{
    public override void Execute(Musician target)
    {
        if (target.sheetMusicTransform != null)
            target.sheetMusicTransform.localRotation = Quaternion.Euler(0, 0, 180f);

        target.SetPlayingState(Musician.PlayingState.PlayingBadly);
        Debug.Log($"¡Partitura al revés en {target.name}!");
    }
}