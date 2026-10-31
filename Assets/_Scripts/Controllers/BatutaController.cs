
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatutaController : MonoBehaviour
{
    [Header("Referencias")]
    public SimplePlayerController playerController;
    public FirstPersonCameraController cameraController;
    public GameObject modeloBatuta;
    public Transform mano;

    [Header("Limitaciones jugador")]
    public float velocidadCaminarDirector = 1.5f;

    [Tooltip("Multiplicador de la sensibilidad original de la camara.")]
    [Range(0f, 1f)]
    public float velocidadRotacionDirector = 0.2f;

    [Tooltip("Multiplicador al mantener pulsado el boton de dirigir.")]
    [Range(0f, 1f)]
    public float multiplicadorCamaraAlDirigir = 0.08f;

    [Header("Movimiento de la batuta")]
    [Tooltip("Cuanto se desplaza la mano por pixel de raton.")]
    [Min(0.0001f)]
    public float sensibilidadRaton = 0.0035f;

    [Tooltip("Limite a izquierda y derecha respecto al reposo.")]
    [Min(0f)]
    public float limiteHorizontal = 0.6f;

    [Tooltip("Limite arriba y abajo respecto al reposo.")]
    [Min(0f)]
    public float limiteVertical = 0.6f;

    [Header("Retorno al reposo")]
    [Tooltip("Velocidad de retorno al soltar el boton.")]
    [Min(0f)]
    public float velocidadRetornoMano = 12f;

    [Header("Efecto de muñeca (solo visual)")]
    [Tooltip("Grados de inclinacion por unidad de delta del raton.")]
    [SerializeField] private float sensibilidadInclinacion = 0.15f;

    [Range(0f, 30f)]
    [SerializeField] private float inclinacionMaxima = 6f;

    [Tooltip("Fuerza con la que la muñeca sigue el movimiento.")]
    [SerializeField] private float rigidezMuneca = 100f;

    [Tooltip("Amortiguacion del balanceo. Menor valor = mas rebote.")]
    [SerializeField] private float amortiguacionMuneca = 15f;

    private Vector2 inclinacionActual;
    private Vector2 velocidadInclinacion;
    // Estado
    private InputSystem_Actions inputActions;

    public bool modoDirectorActivo { get; private set; }
    private bool botonPulsado;
    public bool BotonPulsado => botonPulsado;

    // Posiciones y rotaciones
    private Vector3 posicionInicialMano;
    private Vector3 posicionObjetivoMano;
    private Quaternion rotacionReposoBatuta;

    // Valores originales
    private float originalMoveSpeed;
    private float sensibilidadCamaraOriginal;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();

        inputActions.Player.SacarBatuta.performed +=
            ctx => ActivarModoDirector();

        inputActions.Dirigir.GuardarBatuta.performed +=
            ctx => DesactivarModoDirector();

        inputActions.Dirigir.BotonBatuta.performed += ctx =>
        {
            botonPulsado = true;

            // Evita saltos si pulsamos mientras la mano
            // todavia esta volviendo a su posicion.
            if (mano != null)
                posicionObjetivoMano = mano.localPosition;

            ActualizarSensibilidadCamara();
        };

        inputActions.Dirigir.BotonBatuta.canceled += ctx =>
        {
            botonPulsado = false;
            posicionObjetivoMano = posicionInicialMano;

            ActualizarSensibilidadCamara();
        };
    }

    private void Start()
    {
        if (playerController != null)
            originalMoveSpeed = playerController.Speed;

        if (cameraController != null)
            sensibilidadCamaraOriginal = cameraController.RotationSpeed;

        if (mano != null)
        {
            posicionInicialMano = mano.localPosition;
            posicionObjetivoMano = posicionInicialMano;
        }

        if (modeloBatuta != null)
        {
            rotacionReposoBatuta = modeloBatuta.transform.localRotation;
            modeloBatuta.SetActive(false);
        }
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Dirigir.Disable();
    }

    private void OnDisable()
    {
        if (modoDirectorActivo)
            DesactivarModoDirector();

        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        if (!modoDirectorActivo ||
            mano == null ||
            modeloBatuta == null ||
            Time.timeScale == 0f)
            return;

        Vector3 posicionAnterior = mano.localPosition;

        Vector2 deltaMuneca = Vector2.zero;

        if (botonPulsado)
        {
            // Leer el delta real de este frame.
            Vector2 delta =
                inputActions.Dirigir.MoverBatuta.ReadValue<Vector2>();
            deltaMuneca = delta;

            // Aplicar movimiento independiente por eje.
            posicionObjetivoMano.x += delta.x * sensibilidadRaton;
            posicionObjetivoMano.y += delta.y * sensibilidadRaton;

            // Limite rectangular: X e Y son independientes.
            posicionObjetivoMano.x = Mathf.Clamp(
                posicionObjetivoMano.x,
                posicionInicialMano.x - limiteHorizontal,
                posicionInicialMano.x + limiteHorizontal
            );

            posicionObjetivoMano.y = Mathf.Clamp(
                posicionObjetivoMano.y,
                posicionInicialMano.y - limiteVertical,
                posicionInicialMano.y + limiteVertical
            );

            // Sin SmoothDamp mientras dirigimos.
            // La mano responde directamente al raton.
            mano.localPosition = posicionObjetivoMano;
        }
        else
        {
            // Retorno suave, sin inercia acumulada.
            float factor = 1f - Mathf.Exp(
                -velocidadRetornoMano * Time.deltaTime
            );

            mano.localPosition = Vector3.Lerp(
                mano.localPosition,
                posicionInicialMano,
                factor
            );

            // Evitar pequeñas diferencias permanentes.
            if ((mano.localPosition - posicionInicialMano).sqrMagnitude
                < 0.000001f)
            {
                mano.localPosition = posicionInicialMano;
            }
        }

        // Inclinacion objetivo segun el movimiento del raton.
        Vector2 inclinacionObjetivo = new Vector2(
            Mathf.Clamp(
                deltaMuneca.y * sensibilidadInclinacion,
                -inclinacionMaxima,
                inclinacionMaxima
            ),
            Mathf.Clamp(
                -deltaMuneca.x * sensibilidadInclinacion,
                -inclinacionMaxima,
                inclinacionMaxima
            )
        );

        // Simulacion de un muelle amortiguado.
        // Solo afecta a la rotacion visual de la batuta.
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

        Vector2 aceleracion =
            (inclinacionObjetivo - inclinacionActual) * rigidezMuneca
            - velocidadInclinacion * amortiguacionMuneca;

        velocidadInclinacion += aceleracion * dt;
        inclinacionActual += velocidadInclinacion * dt;

        // Aplicamos una pequeña inclinacion lateral (roll)
        // ademas del movimiento vertical y horizontal.
        Quaternion rotacionObjetivo =
            rotacionReposoBatuta *
            Quaternion.Euler(
                inclinacionActual.x,
                inclinacionActual.y * 0.5f,
                inclinacionActual.y
            );

        modeloBatuta.transform.localRotation = rotacionObjetivo;
    }

    private void ActualizarSensibilidadCamara()
    {
        if (cameraController == null)
            return;

        if (!modoDirectorActivo)
        {
            cameraController.RotationSpeed =
                sensibilidadCamaraOriginal;
            return;
        }

        float multiplicador = botonPulsado
            ? multiplicadorCamaraAlDirigir
            : velocidadRotacionDirector;

        cameraController.RotationSpeed =
            sensibilidadCamaraOriginal * multiplicador;
    }

    private void ActivarModoDirector()
    {
        if (modoDirectorActivo)
            return;

        // Comprobar ANTES de modificar el estado.
        if (GameManager.Instance != null &&
            !GameManager.Instance.PuedeSacarBatuta)
            return;

        modoDirectorActivo = true;
        botonPulsado = false;

        inputActions.Player.Disable();
        inputActions.Dirigir.Enable();

        if (modeloBatuta != null)
            modeloBatuta.SetActive(true);

        if (playerController != null)
            playerController.Speed = velocidadCaminarDirector;

        if (mano != null)
            posicionObjetivoMano = mano.localPosition;

        ActualizarSensibilidadCamara();
    }

    public void DesactivarModoDirector()
    {
        if (!modoDirectorActivo)
            return;

        modoDirectorActivo = false;
        botonPulsado = false;

        inputActions.Dirigir.Disable();
        inputActions.Player.Enable();

        // Restaurar mano
        if (mano != null)
        {
            mano.localPosition = posicionInicialMano;
            posicionObjetivoMano = posicionInicialMano;
        }

        // Restaurar batuta
        if (modeloBatuta != null)
        {
            modeloBatuta.transform.localRotation = rotacionReposoBatuta;
            modeloBatuta.SetActive(false);
        }
        inclinacionActual = Vector2.zero;
        velocidadInclinacion = Vector2.zero;

        // Restaurar movimiento
        if (playerController != null)
            playerController.Speed = originalMoveSpeed;

        // Restaurar sensibilidad
        ActualizarSensibilidadCamara();
    }
}
