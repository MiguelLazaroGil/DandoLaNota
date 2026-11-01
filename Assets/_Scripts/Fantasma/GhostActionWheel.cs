using UnityEngine;

/// <summary>
/// Lógica de la rueda de acciones mientras posees: abrir, apuntar con el ratón, confirmar al soltar y lanzar la acción.
/// </summary>
public class GhostActionWheel : MonoBehaviour, IGhostSystemPart
{
    [Header("Rueda de acciones")]
    [SerializeField] private ActionWheelUI actionWheel;
    [Tooltip("Hasta dónde se acumula el movimiento del ratón al apuntar en la rueda (píxeles)")]
    [SerializeField] private float wheelCursorRadius = 80f;
    [Tooltip("Si al soltar Tab has apuntado menos que esto, se cancela. Mantenlo igual que la zona muerta de ActionWheelUI (10 px)")]
    [SerializeField] private float wheelDeadZone = 10f;

    private GhostEntity ghost;

    // Con el cursor bloqueado no hay posición de ratón, así que acumulamos el movimiento
    // como si fuera un joystick virtual para apuntar en la rueda.
    private Vector2 wheelCursor;

    public bool IsOpen => actionWheel != null && actionWheel.IsOpen;

    private void Awake()
    {
        ghost = GetComponent<GhostEntity>();

        if (actionWheel == null)
            Debug.LogWarning("GhostActionWheel: falta asignar el ActionWheelUI, no podrás usar acciones.", this);
    }

    private void Start()
    {
        // La rueda empieza cerrada
        Close();
    }

    public void Open()
    {
        if (actionWheel == null) return;

        wheelCursor = Vector2.zero;
        actionWheel.OpenWheel();
    }

    public void Close()
    {
        if (actionWheel != null) actionWheel.CloseWheel();
    }

    // La rueda muestra las acciones de este músico
    public void LoadActions(Musician musician)
    {
        if (actionWheel != null) actionWheel.LoadActions(musician.availableActions);
    }

    // Se llama cada frame mientras la rueda está abierta.
    // lookInput = delta del ratón; wheelHeld = la tecla de la rueda sigue pulsada.
    public void Tick(Vector2 lookInput, bool wheelHeld)
    {
        wheelCursor = Vector2.ClampMagnitude(wheelCursor + lookInput, wheelCursorRadius);
        actionWheel.UpdateSelection(wheelCursor);

        // Al soltar Tab se confirma
        if (!wheelHeld)
            ConfirmSelection();
    }

    private void ConfirmSelection()
    {
        // Si no has apuntado a nada (cursor en la zona muerta) se cancela, para no lanzar la acción 0 sin querer
        bool aimedSomething = wheelCursor.sqrMagnitude >= wheelDeadZone * wheelDeadZone;
        SabotageAction selected = aimedSomething ? actionWheel.GetSelectedAction() : null;

        actionWheel.CloseWheel();

        if (selected != null) UseAction(selected);
    }

    private void UseAction(SabotageAction action)
    {
        // Las acciones continuas funcionan como interruptor: elegirla otra vez la apaga
        if (action.isContinuous && ghost.ActiveContinuousAction == action)
        {
            ghost.StopContinuousAction();
            return;
        }

        if (!ghost.ExecuteSabotage(action))
            Debug.Log($"No se pudo ejecutar \"{action.actionName}\" (¿poca energía?)");
    }
}