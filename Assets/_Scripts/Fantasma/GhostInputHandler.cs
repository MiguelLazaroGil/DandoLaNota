using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrada del fantasma (New Input System). Solo expone el estado de las acciones;
/// qué hacer con ellas lo decide GhostController.
/// </summary>
public class GhostInputHandler : MonoBehaviour, IGhostSystemPart
{
    [Header("Input (New Input System) - puedes cambiar los bindings en el inspector")]
    [SerializeField]
    private InputAction possessExitAction =
        new InputAction("PossessExit", InputActionType.Button, "<Keyboard>/e");

    [SerializeField]
    private InputAction wheelAction =
        new InputAction("ActionWheel", InputActionType.Button, "<Keyboard>/tab");

    [SerializeField]
    private InputAction lookAction =
        new InputAction("Look", InputActionType.Value, "<Mouse>/delta", null, null, "Vector2");

    [SerializeField]
    private InputAction moveAction =
        new InputAction("Move", InputActionType.Value, null, null, null, "Vector2");

    /// <summary>Poseer (en modo libre) / salir (mientras posees): se pulsó este frame.</summary>
    public bool PossessExitPressed => possessExitAction.WasPressedThisFrame();

    /// <summary>Se pulsó la tecla de la rueda este frame.</summary>
    public bool WheelPressed => wheelAction.WasPressedThisFrame();

    /// <summary>La tecla de la rueda sigue mantenida.</summary>
    public bool WheelHeld => wheelAction.IsPressed();

    /// <summary>Movimiento del ratón (delta).</summary>
    public Vector2 Look => lookAction.ReadValue<Vector2>();

    /// <summary>Movimiento WASD.</summary>
    public Vector2 Move => moveAction.ReadValue<Vector2>();

    // Se llama en el editor al añadir el componente (o con Reset en el menú ⋮ del inspector):
    // así los bindings WASD quedan guardados y visibles en el inspector.
    private void Reset()
    {
        AddDefaultMoveBindings();
    }

    // Solo añade WASD si la acción no tiene ningún binding (si ya los configuraste a mano, los respeta)
    private void AddDefaultMoveBindings()
    {
        if (moveAction.bindings.Count > 0) return;

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
    }

    private void Awake()
    {
        // Para componentes que ya existían antes de este arreglo y no tienen bindings guardados
        AddDefaultMoveBindings();
    }

    private void OnEnable()
    {
        possessExitAction.Enable();
        wheelAction.Enable();
        lookAction.Enable();
        moveAction.Enable();
    }

    private void OnDisable()
    {
        possessExitAction.Disable();
        wheelAction.Disable();
        lookAction.Disable();
        moveAction.Disable();
    }
}