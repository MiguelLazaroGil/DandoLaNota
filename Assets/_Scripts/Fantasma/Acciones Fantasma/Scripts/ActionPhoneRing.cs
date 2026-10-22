using UnityEngine;

// 4. Suena el móvil (John Pork is calling)
[CreateAssetMenu(menuName = "Fantasma/Acciones/4. John Pork Calling")]
public class ActionPhoneRing : SabotageAction
{
    public AudioClip audioLlamada;

    public override void Execute(GhostEntity ghost, Musician target)
    {
        target.PlaySound(audioLlamada);
        target.ShowPhoneUI(true);
        Debug.Log("yo fone lingin");
    }
}