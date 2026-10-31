using UnityEngine;

/// <summary>
/// Detecta a qué músico apunta el fantasma usando tu sistema de interacción (CompleteTaggedDetector)
/// y activa / desactiva ese detector al poseer.
/// </summary>
public class GhostTargeting : MonoBehaviour, IGhostSystemPart
{
    [Header("Detección de músicos")]
    [Tooltip("El InteractionDetector (CompleteTaggedDetector). Si lo dejas vacío se busca en los hijos de este objeto. " +
             "Su Filter debe aceptar a los músicos. El resaltado (HighlighterScript) y el texto (InteractTextScript) " +
             "funcionan solos porque escuchan a este detector.")]
    [SerializeField] private CompleteTaggedDetector interactionDetector;

    public Musician TargetedMusician { get; private set; }

    private Collider detectorCollider;         // collider trigger del InteractionDetector

    // Lo llama GhostController desde su Awake
    public void Initialize()
    {
        // Detector de interacción: el asignado o, si no, el que cuelgue de este objeto (el InteractionDetector)
        if (interactionDetector == null) interactionDetector = GetComponentInChildren<CompleteTaggedDetector>(true);
        if (interactionDetector != null)
            detectorCollider = interactionDetector.GetComponent<Collider>();
        else
            Debug.LogWarning("GhostTargeting: no hay ningún CompleteTaggedDetector (InteractionDetector), no podrás poseer.", this);
    }

    // Se llama cada frame en modo libre
    public void UpdateTarget()
    {
        TargetedMusician = FindTargetedMusician();
    }

    public void ClearTarget()
    {
        TargetedMusician = null;
    }

    // El objetivo lo decide tu CompleteTaggedDetector (el que resalta y muestra el texto); aquí solo lo traducimos a Musician.
    // El objetivo puede ser un collider hijo del músico, por eso se busca el Musician hacia arriba.
    private Musician FindTargetedMusician()
    {
        if (interactionDetector == null || !interactionDetector.HasTarget()) return null;

        Musician m = interactionDetector.GetTarget().GetComponentInParent<Musician>();
        return (m != null && m.IsAvailable && !m.IsPossessed) ? m : null;
    }

    // Mientras posees, el detector se apaga: no detecta nada y se quita el resaltado y el texto del último objetivo.
    // Como no tiene objetivo, la mano (Interactor) tampoco puede interactuar con nada.
    public void SetInteractionActive(bool active)
    {
        if (interactionDetector == null) return;

        if (!active)
        {
            interactionDetector.SetLocked(true);
            interactionDetector.NullTarget();                  // olvida el objetivo y lo que hubiera dentro del trigger
            interactionDetector.onTargetChanged.Invoke(null);  // el Highlighter y el InteractText se limpian solos
            if (detectorCollider != null) detectorCollider.enabled = false;
        }
        else
        {
            // Al reactivar el trigger, Unity vuelve a avisar de todo lo que ya tenga dentro
            if (detectorCollider != null) detectorCollider.enabled = true;
            interactionDetector.SetLocked(false);
        }
    }
}