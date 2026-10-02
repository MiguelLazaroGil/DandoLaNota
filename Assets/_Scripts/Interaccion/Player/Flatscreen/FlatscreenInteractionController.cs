
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
public class FlatscreenInteractionController : MonoBehaviour
{
    [SerializeField]
    PlayerInput otherInput;
    public static FlatscreenInteraction playerInput;


    public UnityEvent OnInteractEvents;
    public UnityEvent OnDropEvents;

    public UnityEvent OnDisablePlayerInputEvents;
    public UnityEvent OnEnablePlayerInputEvents;
   
    public bool isInteracting = false;
    public bool isDropping = false;

    public void DisablePlayerInput()
    {
        playerInput.main.Disable();

        playerInput.escape.Enable(); //por si acaso
        //Esto se hace si hay un character controller
        if (otherInput != null)
        {
            otherInput.enabled = false;
        }
        OnDisablePlayerInputEvents?.Invoke();
    }

    public void EnablePlayerInput()
    {
        //Reactiva todas las acciones del playerInput
        playerInput.main.Enable();

        playerInput.escape.Enable(); //por si acaso

        
        if (otherInput != null)
        {
            otherInput.enabled = true;
        }
        OnEnablePlayerInputEvents?.Invoke();
    }
    private void OnEscape(InputAction.CallbackContext context)
    {
        if (PauseHandler.gameIsPaused)
        {
            PauseHandler.Resume();
        }
        else
        {
            PauseHandler.Pause();
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        isInteracting = true;
        OnInteractEvents?.Invoke();
    }

    private void StopInteractingMain(InputAction.CallbackContext context) 
    { 
        isInteracting=false;
    }

    private void OnDrop(InputAction.CallbackContext context)
    {
        isInteracting = isDropping;
        OnDropEvents?.Invoke();
    }

    private void StopDrop(InputAction.CallbackContext context)
    {
        isInteracting = false;
        isDropping = false;
    }

    private void Awake()
    {
        if (playerInput == null)
        {
            playerInput = new FlatscreenInteraction();
        }

    }

    private void OnEnable()
    {
        PauseHandler.OnPause.AddListener(DisablePlayerInput);
        PauseHandler.OnResume.AddListener(EnablePlayerInput);
        playerInput.Enable();

        playerInput.escape.escape.performed += OnEscape;

        playerInput.main.Interact.performed += OnInteract;
        playerInput.main.Interact.canceled += StopInteractingMain;

        playerInput.main.Drop.performed += OnDrop;
        playerInput.main.Drop.canceled += StopDrop;

    }
    private void OnDisable()
    {
        PauseHandler.OnResume.RemoveListener(DisablePlayerInput);
        PauseHandler.OnResume.RemoveListener(EnablePlayerInput);
        playerInput.Disable();
        playerInput.escape.escape.performed -= OnEscape;

       
        playerInput.main.Interact.performed -= OnInteract;
        playerInput.main.Interact.canceled -= StopInteractingMain;


        playerInput.main.Drop.performed -= OnDrop;
        playerInput.main.Drop.canceled -= StopDrop;

    }
}
