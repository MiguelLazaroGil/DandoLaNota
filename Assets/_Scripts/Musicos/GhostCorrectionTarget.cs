
using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Musician))]
public class GhostCorrectionTarget : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GhostOutlineVisual outline;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onCorregido;

    public bool TieneFantasma { get; private set; }

    public event Action<GhostCorrectionTarget> Corregido;

    private SabotageAction activeAction;
    private Musician musician;

    private void Awake()
    {
        musician = GetComponent<Musician>();

        if (outline == null)
            outline = GetComponent<GhostOutlineVisual>();

        SetVisual(false);
    }

    public void ActivarSabotaje(SabotageAction action)
    {
        activeAction = action;
        TieneFantasma = true;

        SetVisual(true);

        Debug.Log(
            $"[Fantasma] Sabotaje iniciado en {musician.name}"
        );
    }

    public void DesactivarSabotaje()
    {
        if (!TieneFantasma)
            return;

        if (musician == null)
            musician = GetComponent<Musician>();

        if (musician == null)
        {
            Debug.LogError(
                "[Fantasma] No se encontro el componente Musician."
            );
            return;
        }

        // Si la accion no se registro en Musician, la detenemos
        // utilizando la referencia guardada por el sistema fantasma.
        if (musician.currentAction == null && activeAction != null)
        {
            Debug.LogWarning(
                $"[Fantasma] {musician.name}: la accion no estaba " +
                "registrada en Musician.currentAction."
            );

            activeAction.StopSabotagge(musician);
        }

        // Restaura el estado real del musico.
        // En single player detiene currentAction mediante
        // StopSabotagge y limpia el estado de posesion.
        musician.RestoreMusician();

        // Una vez restaurado, quitamos la marca visual.
        activeAction = null;
        TieneFantasma = false;

        SetVisual(false);

        Debug.Log(
            $"[Fantasma] {musician.name} corregido. " +
            $"Disponible: {musician.IsAvailable}, " +
            $"Accion actual: {(musician.currentAction == null ? "NULL" : "ACTIVA")}"
        );

        onCorregido?.Invoke();
        Corregido?.Invoke(this);
    }

    private void SetVisual(bool active)
    {
        if (outline != null)
            outline.SetHighlighted(active);
    }
}
