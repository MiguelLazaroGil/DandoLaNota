using Unity.Cinemachine;      // Cinemachine 3 (CinemachineCamera)
using UnityEngine;

/// <summary>
/// Cámara que hace de "ojos del músico": prepara el clon de la plantilla, lo coloca en el músico poseído,
/// alterna con la cámara del fantasma y gestiona el giro de cabeza con el ratón.
/// </summary>
public class PossessionCamera : MonoBehaviour, IGhostSystemPart
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

    [Header("Cámara mientras posees")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [Tooltip("Grados máximos que puedes girar la cabeza a izquierda/derecha (cuando el músico no se está moviendo)")]
    [SerializeField] private float maxYaw = 70f;
    [Tooltip("X = límite mirando hacia arriba (negativo), Y = límite mirando hacia abajo")]
    [SerializeField] private Vector2 pitchLimits = new Vector2(-35f, 45f);
    [Tooltip("Solo en modo 'ojos del músico' (Follow Musician desmarcado). Desplazamiento de la cámara respecto al " +
             "Camera First Person Point del músico, en SUS ejes locales: Z = hacia delante, Y = arriba, X = derecha. " +
             "Sácala hacia delante para no quedarte dentro de la cabeza. Se aplica cada frame, así que puedes " +
             "ajustarlo con el juego en marcha. Con RolyPoly o con el cuerpo libre se aplica según hacia dónde miras, " +
             "sin inclinarse con el cuerpo.")]
    [SerializeField] private Vector3 possessionCameraOffset = new Vector3(0f, 0f, 0.3f);
    [Tooltip("Corrige la ORIENTACIÓN de la cámara respecto al Camera First Person Point, en grados " +
             "(X = arriba/abajo, Y = izquierda/derecha, Z = inclinación). Úsalo si la flecha azul (Z) del punto de cámara " +
             "NO apunta hacia donde mira la cabeza del músico: Y = 90 si la cabeza mira hacia el eje rojo (X) del punto, " +
             "Y = -90 si mira hacia el lado contrario, Y = 180 si mira hacia atrás; prueba X = ±90 si mira arriba/abajo. " +
             "Solo afecta sentado y si el músico NO tiene RolyPoly (con RolyPoly la dirección sale de sus físicas). " +
             "Se aplica cada frame, así que puedes ajustarlo con el juego en marcha " +
             "(lo que cambies con el juego en marcha se pierde al pararlo: apunta los valores).")]
    [SerializeField] private Vector3 eyeRotationOffset = Vector3.zero;
    [Tooltip("Solo con RolyPoly. Lo que puede adelantarse (grados) el objetivo de giro respecto a hacia donde mira el cuerpo. " +
             "Evita que con un giro de ratón muy rápido el cuerpo dé la vuelta por el lado contrario (las físicas siempre " +
             "toman el camino corto). Más bajo = el ratón se frena antes si el cuerpo no gira a tiempo.")]
    [SerializeField, Range(30f, 170f)] private float maxTargetLead = 120f;
    [Tooltip("Solo con Cinemachine. Prioridad de la vcam clonada: debe ser MAYOR que la de la vcam del fantasma (10 en tu captura).")]
    [SerializeField] private int possessionCameraPriority = 20;
    [Tooltip("Solo con Cinemachine.\n" +
             "Desmarcado: la cámara va en los ojos del músico y la giras con el ratón. Se quitan Follow / Hard Look At " +
             "(control de posición y rotación) y se conserva todo lo demás: lente, ruido Perlin, extensiones...\n" +
             "Marcado: se conserva TODO, también Follow / Hard Look At, y la cámara sigue al músico igual que la del fantasma te sigue a ti.")]
    [SerializeField] private bool followMusician = false;

    private Transform possessionCamTransform;  // cámara que hace de ojos del músico
    private GameObject possessionRoot;         // raíz del clon de la cámara de posesión
    private CinemachineCamera possessionVcam;  // != null -> estamos en modo Cinemachine

    private float yaw;
    private float pitch;

    // Músico poseído
    private MusicianMovement possessedMovement;  // su "buzón": aquí publicamos hacia dónde mira el jugador
    private Transform eyePoint;                  // su Camera First Person Point
    private bool possessing;

    private Vector2 lookDelta;                   // ratón acumulado (ya con sensibilidad) desde el último LateUpdate
    private bool lookingFree;                    // true = el cuerpo está libre y la cámara gira en el MUNDO
    private float worldYaw;                      // hacia dónde mira la cámara en el mundo (solo con el cuerpo libre)
    private Transform followedBody;              // cuerpo físico cuyos ojos seguimos (null = usar el punto de cámara tal cual)
    private Vector3 eyeInBody;                   // dónde están los ojos respecto a ese cuerpo, en sus ejes locales
    private float desiredYaw;                    // con RolyPoly: hacia dónde QUIERE mirar el cuerpo (lo mueve el ratón)
    private bool wasControlled;                  // con RolyPoly: el cuerpo estaba libre en el frame anterior

    // Lo llama GhostController desde su Awake. Devuelve false si no se puede preparar la cámara.
    public bool Initialize()
    {
        if (ghostCamera == null) ghostCamera = Camera.main;
        if (ghostCamera == null)
        {
            Debug.LogError("PossessionCamera: no hay cámara asignada y no existe Camera.main", this);
            return false;
        }

        // Cámara de posesión: clon de la plantilla que arrastraste (o de la cámara del fantasma si no hay plantilla)
        return SetupPossessionCamera();
    }

    // ───────────────────────── Posesión ─────────────────────────

    // Cambiamos a la cámara del músico: la colocamos en sus ojos y apagamos la del fantasma
    public void BeginPossession(Musician musician)
    {
        if (musician.cameraFirstPersonPoint == null)
            Debug.LogWarning($"{musician.name} no tiene Camera First Person Point asignado: la cámara se coloca en su pivote.", musician);

        possessedMovement = musician.Movement;
        eyePoint = musician.CameraPoint;
        lookingFree = false;
        wasControlled = false;
        lookDelta = Vector2.zero;

        AttachPossessionCamera(musician);

        possessionCamTransform.localPosition = Vector3.zero;
        ApplyEyeOffset();
        possessionCamTransform.localRotation = Quaternion.Euler(eyeRotationOffset);
        yaw = 0f;
        pitch = 0f;
        SetActiveCamera(true);

        possessing = true;
        PublishLookYaw();
    }

    // Devolvemos la vista a la cámara del fantasma y guardamos la del músico en el fantasma
    public void EndPossession()
    {
        possessing = false;
        if (possessedMovement != null) possessedMovement.ClearLookYaw();
        possessedMovement = null;
        eyePoint = null;

        SetActiveCamera(false);
        possessionCamTransform.SetParent(transform, false);
    }

    // lookInput = delta del ratón sin escalar (la sensibilidad se aplica aquí).
    // Solo acumula el movimiento: la cámara se coloca en LateUpdate, cuando las físicas y los huesos ya se han movido.
    public void UpdateLook(Vector2 lookInput)
    {
        lookDelta += lookInput * lookSensitivity;
    }

    private void LateUpdate()
    {
        if (!possessing || eyePoint == null) return;

        Vector2 delta = lookDelta;
        lookDelta = Vector2.zero;

        pitch = Mathf.Clamp(pitch - delta.y, pitchLimits.x, pitchLimits.y);

        // Con RolyPoly, hacia dónde mira la cámara sale de las FÍSICAS del músico (de su cuerpo).
        // Sin RolyPoly, de la orientación del Camera First Person Point.
        float bodyYaw = 0f;
        bool physicsGaze = CameraIsEyes && possessedMovement != null && possessedMovement.TryGetBodyLookYaw(out bodyYaw);

        if (physicsGaze) UpdateFromBodyPhysics(delta.x, bodyYaw);
        else UpdateFromEyePoint(delta.x);
    }

    // Con RolyPoly: la cámara mira hacia donde mira el cuerpo físico del músico, leído cada frame de sus físicas.
    // El ratón no gira la cámara directamente: mueve el OBJETIVO del cuerpo (MusicianMovement.LookYaw), el cuerpo gira
    // hacia él con su torque y la cámara lo acompaña. Sentado (o volviendo al asiento) solo giras la cabeza un poco.
    private void UpdateFromBodyPhysics(float deltaYaw, float bodyYaw)
    {
        bool controlled = possessedMovement.IsControlled;

        if (controlled)
        {
            // Al empezar, el objetivo es hacia donde ya mira el cuerpo (no se gira nada de golpe)
            if (!wasControlled) desiredYaw = bodyYaw;

            // El objetivo no se aleja más de maxTargetLead del cuerpo (ver tooltip)
            float lead = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, desiredYaw) + deltaYaw, -maxTargetLead, maxTargetLead);
            desiredYaw = bodyYaw + lead;

            yaw = 0f;
            possessedMovement.SetLookYaw(desiredYaw);
        }
        else
        {
            yaw = Mathf.Clamp(yaw + deltaYaw, -maxYaw, maxYaw);
            possessedMovement.SetLookYaw(bodyYaw);   // si empieza la acción, el cuerpo parte mirando hacia donde ya miraba
        }
        wasControlled = controlled;

        float cameraYaw = bodyYaw + yaw;
        Vector3 position = EyePosition() + Quaternion.Euler(0f, cameraYaw, 0f) * possessionCameraOffset;
        possessionCamTransform.SetPositionAndRotation(position, Quaternion.Euler(pitch, cameraYaw, 0f));
    }

    // Sin RolyPoly: la cámara sale de la orientación del Camera First Person Point (+ Eye Rotation Offset).
    // Con el cuerpo libre gira por su cuenta en el mundo y el cuerpo la sigue (MusicianMovement.Update).
    private void UpdateFromEyePoint(float deltaYaw)
    {
        bool bodyIsFree = possessedMovement != null && possessedMovement.IsBodyDriven;

        if (bodyIsFree && !lookingFree)
        {
            // Acaba de levantarse: seguimos mirando exactamente hacia donde mirábamos sentados (sin saltos)
            worldYaw = Heading(possessionCamTransform);
            followedBody = null;   // se vuelve a calcular dónde están los ojos respecto al cuerpo físico
        }
        else if (!bodyIsFree && lookingFree)
        {
            // Vuelve a "estar sentado": la cabeza queda girada lo justo para que la vista no pegue un salto
            float headHeading = Heading(eyePoint.rotation * Quaternion.Euler(eyeRotationOffset) * Vector3.forward, worldYaw);
            yaw = Mathf.Clamp(Mathf.DeltaAngle(headHeading, worldYaw), -maxYaw, maxYaw);
        }
        lookingFree = bodyIsFree;

        if (bodyIsFree)
        {
            // El ratón gira el cuerpo entero
            worldYaw += deltaYaw;
            yaw = 0f;
        }
        else
        {
            // Sentado tocando: solo puedes girar la cabeza un poco
            yaw = Mathf.Clamp(yaw + deltaYaw, -maxYaw, maxYaw);
        }

        if (bodyIsFree && CameraIsEyes)
        {
            Vector3 position = EyePosition() + Quaternion.Euler(0f, worldYaw, 0f) * possessionCameraOffset;
            possessionCamTransform.SetPositionAndRotation(position, Quaternion.Euler(pitch, worldYaw, 0f));
        }
        else
        {
            ApplyEyeOffset();
            possessionCamTransform.localRotation = Quaternion.Euler(eyeRotationOffset) * Quaternion.Euler(pitch, yaw, 0f);
        }

        PublishLookYaw();
    }

    // En modo "ojos del músico" la cámara cuelga del Camera First Person Point y se saca un poco hacia delante
    // para no quedarse dentro de la cabeza. Con Follow Musician (Cinemachine) manda el Follow Offset de la vcam, no esto.
    private void ApplyEyeOffset()
    {
        if (CameraIsEyes) possessionCamTransform.localPosition = Quaternion.Euler(eyeRotationOffset) * possessionCameraOffset;
    }

    // Dónde están los ojos con el cuerpo libre. Con físicas, el Camera First Person Point puede NO colgar del cuerpo
    // que se mueve (y entonces se quedaría en el asiento), así que se recuerda dónde estaban los ojos respecto al
    // Rigidbody al empezar y se siguen como un punto fijo de ese cuerpo.
    private Vector3 EyePosition()
    {
        Transform body = possessedMovement != null ? possessedMovement.PhysicsBody : null;
        if (body == null)
        {
            followedBody = null;
            return eyePoint.position;
        }

        if (body != followedBody)
        {
            followedBody = body;
            eyeInBody = body.InverseTransformPoint(eyePoint.position);
        }
        return body.TransformPoint(eyeInBody);
    }

    // false solo con Cinemachine + Follow Musician: ahí la vcam se coloca ella sola (Follow / Hard Look At)
    private bool CameraIsEyes => !(possessionVcam != null && followMusician);

    // Le dice al músico hacia dónde mira el jugador (grados en el mundo): MusicianMovement lo guarda y la
    // acción Mover lo usa para girar el cuerpo y para interpretar WASD.
    private void PublishLookYaw()
    {
        if (possessedMovement != null)
            possessedMovement.SetLookYaw(lookingFree ? worldYaw : Heading(possessionCamTransform));
    }

    // Hacia dónde mira un transform, en grados sobre el plano horizontal (0 = eje Z del mundo)
    private static float Heading(Transform t)
    {
        return Heading(t.forward, t.eulerAngles.y);
    }

    private static float Heading(Vector3 forward, float fallback)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return fallback;

        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
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

    // ───────────────────────── Preparación del clon ─────────────────────────

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
                Debug.LogError("PossessionCamera: la plantilla de cámara no tiene ninguna Camera ni Cinemachine Camera.", this);
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
}