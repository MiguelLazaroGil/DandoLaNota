using UnityEngine;
using UnityEngine.Rendering;   // Volume (URP / HDRP)

/// <summary>
/// Efecto visual de posesión (Global Volume) con fundido de entrada / salida.
/// </summary>
public class PossessionVolumeEffect : MonoBehaviour, IGhostSystemPart
{
    [Header("Efecto al poseer (solo lo ve quien posee)")]
    [Tooltip("Global Volume con el efecto de posesión. Déjalo ACTIVO en la escena: el script le pone peso 0 al empezar, " +
             "lo sube a 1 al poseer y lo baja a 0 al salir.")]
    [SerializeField] private Volume possessionVolume;
    [Tooltip("Segundos que tarda el efecto en aparecer / desaparecer (0 = instantáneo)")]
    [SerializeField, Min(0f)] private float volumeFadeTime = 0.4f;

    private float volumeTargetWeight;          // 0 = efecto apagado, 1 = encendido

    private void Awake()
    {
        // El efecto de posesión empieza apagado
        if (possessionVolume != null) possessionVolume.weight = 0f;
    }

    // Un Volume solo afecta a lo que renderiza la máquina donde está, así que en red lo ve únicamente quien posee
    // (el director y los demás no). Y solo está encendido mientras posees, así que nunca se ve
    // desde la vista libre del fantasma.
    // GhostController pasa visible = ghost.IsLocallyControlled al poseer y false al salir.
    public void Show(bool visible)
    {
        volumeTargetWeight = visible ? 1f : 0f;
    }

    private void Update()
    {
        if (possessionVolume == null) return;

        float speed = volumeFadeTime > 0f ? 1f / volumeFadeTime : float.MaxValue;
        possessionVolume.weight = Mathf.MoveTowards(possessionVolume.weight, volumeTargetWeight, speed * Time.deltaTime);
    }
}