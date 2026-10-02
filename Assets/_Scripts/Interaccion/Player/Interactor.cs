using UnityEngine;

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
            //Si ya lo ha cogido nada más
            return;
        }

        //Interacción con objeto no cogible
        Interactable interactable = temp.GetComponent<Interactable>();
        InteractWith(interactable);
    }
    public void TryDrop()
    {
        if(!isGrabbing) return;

        currentGrabbedObj.Drop();
        currentGrabbedObj = null;
        isGrabbing = false;


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
        InteractWith(interactable);
        return;
    }

}
