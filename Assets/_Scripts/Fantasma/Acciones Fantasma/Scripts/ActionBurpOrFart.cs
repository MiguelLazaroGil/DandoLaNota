using UnityEngine;

// 5. Eructo o Pedo aleatorio
[CreateAssetMenu(menuName = "Fantasma/Acciones/5. Eructo o Pedo")]
public class ActionBurpOrFart : SabotageAction
{
    public AudioClip[] audioClips;

    protected override void Execute(Musician target)
    {
        if (audioClips.Length == 0) return;
        AudioClip randomClip = audioClips[Random.Range(0, audioClips.Length)];
        target.PlaySound(randomClip);
        Debug.Log("uy perdon");
    }
}
