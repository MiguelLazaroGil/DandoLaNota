using UnityEngine;

// 4. Suena el móvil (John Pork is calling)
[CreateAssetMenu(menuName = "Fantasma/Acciones/4. John Pork Calling")]
public class ActionPhoneRing : SabotageAction
{
    public AudioClip audioLlamada;

    protected override void Execute(Musician target)
    {
        target.PlaySound(audioLlamada);
        target.ShowPhoneUI(true);
        Debug.Log("yo fone lingin");
    }
}