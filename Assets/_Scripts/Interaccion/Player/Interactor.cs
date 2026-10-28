using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{

    [SerializeField]
    CompleteTaggedDetector detector;
    [SerializeField]
    Transform grabPivot;
    [SerializeField, ReadOnly]
    bool isGrabbing;
    public bool IsGrabbing { get => isGrabbing; }

    [SerializeField, ReadOnly]
    Grabbable currentGrabbedObj;
    [SerializeField]
    float minHoldTimeForThrow = 0.1f;
    [SerializeField]
    float maxHoldTime = 3f;
    [SerializeField]
    float parabolaHeight = 1f;
    [SerializeField]
    float maxAmplitude = 10f;

    [Header("Eventos")]
    public UnityEvent<Grabbable> OnGrabSuccess;
    public UnityEvent<Grabbable> OnDropSuccess;

    public UnityEvent<Interactable> OnInteractionInHandSuccess;
    public UnityEvent<Interactable> OnInteractionOtherSuccess;

    private void Awake()
    {
        if (grabPivot == null)
        {
            grabPivot = this.transform;
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
            //Si ya lo ha cogido nada m�s
            return;
        }

        //Interacci�n con objeto no cogible
        Interactable interactable = temp.GetComponent<Interactable>();
        if (InteractWith(interactable))
        {
            OnInteractionOtherSuccess?.Invoke(interactable);
        }
    }
    private float pressStartTime; // Variable privada para registrar el timestamp de inicio
    public void OnReleaseObjectInput(InputAction.CallbackContext ctx)
    {


        if (!isGrabbing) return;

        // 1. Cuando el jugador empieza a presionar el botón
        if (ctx.started)
        {
            pressStartTime = Time.time;
        }

        // 2. Cuando el jugador suelta el botón
        if (ctx.canceled)
        {
            // Calculamos el tiempo real transcurrido entre el inicio y el final
            float holdTime = Time.time - pressStartTime;

            // Si el tiempo es menor al umbral, es un Drop (soltar rápido)
            if (holdTime < minHoldTimeForThrow)
            {
                TryDrop();
            }
            else
            {
                // Si se mantuvo pulsado, es un Throw (lanzamiento cargado)
                TryThrow(ctx);

            }
        }
    }
    public void TryDrop()
    {
        if (!isGrabbing) return;

        currentGrabbedObj.Drop();

        isGrabbing = false;
        OnDropSuccess?.Invoke(currentGrabbedObj);
        currentGrabbedObj = null;


    }
    public void TryThrow(InputAction.CallbackContext ctx)
    {
        if (!isGrabbing) return;

        //Physics

        Vector3 throwDirection = grabPivot.transform.forward;
        float holdTime = (float)ctx.duration;
        float rawRatio = Mathf.Clamp01(holdTime / maxHoldTime);

        // 2. Curva para llegar antes a la amplitud máxima (crece rápido al inicio)
        float chargeRatio = Mathf.Pow(rawRatio, 0.5f);
        float currentAmplitude = Mathf.Lerp(0f, maxAmplitude, chargeRatio);

        // 3. Componente vertical para la altura de la parábola
        float gravity = Mathf.Abs(Physics.gravity.y);
        float verticalVelocity = Mathf.Sqrt(2f * gravity * parabolaHeight);

        // 4. Componente horizontal basada en la dirección y la amplitud calculada
        Vector3 horizontalDir = new Vector3(throwDirection.x, 0f, throwDirection.z).normalized;
        if (horizontalDir == Vector3.zero) horizontalDir = transform.forward;

        Vector3 horizontalVelocity = horizontalDir * currentAmplitude;

        // Retornamos el vector combinado
        TryThrow(new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z));
    }
    public void TryThrow(Vector3 throwDirection)
    {
        currentGrabbedObj.Throw(throwDirection);
        isGrabbing = false;
        OnDropSuccess?.Invoke(currentGrabbedObj);
        currentGrabbedObj = null;


    }
    public bool Grab(Grabbable grabbable)
    {
        if (grabbable != null)
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
        if (InteractWith(interactable))
        {
            OnInteractionInHandSuccess?.Invoke(interactable);
        }
        return;
    }

}