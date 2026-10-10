using StarterAssets;
using System.Reflection; //permite cambiar clases en tiemmpo de ejecucion(cambiar FirstPersonController)
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.ProBuilder.Shapes;

public class BatutaController : MonoBehaviour
{
    [Header("Referencias")]
    public SimplePlayerController playerController;
    public GameObject modeloBatuta;
    public Transform mano;
    [Tooltip("Arrastra aqu� el PlayerCameraRoot o CuelloBatuta")]
    public Transform camaraPrincipal;

    [Header("Limitaciones Jugador (Modo Director)")]
    [Tooltip("Velocidad de movimiento al usar la batuta.")]
    public float velocidadCaminarDirector = 1.5f;
    [Tooltip("Velocidad de giro de c�mara al usar la batuta.")]
    public float velocidadRotacionDirector = 0.2f;

    [Header("C�mara (Modo Director)")]
    public float sensibilidadCamara = 5f;
    public float suavizadoCamara = 5f;

    [Header("Configuraci�n de Fluidez (Brazo)")]
    public float sensibilidadRaton = 0.005f;
    public float limiteDistancia = 0.6f;
    [Range(0.01f, 0.5f)] public float tiempoSuavizado = 0.1f;

    [Header("Efecto de Mu�eca (Batuta)")]
    public float multiplicadorInclinacion = 15f;
    public float velocidadRecuperacionMuneca = 30f;

    //Variables de estado
    private InputSystem_Actions inputActions;
    public bool modoDirectorActivo { get; private set; } = false;
    private Vector2 movimientoRaton;
    private bool botonPulsado = false;

    //mates
    private Vector3 posicionInicialMano;
    private Vector3 posicionObjetivoMano; 
    private Vector3 velocidadActualMano; //Variable requerida por SmoothDamp. Unity la usa internamente para calcular la inercia frame a frame.
    private Quaternion rotacionReposoBatuta;
    private Quaternion rotacionInicialCamara;

    //Guardamos las velocidades originales del jugador aqu� para poder devolv�rselas al guardar la batuta.
    private float originalMoveSpeed;
    // private float originalSprintSpeed;
    // private float originalRotationSpeed;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();

        //Suscripci�n a eventos del Input System
        inputActions.Player.SacarBatuta.performed += ctx => ActivarModoDirector();
        inputActions.Dirigir.GuardarBatuta.performed += ctx => DesactivarModoDirector();

        inputActions.Dirigir.BotonBatuta.performed += ctx => botonPulsado = true;
        inputActions.Dirigir.BotonBatuta.canceled += ctx => botonPulsado = false;

        inputActions.Dirigir.MoverBatuta.performed += ctx => movimientoRaton = ctx.ReadValue<Vector2>();
        inputActions.Dirigir.MoverBatuta.canceled += ctx => movimientoRaton = Vector2.zero;
    }

    private void Start()
    {
        if (playerController != null)
        {
            originalMoveSpeed = playerController.Speed;
            // originalSprintSpeed = playerController.SprintSpeed;
            // originalRotationSpeed = playerController.RotationSpeed;
        }

        //Guardamos d�nde est�n los objetos para saber a d�nde deben volver cuando soltemos el rat�n.
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
        inputActions.Disable();
    }

    private void Update()
    {
        //Si no estamos dirigiendo o faltan referencias, cortamos la ejecuci�n aqu� por rendimiento
        if (!modoDirectorActivo || mano == null || modeloBatuta == null) return;

        if (botonPulsado)
        {
            Vector3 desplazamiento = new Vector3(movimientoRaton.x, movimientoRaton.y, 0) * sensibilidadRaton;
            posicionObjetivoMano += desplazamiento;

            //Para evitar que la mano salga de la pantalla, calculamos un vector desde el centro hasta la posici�n actual.
            Vector3 distanciaDesdeCentro = posicionObjetivoMano - posicionInicialMano;

            //Si ese vector es m�s largo que el l�mite, lo Clampeo 
            if (distanciaDesdeCentro.magnitude > limiteDistancia)
            {
                posicionObjetivoMano = posicionInicialMano + Vector3.ClampMagnitude(distanciaDesdeCentro, limiteDistancia);
            }
        }
        else
        {
            //Si soltamos el click, vuelve al centro de la pantalla
            posicionObjetivoMano = posicionInicialMano;
        }

        //Suavizamos el movimiento de la mano usando SmoothDamp, que nos da un efecto de inercia.
        mano.localPosition = Vector3.SmoothDamp(
            mano.localPosition,
            posicionObjetivoMano,
            ref velocidadActualMano,
            tiempoSuavizado
        );

        //Aprovechamos la velocidad que calcul� el SmoothDamp arriba para inclinar la mu�eca.
        //Multiplicamos la velocidad por un factor para inclinar la mu�eca m�s o menos seg�n queramos.
        float inclinacionVertical = velocidadActualMano.y * multiplicadorInclinacion;
        float inclinacionHorizontal = -velocidadActualMano.x * multiplicadorInclinacion;

        //Calculamos la rotaci�n objetivo de la batuta combinando la rotaci�n de reposo con la inclinaci�n calculada.
        Quaternion rotacionObjetivo = rotacionReposoBatuta * Quaternion.Euler(inclinacionVertical, inclinacionHorizontal, 0);

        //Slerp suaviza la rotaci�n de la batuta hacia la rotaci�n objetivo, usando un factor de velocidad para controlar la rapidez del movimiento.
        modeloBatuta.transform.localRotation = Quaternion.Slerp(
            modeloBatuta.transform.localRotation,
            rotacionObjetivo,
            Time.deltaTime * velocidadRecuperacionMuneca
        );

    }

    private void ActivarModoDirector()
    {
        modoDirectorActivo = true;

        //Desactivamos el script de movimiento original para que no interfiera con el control de la batuta.
        inputActions.Player.Disable();
        inputActions.Dirigir.Enable();

        if (modeloBatuta != null) modeloBatuta.SetActive(true);

        if (playerController != null)
        {
            playerController.Speed = velocidadCaminarDirector;

            // playerController.RotationSpeed = velocidadRotacionDirector;
            // playerController.SprintSpeed = velocidadCaminarDirector;
        }

        //Guardamos d�nde miraba la c�mara para poder restaurarla al salir del modo director.
        if (camaraPrincipal != null) rotacionInicialCamara = camaraPrincipal.localRotation;
    }

    public void DesactivarModoDirector() //public para que pueda ser llamado desde otros scripts (Interactable o Grabbable)
    {
        modoDirectorActivo = false;

        //devolvemos el control al jugador y desactivamos el control de la batuta.
        inputActions.Dirigir.Disable();
        inputActions.Player.Enable();

        //reseteo visual
        if (mano != null) mano.localPosition = posicionInicialMano;

        if (modeloBatuta != null)
        {
            modeloBatuta.transform.localRotation = rotacionReposoBatuta;
            modeloBatuta.SetActive(false);
        }

        if (camaraPrincipal != null) camaraPrincipal.localRotation = rotacionInicialCamara;

        //le devolvemos al FirstPersonController las velocidades originales que ten�a antes de activar el modo director.
        if (playerController != null)
        {
            playerController.Speed = originalMoveSpeed;
            // playerController.SprintSpeed = originalSprintSpeed;
            // playerController.RotationSpeed = originalRotationSpeed;
        }
    }
}