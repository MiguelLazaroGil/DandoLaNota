using System.Collections.Generic;
using UnityEngine;

public class MusicoController : MonoBehaviour
{
    [Header("Estado del Enemigo")]
    [Tooltip("Permite deshabilitar a este enemigo temporalmente (stunteado, ejecutando animación, etc.).")]
    [SerializeField] private bool isAvailable = true;

    [Header("Acciones Disponibles")]
    [SerializeField] private List<MusicoAccion> actions = new List<MusicoAccion>();

    public bool IsAvailable => isAvailable && gameObject.activeInHierarchy;
    public List<MusicoAccion> Actions => actions;

    public void SetAvailable(bool available)
    {
        isAvailable = available;
    }
}