using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

public class GhostPlayerController : MonoBehaviour
{
    [Header("Referencias de tu PlayerCapsule")]
    public GhostEntity ghostEntity;
    public FirstPersonController fpsController;
    public CharacterController charController;
    public Transform playerCameraRoot;
    public ActionWheelUI actionWheel;

    private bool isWalkingWithMusician = false;

    private void Update()
    {
        // Si no estamos dentro de un músico, el movimiento lo hace FirstPersonController automáticamente
        if (ghostEntity.CurrentMusician == null)
        {
            // Botón E para intentar poseer al músico que tengamos delante
            if (Input.GetKeyDown(KeyCode.E))
            {
                TryInteractOrPossess();
            }
            return;
        }

        // --- DENTRO DEL MÚSICO ---

        // 1. Salir del músico (con E)
        if (Input.GetKeyDown(KeyCode.E))
        {
            ghostEntity.RequestVoluntaryExit();
        }

        // 2. Alternar entre tocar bien (fingir) o tocar mal básico con la tecla Q
        if (Input.GetKeyDown(KeyCode.Q))
        {
            var m = ghostEntity.CurrentMusician;
            var nuevoEstado = m.currentState == Musician.PlayingState.PlayingWell
                ? Musician.PlayingState.PlayingBadly
                : Musician.PlayingState.PlayingWell;
            m.SetPlayingState(nuevoEstado);
            Debug.Log($"Modo de toque cambiado a: {nuevoEstado}");
        }

        // 3. Si activó la acción de levantarse y caminar con WASD, sincronizamos la posición del músico con nuestra cápsula
        if (isWalkingWithMusician)
        {
            ghostEntity.CurrentMusician.transform.position = transform.position;
            ghostEntity.CurrentMusician.transform.rotation = transform.rotation;
        }

        // 4. Rueda de acciones (Mantener Click Derecho o Tab)
        HandleActionWheel();
    }

    private void TryInteractOrPossess()
    {
        // Lanza un rayo desde la cámara en primera persona (puedes conectarlo a tu Interactor actual si prefieres)
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            if (hit.collider.TryGetComponent(out Musician musician))
            {
                ghostEntity.TryPossess(musician);
            }
            //else if (hit.collider.TryGetComponent(out AudienceInteractable audience))
            //{
            //    audience.InteractWithAudience(ghostEntity);
            //}
        }
    }

    // Llamado automáticamente por GhostEntity cuando entra en un músico
    private void OnPossessMusician(Musician musician)
    {
        // Desactivamos el movimiento WASD de Starter Assets (pero dejamos que pueda mirar con el ratón si quieres, o lo bloqueamos)
        fpsController.MoveSpeed = 0f;
        fpsController.SprintSpeed = 0f;
        charController.enabled = false;

        // Colocamos nuestra cápsula/cámara en el punto de vista en 1ª persona del músico
        transform.position = musician.transform.position;
        transform.rotation = musician.transform.rotation;

        // Cargamos en la rueda las acciones disponibles (filtrando o permitiendo todas porque somos Player)
        actionWheel.LoadActions(musician.availableActions);
    }

    // Llamado automáticamente por GhostEntity al salir o ser golpeado
    private void OnLeaveMusician(Musician musician)
    {
        SetMusicianWalkMode(false);
        actionWheel.CloseWheel();

        // Devolvemos la velocidad al fantasma para que vuelva a volar/caminar en Ghost Cam
        charController.enabled = true;
        fpsController.MoveSpeed = 4f; // Pon aquí tu velocidad original del Inspector
        fpsController.SprintSpeed = 6f;

        //Damos un pequeño salto atrás para salir del cuerpo visualmente
        transform.position += Vector3.up * 1.5f - transform.forward * 1.5f;
    }

    // Para la Acción 6 (levantarse de la silla y moverse con el músico usando WASD)
    public void SetMusicianWalkMode(bool canWalk)
    {
        isWalkingWithMusician = canWalk;
        charController.enabled = canWalk;
        fpsController.MoveSpeed = canWalk ? 3.5f : 0f;
        fpsController.SprintSpeed = canWalk ? 5f : 0f;
    }

    private void HandleActionWheel()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Tab))
        {
            Cursor.lockState = CursorLockMode.None; // Liberar el ratón para elegir en la rueda
            Cursor.visible = true;
            actionWheel.OpenWheel();
        }

        if (actionWheel.IsOpen)
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = ((Vector2)Input.mousePosition - screenCenter);
            actionWheel.UpdateSelection(dir);

            if (Input.GetMouseButtonDown(0))
            {
                SabotageAction selected = actionWheel.GetSelectedAction();
                if (selected != null)
                {
                    ghostEntity.ExecuteSabotage(selected);
                    CloseWheelAndLockCursor();
                }
            }
        }

        if (Input.GetMouseButtonUp(1) || Input.GetKeyUp(KeyCode.Tab))
        {
            CloseWheelAndLockCursor();
        }
    }

    private void CloseWheelAndLockCursor()
    {
        actionWheel.CloseWheel();
        Cursor.lockState = CursorLockMode.Locked; // Devolver el ratón a la cámara en 1ª persona
        Cursor.visible = false;
    }
}