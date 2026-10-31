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

        AttachPossessionCamera(musician);

        possessionCamTransform.localPosition = Vector3.zero;
        possessionCamTransform.localRotation = Quaternion.identity;
        yaw = 0f;
        pitch = 0f;
        SetActiveCamera(true);
    }

    // Devolvemos la vista a la cámara del fantasma y guardamos la del músico en el fantasma
    public void EndPossession()
    {
        SetActiveCamera(false);
        possessionCamTransform.SetParent(transform, false);
    }

    // lookInput = delta del ratón sin escalar (la sensibilidad se aplica aquí)
    public void UpdateLook(Vector2 lookInput, MusicianMovement movement)
    {
        Vector2 delta = lookInput * lookSensitivity;
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