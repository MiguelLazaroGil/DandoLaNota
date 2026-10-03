using UnityEngine;
using UnityEngine.InputSystem;

public class EnableInputOnStart : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;

    private void Start()
    {
        inputActions.FindActionMap("main").Enable();
    }

}
