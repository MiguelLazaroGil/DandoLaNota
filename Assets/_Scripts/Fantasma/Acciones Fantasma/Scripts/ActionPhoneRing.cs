using UnityEngine;

// 4. Suena el móvil (John Pork is calling) (toggle)
// Al activarla el móvil suena en bucle y sale la UI; al elegirla otra vez se corta el sonido y se esconde la UI.
[CreateAssetMenu(menuName = "Fantasma/Acciones/4. John Pork Calling")]
public class ActionPhoneRing : SabotageAction
{
    public AudioClip audioLlamada;

    // Es un toggle: siempre tiene que ser continua
    private void OnValidate()
    {
        isContinuous = true;
    }

    protected override void Execute(Musician target)
    {
        target.PlaySound(audioLlamada, true); // true = se repite hasta que se apague
        target.ShowPhoneUI(true);
        Debug.Log("yo fone lingin");
    }

    protected override void Stop(Musician target)
    {
        target.StopSound();
        target.ShowPhoneUI(false);
    }
}