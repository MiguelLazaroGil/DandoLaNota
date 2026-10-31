using System.Collections.Generic;
using Unity.Cinemachine;      // Cinemachine 3 (CinemachineCamera)
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;   // Volume (URP / HDRP)

[RequireComponent(typeof(GhostEntity))]
public class GhostController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Cámara del fantasma. Si está vacía se usa Camera.main")]
    [SerializeField] private Camera ghostCamera;
    [Tooltip("ARRASTRA AQUÍ la cámara que quieres tener al poseer (un objeto de la escena o un prefab). " +
             "Al empezar se hace un CLON del objeto entero (hijos y todos sus componentes), así que queda igual que el original.\n" +
             "• Con una Cinemachine Camera: se clona la vcam; la Camera real con el Brain no se toca.\n" +
             "• Con una Camera normal: se clona y se activa en lugar de la del fantasma.\n" +
             "• Vacío: se clona la Camera del fantasma sin sus scripts.")]
    [SerializeField] private GameObject possessionCameraTemplate;
    [SerializeField] private ActionWheelUI actionWheel;

    [Header("Detección de músicos (tu sistema de interacción)")]
    [Tooltip("El InteractionDetector (CompleteTaggedDetector). Si lo dejas vacío se busca en los hijos de este objeto. " +
             "Su Filter debe aceptar a los músicos. El resaltado (HighlighterScript) y el texto (InteractTextScript) " +
             "funcionan solos porque escuchan a este detector.")]
    [SerializeField] private CompleteTaggedDetector interactionDetector;

    [Header("Efecto al poseer (solo lo ve quien posee)")]
    [Tooltip("Global Volume con el efecto de posesión. Déjalo ACTIVO en la escena: el script le pone peso 0 al empezar, " +
             "lo sube a 1 al poseer y lo baja a 0 al salir.")]
    [SerializeField] private Volume possessionVolume;
    [Tooltip("Segundos que tarda el efecto en aparecer / desaparecer (0 = instantáneo)")]
    [SerializeField, Min(0f)] private float volumeFadeTime = 0.4f;

    [Header("Cámara mientras posees")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [Tooltip("Grados máximos que puedes girar la cabeza a izquierda/derecha (cuando el músico no se está moviendo)")]
    [SerializeField] private float maxYaw = 70f;
    [Tooltip("X = límite mirando hacia arriba (negativo), Y = límite mirando hacia abajo")]
    [SerializeField] private Vector2 pitchLimits = new Vector2(-35f, 45f);
    [Tooltip("Solo con Cinemachine. Prioridad de la vcam clonada: debe ser MAYOR que la de la vcam del fantasma (10 en tu captura).")]
    [SerializeField] private int possessionCameraPriority = 20;
    [Tooltip("Solo con Cinemachine.\n" +
             "Desmarcado: la cámara va en los ojos del músico y la giras con el ratón. Se quitan Follow / Hard Look At " +
             "(control de posición y rotación) y se conserva todo lo demás: lente, ruido Perlin, extensiones...\n" +
             "Marcado: se conserva TODO, también Follow / Hard Look At, y la cámara sigue al músico igual que la del fantasma te sigue a ti.")]
    [SerializeField] private bool followMusician = false;

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

    [SerializeField]
    private InputAction moveAction =
        new InputAction("Move", InputActionType.Value, null, null, null, "Vector2");

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = true;

    public Musician TargetedMusician { get; private set; }

    private GhostEntity ghost;
    private Collider detectorCollider;         // collider trigger del InteractionDetector
    private Transform possessionCamTransform;  // cámara que hace de ojos del músico
    private GameObject possessionRoot;         // raíz del clon de la cámara de posesión
    private CinemachineCamera possessionVcam;  // != null -> estamos en modo Cinemachine
    private float volumeTargetWeight;          // 0 = efecto apagado, 1 = encendido

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

        // Cámara de posesión: clon de la plantilla que arrastraste (o de la cámara del fantasma si no hay plantilla)
        if (!SetupPossessionCamera())
        {
            enabled = false;
            return;
        }

        // El efecto de posesión empieza apagado
        if (possessionVolume != null) possessionVolume.weight = 0f;

        // Detector de interacción: el asignado o, si no, el que cuelgue de este objeto (el InteractionDetector)
        if (interactionDetector == null) interactionDetector = GetComponentInChildren<CompleteTaggedDetector>(true);
        if (interactionDetector != null)
            detectorCollider = interactionDetector.GetComponent<Collider>();
        else
            Debug.LogWarning("GhostController: no hay ningún CompleteTaggedDetector (InteractionDetector), no podrás poseer.", this);

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

        UpdatePossessionVolume();
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

    // El objetivo lo decide tu CompleteTaggedDetector (el que resalta y muestra el texto); aquí solo lo traducimos a Musician.
    // El objetivo puede ser un collider hijo del músico, por eso se busca el Musician hacia arriba.
    private Musician FindTargetedMusician()
    {
        if (interactionDetector == null || !interactionDetector.HasTarget()) return null;

        Musician m = interactionDetector.GetTarget().GetComponentInParent<Musician>();
        return (m != null && m.IsAvailable && !m.IsPossessed) ? m : null;
    }

    // Mientras posees, el detector se apaga: no detecta nada y se quita el resaltado y el texto del último objetivo.
    // Como no tiene objetivo, la mano (Interactor) tampoco puede interactuar con nada.
    private void SetInteractionActive(bool active)
    {
        if (interactionDetector == null) return;

        if (!active)
        {
            interactionDetector.SetLocked(true);
            interactionDetector.NullTarget();                  // olvida el objetivo y lo que hubiera dentro del trigger
            interactionDetector.onTargetChanged.Invoke(null);  // el Highlighter y el InteractText se limpian solos
            if (detectorCollider != null) detectorCollider.enabled = false;
        }
        else
        {
            // Al reactivar el trigger, Unity vuelve a avisar de todo lo que ya tenga dentro
            if (detectorCollider != null) detectorCollider.enabled = true;
            interactionDetector.SetLocked(false);
        }
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

        possessionCamTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
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
        if (musician.cameraFirstPersonPoint == null)
            Debug.LogWarning($"{musician.name} no tiene Camera First Person Point asignado: la cámara se coloca en su pivote.", musician);

        // Cambiamos a la cámara del músico: la colocamos en sus ojos y apagamos la del fantasma
        AttachPossessionCamera(musician);
        //Desactivar componentes propios(camara,player input en componentactivator.deactivate...)
        //Activar componentes de musician(en componentactivator.activate... encontrar en Musician.GetcomponentInchildren<ComponentActivator>() )
        possessionCamTransform.localPosition = Vector3.zero;
        possessionCamTransform.localRotation = Quaternion.identity;
        yaw = 0f;
        pitch = 0f;
        SetActiveCamera(true);
        SetPossessionEffect(true);

        // La rueda muestra las acciones de este músico
        if (actionWheel != null) actionWheel.LoadActions(musician.availableActions);

        TargetedMusician = null;
        SetInteractionActive(false);
        SetGhostPresence(false);
    }

    // Se llama tanto al salir voluntariamente como cuando el director te golpea
    private void HandleLeft(Musician musician)
    {
        if (actionWheel != null) actionWheel.CloseWheel();

        // El fantasma reaparece donde estaba el músico (puede haberse movido), para que la cámara no pegue un salto
        MoveGhostTo(musician.transform.position);

        // Devolvemos la vista a la cámara del fantasma y guardamos la del músico en el fantasma
        SetActiveCamera(false);
        SetPossessionEffect(false);
        possessionCamTransform.SetParent(transform, false);

        SetInteractionActive(true);
        SetGhostPresence(true);
    }

    // Intercambia qué cámara se ve. También se apaga el AudioListener del fantasma para no tener dos a la vez.
    private void SetActiveCamera(bool possessing)
    {
        // Con Cinemachine hay UNA sola Camera real (la del Brain) y no se apaga: lo que cambia es qué vcam está "live"
        // (la clonada tiene más prioridad, así que el Brain hace la transición hacia ella al activarla).
        if (possessionVcam != null)
        {
            possessionRoot.SetActive(possessing);
            return;
        }

        ghostCamera.enabled = !possessing;

        AudioListener ghostListener = ghostCamera.GetComponent<AudioListener>();
        if (ghostListener != null) ghostListener.enabled = !possessing;

        possessionRoot.SetActive(possessing);
    }

    // Prepara la cámara de posesión a partir de la plantilla del inspector. Se CLONA el objeto entero (hijos y todos
    // sus componentes), así que queda igual que el original. Devuelve false si la plantilla no sirve.
    private bool SetupPossessionCamera()
    {
        CinemachineCamera templateVcam = possessionCameraTemplate != null
            ? possessionCameraTemplate.GetComponentInChildren<CinemachineCamera>(true)
            : null;

        if (templateVcam != null)
        {
            // 1) Cinemachine: se clona la vcam. La Camera real (la del Brain) no se toca.
            possessionRoot = InstantiateDetached(possessionCameraTemplate);
            possessionVcam = possessionRoot.GetComponentInChildren<CinemachineCamera>(true);
            possessionVcam.Priority.Enabled = true;
            possessionVcam.Priority.Value = possessionCameraPriority;

            if (!followMusician) RemoveBodyAndAim(possessionRoot);
        }
        else
        {
            // 2) Camera normal (o sin plantilla: se clona la del fantasma sin sus scripts)
            GameObject source = possessionCameraTemplate != null ? possessionCameraTemplate : ghostCamera.gameObject;
            possessionRoot = InstantiateDetached(source);
            if (possessionCameraTemplate == null) StripClonedCamera(possessionRoot);

            if (possessionRoot.GetComponentInChildren<Camera>(true) == null)
            {
                Debug.LogError("GhostController: la plantilla de cámara no tiene ninguna Camera ni Cinemachine Camera.", this);
                Destroy(possessionRoot.transform.parent.gameObject);
                return false;
            }
        }

        // Inactiva hasta que poseas (así no hay dos AudioListener ni dos cámaras compitiendo)
        GameObject holder = possessionRoot.transform.parent.gameObject;
        possessionRoot.SetActive(false);
        possessionRoot.transform.SetParent(transform, false);
        Destroy(holder);

        possessionCamTransform = possessionRoot.transform;
        return true;
    }

    // Instancia el clon DENTRO de un contenedor inactivo, para que no se ejecuten el Awake/OnEnable de los
    // scripts clonados mientras lo preparamos.
    private static GameObject InstantiateDetached(GameObject source)
    {
        GameObject holder = new GameObject("~PossessionCameraHolder");
        holder.SetActive(false);

        GameObject go = Instantiate(source, holder.transform);
        go.name = "PossessionCamera";
        go.tag = "Untagged"; // que no compita con Camera.main
        return go;
    }

    // Modo "ojos del músico": se quita el control de posición y de rotación (Follow, Hard Look At...) para que la
    // cámara vaya en los ojos del músico. Se conserva el resto: lente, ruido Perlin, extensiones, etc.
    private static void RemoveBodyAndAim(GameObject root)
    {
        foreach (CinemachineComponentBase c in root.GetComponentsInChildren<CinemachineComponentBase>(true))
        {
            if (c == null) continue;
            if (c.Stage == CinemachineCore.Stage.Body || c.Stage == CinemachineCore.Stage.Aim)
                DestroyImmediate(c);
        }
    }

    // Coloca la cámara de posesión en el músico que acabas de poseer
    private void AttachPossessionCamera(Musician musician)
    {
        if (possessionVcam != null && followMusician)
        {
            // La vcam conserva su Follow / Look At, pero ahora apuntando al músico
            var target = possessionVcam.Target;
            target.TrackingTarget = musician.CameraPoint;
            target.LookAtTarget = musician.CameraPoint;
            possessionVcam.Target = target;
        }
        else
        {
            // Ojos del músico: la cámara cuelga de su punto de cámara
            possessionCamTransform.SetParent(musician.CameraPoint, false);
        }
    }

    // Deja solo lo necesario para renderizar: fuera hijos y fuera los scripts de gameplay de la cámara original
    // (movimiento, look, Cinemachine...). Se conservan los componentes de render de URP/HDRP/Post Processing.
    private static void StripClonedCamera(GameObject go)
    {
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(go.transform.GetChild(i).gameObject);

        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;

            string ns = mb.GetType().Namespace;
            if (ns != null && ns.StartsWith("UnityEngine.Rendering")) continue;

            DestroyImmediate(mb);
        }
    }

    // ───────────────────────── Efecto de posesión (Global Volume) ─────────────────────────

    // Un Volume solo afecta a lo que renderiza la máquina donde está, así que en red lo ve únicamente quien posee
    // (el director y los demás no). Y solo está encendido mientras posees, así que nunca se ve
    // desde la vista libre del fantasma.
    private void SetPossessionEffect(bool on)
    {
        volumeTargetWeight = (on && ghost.IsLocallyControlled) ? 1f : 0f;
    }

    private void UpdatePossessionVolume()
    {
        if (possessionVolume == null) return;

        float speed = volumeFadeTime > 0f ? 1f / volumeFadeTime : float.MaxValue;
        possessionVolume.weight = Mathf.MoveTowards(possessionVolume.weight, volumeTargetWeight, speed * Time.deltaTime);
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