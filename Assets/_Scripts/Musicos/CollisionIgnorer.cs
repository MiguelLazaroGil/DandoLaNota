using System.Collections.Generic;
using UnityEngine;

public class CollisionIgnorer : MonoBehaviour
{
    [Header("Listas de Colisionadores")]
    [Tooltip("Primera lista de colisionadores (ej: los del personaje)")]
    [SerializeField] private List<Collider> listA = new List<Collider>();

    [Tooltip("Segunda lista de colisionadores (ej: los del objeto agarrado)")]
    [SerializeField] private List<Collider> listB = new List<Collider>();

    [Header("Configuración")]
    [Tooltip("¿Ignorar las colisiones automáticamente al despertar el objeto?")]
    [SerializeField] private bool ignoreOnAwake = false;

    private bool isCurrentlyIgnored = false;

    private void Awake()
    {

        if (ignoreOnAwake)
        {
            DeactivateA();
            EnableIgnore();
            ActivateA();
        }
    }
    private void DeactivateA()
    {
        foreach (var colA in listA)
        {
            colA.enabled = false;
        }
    }
    private void ActivateA()
    {
        foreach (var colA in listA)
        {
            colA.enabled = true;
        }
    }
    /// <summary>
    /// Activa la ignorancia de colisiones entre todos los elementos de la Lista A y la Lista B.
    /// </summary>
    public void EnableIgnore()
    {
        SetCollisionState(true);
    }

    /// <summary>
    /// Desactiva la ignorancia de colisiones (las colisiones vuelven a su estado físico normal).
    /// </summary>
    public void DisableIgnore()
    {
        SetCollisionState(false);
    }

    private void SetCollisionState(bool ignore)
    {
        if (listA == null || listB == null) return;

        foreach (var colA in listA)
        {
            if (colA == null) continue;

            foreach (var colB in listB)
            {
                if (colB == null) continue;
                if (colA == colB) continue; // Evitar auto-comparaciones si se solapan las listas

                Physics.IgnoreCollision(colA, colB, ignore);
            }
        }

        isCurrentlyIgnored = ignore;
    }

    /// <summary>
    /// Permite actualizar las listas de forma dinámica en tiempo de ejecución (útil al cambiar de objeto agarrado).
    /// </summary>
    public void UpdateListsAndIgnore(List<Collider> newListA, List<Collider> newListB)
    {
        // 1. Si ya había una regla activa, limpiamos primero el estado anterior
        if (isCurrentlyIgnored)
        {
            DisableIgnore();
        }

        // 2. Asignamos las nuevas listas
        listA = newListA ?? new List<Collider>();
        listB = newListB ?? new List<Collider>();

        // 3. Aplicamos el nuevo estado de ignorar colisiones
        EnableIgnore();
    }
}