using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ButtonSelectTrigger : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Select Events")]
    [Tooltip("When selected.")]
    [SerializeField]
    public UnityEvent onSelected;

    [Tooltip("When deselected.")]
    [SerializeField]

    public UnityEvent onDeselected;
    [Tooltip("When hover")]
    [SerializeField]

    public UnityEvent onHover;
    [Tooltip("When exit hover")]
    [SerializeField]
    public UnityEvent onHoverExit;


    public void OnSelect(BaseEventData eventData)
    {
        onSelected?.Invoke();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        onDeselected?.Invoke();
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        onHover?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHoverExit?.Invoke();
    }
}