using UnityEngine;
using UnityEngine.InputSystem;

public class EscapeKeyHandler : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            EjecutarMiFuncion();
        }
    }

    private void EjecutarMiFuncion()
    {
        PauseHandler.TogglePaused();
    }
}