using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// This class should be the base class for any complex interactable item in your game, 
/// the base form will only play events when interacted which might suit your needs
/// </summary>
/// 
public class Interactable : MonoBehaviour
{
    [SerializeField]
    protected UnityEvent onInteractEvents;
    [Space]
    [SerializeField]
    protected bool destroyOnInteract;
    /// <summary>
    /// Called ideally from an item detector will do as the inhereted class implements it, normally with diferent outcomes depending on the code
    /// 
    /// </summary>
    /// <param name="code">Normally a casted Enum</param>
    public virtual void Interact()
    {
        BatutaController batuta = Object.FindAnyObjectByType<BatutaController>();
        if (batuta != null)
        {
            batuta.DesactivarModoDirector();
        }

        onInteractEvents?.Invoke();
        if (destroyOnInteract) GameObject.Destroy(gameObject);

    }

    /// <summary>
    /// Called ideally from an item detector will do as the inhereted class implements it, normally with diferent outcomes depending on the code
    /// 
    /// </summary>
    /// <param name="code">Normally a casted Enum</param>
    public virtual void Interact(int code = 0)
    {
        Interact();
        if (destroyOnInteract) GameObject.Destroy(gameObject);

    }

    protected void PlayInteractionEvents()
    {
        onInteractEvents?.Invoke();
    }

}