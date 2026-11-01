using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona la "presencia" del fantasma: qu� scripts y objetos se apagan mientras posees
/// y la recolocaci�n del fantasma al salir del m�sico.
/// </summary>
public class GhostPresence : MonoBehaviour, IGhostSystemPart
{
    [Header("Jugador")]
    [Tooltip("Objeto ra�z del jugador/fantasma: es el que se recoloca al salir del m�sico y el que tiene el movimiento " +
             "libre, el CharacterController, etc. Si estos scripts est�n en un hijo del player, arrastra aqu� el player. " +
             "Vac�o = este mismo objeto.")]
    [SerializeField] private Transform ghostRoot;

    [Header("Mientras posees...")]
    [Tooltip("Desactiva autom�ticamente TODOS los dem�s scripts que haya en el Ghost Root (y en este objeto) " +
             "(tu movimiento libre, etc.) mientras est�s dentro de un m�sico, y los reactiva al salir. " +
             "No toca GhostEntity ni los scripts del sistema del fantasma (GhostController y sus piezas). " +
             "Si alg�n script debe seguir activo, ponlo en Keep Enabled.")]
    [SerializeField] private bool autoDisableOtherScripts = true;
    [Tooltip("Scripts que NO se desactivan con la opci�n autom�tica")]
    [SerializeField] private Behaviour[] keepEnabledWhilePossessing;
    [Tooltip("Scripts a desactivar mientras posees aunque est�n en otro objeto (ej. un script de movimiento " +
             "o de c�mara que est� en un hijo). NO pongas aqu� GhostEntity ni los scripts del sistema del fantasma.")]
    [SerializeField] private Behaviour[] disableWhilePossessing;
    [Tooltip("Objetos a ocultar mientras posees (ej. el modelo del fantasma). No pongas la c�mara.")]
    [SerializeField] private GameObject[] hideWhilePossessing;

    private GhostEntity ghost;

    // Scripts que hemos desactivado al poseer (para reactivar exactamente esos al salir)
    private readonly List<Behaviour> disabledByController = new List<Behaviour>();

    private void Awake()
    {
        ghost = GetComponent<GhostEntity>();
        if (ghostRoot == null) ghostRoot = transform;
    }

    // present = false -> al poseer (se apaga lo configurado); present = true -> al salir (se reactiva)
    public void SetPresent(bool present)
    {
        if (present)
        {
            // Reactivamos solo lo que desactivamos nosotros
            foreach (Behaviour b in disabledByController)
                if (b != null) b.enabled = true;
            disabledByController.Clear();
        }
        else
        {
            foreach (Behaviour b in disableWhilePossessing)
                DisableIfEnabled(b);

            if (autoDisableOtherScripts)
            {
                DisableOtherScriptsIn(ghostRoot.gameObject);
                if (ghostRoot.gameObject != gameObject) DisableOtherScriptsIn(gameObject);
            }
        }

        foreach (GameObject go in hideWhilePossessing)
            if (go != null) go.SetActive(present);
    }

    // El fantasma reaparece donde estaba el m�sico (puede haberse movido), para que la c�mara no pegue un salto
    public void MoveTo(Vector3 position)
    {
        // Un CharacterController activo pisa los cambios de transform.position
        CharacterController cc = ghostRoot.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        ghostRoot.position = position;
        if (cc != null) cc.enabled = true;
    }

    private void DisableOtherScriptsIn(GameObject target)
    {
        foreach (MonoBehaviour mb in target.GetComponents<MonoBehaviour>())
        {
            // Nunca apagamos GhostEntity ni las piezas del propio sistema del fantasma
            if (mb == ghost || mb is IGhostSystemPart) continue;
            if (keepEnabledWhilePossessing != null
                && System.Array.IndexOf(keepEnabledWhilePossessing, mb) >= 0) continue;
            DisableIfEnabled(mb);
        }
    }

    private void DisableIfEnabled(Behaviour b)
    {
        if (b == null || !b.enabled) return;

        b.enabled = false;
        disabledByController.Add(b);
    }
}