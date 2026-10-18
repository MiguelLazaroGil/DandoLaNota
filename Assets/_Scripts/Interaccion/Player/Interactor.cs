using UnityEngine;
using UnityEngine.Events;

public class Interactor : MonoBehaviour
{

    [SerializeField]
    CompleteTaggedDetector detector;
    [SerializeField]
    Transform grabPivot;
    [SerializeField, ReadOnly]
    bool isGrabbing;

    [SerializeField, ReadOnly]
    Grabbable currentGrabbedObj;

    [Header("Eventos")]
    public UnityEvent<Grabbable> OnGrabSuccess;
    public UnityEvent<Grabbable> OnDropSuccess;

    public UnityEvent<Interactable> OnInteractionInHandSuccess;
    public UnityEvent<Interactable> OnInteractionOtherSuccess;

    private void Awake()
    {
        if(grabPivot == null)
        {
            grabPivot= this.transform;
        }
    }

    public void TryInteract()
    {
        //Si tienes objeto en la mano, lo usas
        if (isGrabbing)
        {
            InteractWithGrabbedObj();
            return;
        }

        //No interaccion
        if (!detector.HasTarget())
        {
            return;
        }

        var temp = detector.GetTarget();


       //Si es grabbable se coge antes
        Grabbable grabbable = temp.GetComponent<Grabbable>();
        if (Grab(grabbable))

        {
            OnGrabSuccess?.Invoke(grabbable);
            //Si ya lo ha cogido nada más
            return;
        }

        //Interacción con objeto no cogible
        Interactable interactable = temp.GetComponent<Interactable>();
        if(InteractWith(interactable))
        {
            OnInteractionOtherSuccess?.Invoke(interactable);
        }
    }
    public void TryDrop()
    {
        if(!isGrabbing) return;

        currentGrabbedObj.Drop();

        isGrabbing = false;
        OnDropSuccess?.Invoke(currentGrabbedObj);
        currentGrabbedObj = null;


    }

    public bool Grab(Grabbable grabbable)
    {
        if(grabbable != null)
        {
            isGrabbing = true;
            currentGrabbedObj = grabbable;
            grabbable.Grab(grabPivot);
            return true;
        }
        return false;

    }
    public bool InteractWith(Interactable interactable)
    {
        if (interactable != null)
        {
            interactable.Interact();
            return true;
        }
        return false;
    }

    public void InteractWithGrabbedObj()
    {

        Interactable interactable = currentGrabbedObj.GetComponent<Interactable>();
       if(InteractWith(interactable))
        {
            OnInteractionInHandSuccess?.Invoke(interactable);
        }
        return;
    }

}
