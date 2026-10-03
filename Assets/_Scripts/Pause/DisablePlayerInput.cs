using UnityEngine;
using UnityEngine.InputSystem;

public class DisablePlayerInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;

    private void Start()
    {
        inputActions.FindActionMap("main").Disable();
    }
    private void OnEnable()
    {
        inputActions.FindActionMap("main").Disable();

    }
    private void OnDisable()
    {
        inputActions.FindActionMap("main").Enable();

    }
}