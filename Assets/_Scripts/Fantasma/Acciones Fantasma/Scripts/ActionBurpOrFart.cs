using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 5. Eructo o Pedo aleatorio (toggle)
// Al activarla va soltando eructos/pedos al azar, uno tras otro, hasta que se elige otra vez en la rueda.
[CreateAssetMenu(menuName = "Fantasma/Acciones/5. Eructo o Pedo")]
public class ActionBurpOrFart : SabotageAction
{
    public AudioClip[] audioClips;

    [Tooltip("Segundos de pausa entre un sonido y el siguiente")]
    [SerializeField] private float secondsBetweenRepeats = 1f;

    // Es un toggle: siempre tiene que ser continua
    private void OnValidate()
    {
        isContinuous = true;
    }

    // El asset es compartido por todos los músicos, así que la corrutina de cada uno se guarda por separado
    private readonly Dictionary<Musician, Coroutine> routines = new Dictionary<Musician, Coroutine>();

    protected override void Execute(Musician target)
    {
        if (audioClips == null || audioClips.Length == 0)
        {
            Debug.LogWarning($"[{actionName}] No hay AudioClips asignados.", this);
            return;
        }

        CancelRoutine(target);
        routines[target] = target.StartCoroutine(RepeatRoutine(target));
    }

    protected override void Stop(Musician target)
    {
        CancelRoutine(target);
        target.StopSound();
    }

    // Suena un clip al azar, espera a que acabe (más la pausa) y vuelve a empezar, hasta que se cancele
    private IEnumerator RepeatRoutine(Musician target)
    {
        while (true)
        {
            AudioClip randomClip = audioClips[Random.Range(0, audioClips.Length)];
            target.PlaySound(randomClip);
            Debug.Log("uy perdon");

            float wait = (randomClip != null ? randomClip.length : 0f) + Mathf.Max(0.1f, secondsBetweenRepeats);
            yield return new WaitForSeconds(wait);
        }
    }

    private void CancelRoutine(Musician target)
    {
        if (routines.TryGetValue(target, out Coroutine routine))
        {
            if (routine != null) target.StopCoroutine(routine);
            routines.Remove(target);
        }
    }
}