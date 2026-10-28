using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla al fantasma usando el New Input System:
///  - Mirar a un músico y pulsar E   -> poseerlo (la cámara pasa a su punto de primera persona).
///                                      Mientras estás dentro NO puedes moverte por tu cuenta.
///  - Mantener Tab                   -> abre la rueda de acciones; apuntas con el ratón y al soltar Tab
///                                      se ejecuta la acción elegida (si no apuntas a ninguna, se cancela).
///                                      Las acciones continuas funcionan como interruptor:
///                                      elegirlas otra vez las apaga.
///  - Acción "Mover músico" activa   -> WASD mueve el cuerpo del músico y el ratón lo gira.
///  - E otra vez                     -> intenta salir (tarda exitCastTime, definido en GhostEntity)
///
/// Requisitos: la cámara debe ser hija del fantasma (o asignarla en el inspector) y los músicos
/// necesitan un Collider para poder ser detectados.
/// </summary>
[RequireComponent(typeof(GhostEntity))]
public class GhostController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Cámara del fantasma. Si está vacía se usa Camera.main")]
    [SerializeField] private Camera ghostCamera;
    [SerializeField] private ActionWheelUI actionWheel;

    [Header("Detección de músicos")]
    [SerializeField] private float possessRange = 8f;
    [Tooltip("Radio del SphereCast: más grande = más fácil apuntar")]
    [SerializeField] private float aimRadius = 0.4f;
    [Tooltip("Capas que bloquean/detectan la mirada (incluye la capa de los músicos y las paredes)")]
    [SerializeField] private LayerMask aimMask = ~0;

    [Header("Cámara mientras posees")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [Tooltip("Grados máximos que puedes girar la cabeza a izquierda/derecha (cuando el músico no se está moviendo)")]
    [SerializeField] private float maxYaw = 70f;
    [Tooltip("X = límite mirando hacia arriba (negativo), Y = límite mirando hacia abajo")]
    [SerializeField] private Vector2 pitchLimits = new Vector2(-35f, 45f);

    [Header("Rueda de acciones")]
    [Tooltip("Hasta dónde se acumula el movimiento del ratón al apuntar en la rueda (píxeles)")]
    [SerializeField] private float wheelCursorRadius = 80f;
    [Tooltip("Si al soltar Tab has apuntado menos que esto, se cancela. Mantenlo igual que la zona muerta de ActionWheelUI (10 px)")]
    [SerializeField] private float wheelDeadZone = 10f;

    [Header("Mientras posees...")]
    [Tooltip("Desactiva automáticamente TODOS los demás scripts que haya en este mismo objeto " +
             "(tu movimiento libre, etc.) mientras estás dentro de un músico, y los reactiva al salir. " +
             "No toca GhostController ni GhostEntity. Si algún script debe seguir activo, ponlo en Keep Enabled.")]
    [SerializeField] private bool autoDisableOtherScripts = true;
    [Tooltip("Scripts que NO se desactivan con la opción automática")]
    [SerializeField] private Behaviour[] keepEnabledWhilePossessing;
    [Tooltip("Scripts a desactivar mientras posees aunque estén en otro objeto (ej. un script de movimiento " +
             "o de cámara que esté en un hijo). NO pongas aquí GhostController ni GhostEntity.")]
    [SerializeField] private Behaviour[] disableWhilePossessing;
    [Tooltip("Objetos a ocultar mientras posees (ej. el modelo del fantasma). No pongas la cámara.")]
    [SerializeField] private GameObject[] hideWhilePossessing;

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

    // Los bindings WASD (composite 2DVector) NO se pueden crear aquí, en el constructor:
    // el Input System aún no está listo y da NullReferenceException. Se añaden en Reset() / Awake().
    [SerializeField]
    private InputAction moveAction =
        new InputAction("Move", InputActionType.Value, null, null, null, "Vector2");

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = true;

    /// <summary>Músico al que está apuntando el fantasma ahora mismo (null si ninguno). Útil para tu UI.</summary>
    public Musician TargetedMusician { get; private set; }

    private GhostEntity ghost;
    private Transform camTransform;

    // Pose original de la cámara para devolverla al salir
    private Transform camOriginalParent;
    private Vector3 camOriginalLocalPos;
    private Quaternion camOriginalLocalRot;

    private float yaw;
    private float pitch;

    // Scripts que hemos desactivado al poseer (para reactivar exactamente esos al salir)
    private readonly List<Behaviour> disabledByController = new List<Behaviour>();

    // Con el cursor bloqueado no hay posición de ratón, así que acumulamos el movimiento
    // como si fuera un joystick virtual para apuntar en la rueda.
    private Vector2 wheelCursor;

    private bool IsWheelOpen => actionWheel != null && actionWheel.IsOpen;

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
        ghost = GetComponent<GhostEntity>();

        // Para componentes que ya existían antes de este arreglo y no tienen bindings guardados
        AddDefaultMoveBindings();

        if (ghostCamera == null) ghostCamera = Camera.main;
        if (ghostCamera == null)
        {
            Debug.LogError("GhostController: no hay cámara asignada y no existe Camera.main", this);
            enabled = false;
            return;
        }
        camTransform = ghostCamera.transform;

        if (actionWheel == null)
            Debug.LogWarning("GhostController: falta asignar el ActionWheelUI, no podrás usar acciones.", this);
    }

    private void Start()
    {
        // La rueda empieza cerrada
        if (actionWheel != null) actionWheel.CloseWheel();
    }

    private void OnEnable()
    {
        possessExitAction.Enable();
        wheelAction.Enable();
        lookAction.Enable();
        moveAction.Enable();

        ghost.MusicianPossessed += HandlePossessed;
        ghost.MusicianLeft += HandleLeft;
    }

    private void OnDisable()
    {
        possessExitAction.Disable();
        wheelAction.Disable();
        lookAction.Disable();
        moveAction.Disable();

        ghost.MusicianPossessed -= HandlePossessed;
        ghost.MusicianLeft -= HandleLeft;
    }

    private void Update()
    {
        if (ghost.CurrentMusician == null) UpdateFreeMode();
        else UpdatePossessedMode();
    }

    // ───────────────────────── Modo libre (sin poseer) ─────────────────────────

    private void UpdateFreeMode()
    {
        TargetedMusician = FindTargetedMusician();

        if (TargetedMusician != null && possessExitAction.WasPressedThisFrame())
        {
            if (!ghost.TryPossess(TargetedMusician))
                Debug.Log("No se pudo poseer: ¿poca energía, aturdido o músico ocupado?");
        }
    }

    private Musician FindTargetedMusician()
    {
        Transform t = camTransform;
        if (Physics.SphereCast(t.position, aimRadius, t.forward, out RaycastHit hit,
                               possessRange, aimMask, QueryTriggerInteraction.Ignore))
        {
            Musician m = hit.collider.GetComponentInParent<Musician>();
            if (m != null && m.IsAvailable && !m.IsPossessed) return m;
        }
        return null;
    }

    // ───────────────────────── Modo poseído ─────────────────────────

    private void UpdatePossessedMode()
    {
        // Abrir la rueda (no se puede mientras estás saliendo)
        if (!ghost.IsExiting && !IsWheelOpen && wheelAction.WasPressedThisFrame())
            OpenWheel();

        // Si la acción "Mover músico" está activa, WASD mueve el cuerpo
        UpdateMovement();

        // Con la rueda abierta el ratón sirve para apuntar: no se mira ni se sale
        if (IsWheelOpen)
        {
            UpdateWheel();
            return;
        }

        // E mientras posees = intentar salir (GhostEntity se encarga del tiempo de casteo)
        if (possessExitAction.WasPressedThisFrame())
        {
            ghost.RequestVoluntaryExit();
            return;
        }

        UpdateLook();
    }

    private void UpdateMovement()
    {
        MusicianMovement movement = ghost.CurrentMusician.Movement;
        if (movement == null || !movement.IsControlled) return;

        movement.SetMoveInput(moveAction.ReadValue<Vector2>());
    }

    private void UpdateLook()
    {
        Vector2 delta = lookAction.ReadValue<Vector2>() * lookSensitivity;
        MusicianMovement movement = ghost.CurrentMusician.Movement;
        bool bodyIsFree = movement != null && movement.IsControlled;

        pitch = Mathf.Clamp(pitch - delta.y, pitchLimits.x, pitchLimits.y);

        if (bodyIsFree)
        {
            // El ratón gira el cuerpo entero (absorbe también el giro de cabeza que hubiera, sin saltos)
            movement.Rotate(yaw + delta.x);
            yaw = 0f;
        }
        else
        {
            // Sentado tocando: solo puedes girar la cabeza un poco
            yaw = Mathf.Clamp(yaw + delta.x, -maxYaw, maxYaw);
        }

        camTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // ───────────────────────── Rueda de acciones ─────────────────────────

    private void OpenWheel()
    {
        if (actionWheel == null) return;

        wheelCursor = Vector2.zero;
        actionWheel.OpenWheel();
    }

    private void UpdateWheel()
    {
        wheelCursor = Vector2.ClampMagnitude(wheelCursor + lookAction.ReadValue<Vector2>(), wheelCursorRadius);
        actionWheel.UpdateSelection(wheelCursor);

        // Al soltar Tab se confirma
        if (!wheelAction.IsPressed())
            ConfirmWheelSelection();
    }

    private void ConfirmWheelSelection()
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

    // ───────────────────────── Eventos de GhostEntity ─────────────────────────

    private void HandlePossessed(Musician musician)
    {
        // Guardamos la pose de la cámara para devolverla al salir
        camOriginalParent = camTransform.parent;
        camOriginalLocalPos = camTransform.localPosition;
        camOriginalLocalRot = camTransform.localRotation;

        // La cámara pasa a ser los ojos del músico
        camTransform.SetParent(musician.CameraPoint, false);
        camTransform.localPosition = Vector3.zero;
        camTransform.localRotation = Quaternion.identity;
        yaw = 0f;
        pitch = 0f;

        // La rueda muestra las acciones de este músico
        if (actionWheel != null) actionWheel.LoadActions(musician.availableActions);

        TargetedMusician = null;
        SetGhostPresence(false);
    }

    // Se llama tanto al salir voluntariamente como cuando el director te golpea
    private void HandleLeft(Musician musician)
    {
        if (actionWheel != null) actionWheel.CloseWheel();

        // El fantasma reaparece donde estaba el músico (puede haberse movido), para que la cámara no pegue un salto
        MoveGhostTo(musician.transform.position);

        camTransform.SetParent(camOriginalParent, false);
        camTransform.localPosition = camOriginalLocalPos;
        camTransform.localRotation = camOriginalLocalRot;

        SetGhostPresence(true);
    }

    private void SetGhostPresence(bool present)
    {
        if (present)
        {
            // Reactivamos solo lo que desactivamos nosotros
            foreach (Behaviour b in disabledByController)
                if (b != null) b.enabled = true;
            disabledByController.Clear();
        }
        else
        {
            foreach (Behaviour b in disableWhilePossessing)
                DisableIfEnabled(b);

            if (autoDisableOtherScripts)
            {
                foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
                {
                    if (mb == this || mb == ghost) continue;
                    if (keepEnabledWhilePossessing != null
                        && System.Array.IndexOf(keepEnabledWhilePossessing, mb) >= 0) continue;
                    DisableIfEnabled(mb);
                }
            }
        }

        foreach (GameObject go in hideWhilePossessing)
            if (go != null) go.SetActive(present);
    }

    private void DisableIfEnabled(Behaviour b)
    {
        if (b == null || !b.enabled) return;

        b.enabled = false;
        disabledByController.Add(b);
    }

    private void MoveGhostTo(Vector3 position)
    {
        // Un CharacterController activo pisa los cambios de transform.position
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position;
        if (cc != null) cc.enabled = true;
    }

    // ───────────────────────── Debug (sustitúyelo por tu UI) ─────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI || ghost == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 420, 220));
        GUILayout.Label($"Energía: {ghost.currentEnergy:0} / {ghost.maxEnergy:0}");

        if (ghost.IsStunned)
        {
            GUILayout.Label("ATURDIDO (cooldown)");
        }
        else if (ghost.CurrentMusician == null)
        {
            if (TargetedMusician != null)
                GUILayout.Label($"[E] Poseer a {TargetedMusician.name} (coste {ghost.possessInitialCost:0})");
        }
        else
        {
            GUILayout.Label(ghost.IsExiting ? "Saliendo..." : "[E] Salir del músico   |   [Tab] mantener: rueda de acciones");

            if (ghost.ActiveContinuousAction != null)
                GUILayout.Label($"Acción activa: {ghost.ActiveContinuousAction.actionName} (elígela otra vez para pararla)");

            MusicianMovement movement = ghost.CurrentMusician.Movement;
            if (movement != null && movement.IsControlled)
                GUILayout.Label("WASD: mover   |   Ratón: girar");
        }

        GUILayout.EndArea();
    }
}