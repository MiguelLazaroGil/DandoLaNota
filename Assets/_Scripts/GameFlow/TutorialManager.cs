
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum PasoTutorial
    {
        Movimiento,
        SacarBatuta,
        MoverBatuta,
        SeguirMetronomo,
        CorregirMusicos,
        EsperandoSiguienteOrden,
        TutorialCompletado
    }

    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private TMP_Text textoInstruccion;
    [SerializeField] private GameObject panelInstrucciones;
    [SerializeField] private TMP_Text textoPanel;
    [SerializeField] private Button botonSiguiente;
    [SerializeField] private MusicScoreManager musicScoreManager;

    [Header("Configuración tutorial")]
    [SerializeField, Min(0f)] private float tiempoEntrePasos = 2f;

    [Header("Movimiento")]
    [SerializeField, Min(0.0001f)]
    private float distanciaMinimaMovimiento = 0.001f;

    public PasoTutorial PasoActual { get; private set; }

    // Permite controlar cuando se puede sacar la batuta.

    public bool PuedeSacarBatuta =>
        tutorialIniciado &&
        (PasoActual == PasoTutorial.SacarBatuta ||
         PasoActual == PasoTutorial.MoverBatuta ||
         PasoActual == PasoTutorial.SeguirMetronomo ||
         PasoActual == PasoTutorial.CorregirMusicos ||
         PasoActual == PasoTutorial.TutorialCompletado);


    private bool tutorialIniciado;
    private bool transicionEnCurso;

    private PasoTutorial pasoPendiente;

    private bool wCompletada;
    private bool aCompletada;
    private bool sCompletada;
    private bool dCompletada;
    private bool espacioCompletada;
    private bool fCompletada;

    private Vector3 posicionAnterior;

    //PArametros Batuta
    public enum DireccionBatuta
    {
        Arriba,
        Abajo,
        Izquierda,
        Derecha
    }

    [Header("Tutorial Batuta")]
    [SerializeField] private BatutaController batutaController;
    [SerializeField] private Transform manoBatuta;
    [SerializeField] private GameObject imagenSecuenciaBatuta;

    [SerializeField]
    private DireccionBatuta[] secuenciaBatuta =
    {
    DireccionBatuta.Abajo,
    DireccionBatuta.Izquierda,
    DireccionBatuta.Derecha,
    DireccionBatuta.Arriba
};

    [SerializeField, Min(0.001f)]
    private float distanciaMinimaBatuta = 0.03f;

    [SerializeField, Range(0.5f, 1f)]
    private float precisionDireccionBatuta = 0.75f;

    private int indiceMovimientoBatuta;
    private Vector2 posicionInicioBatuta;
    private bool registrandoMovimientoBatuta;

    [Header("Tutorial Metrónomo")]
    [SerializeField, Range(0f, 1f)] private float calidadMinima = 0.6f;
    [SerializeField, Min(0.1f)] private float segundosCalidadMantenida = 4f;

    private float tiempoCalidadCorrecta;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void IniciarTutorial()
    {
        if (jugador == null || textoInstruccion == null ||
            panelInstrucciones == null || botonSiguiente == null || textoPanel == null)
        {
            Debug.LogError("TutorialManager: Faltan referencias.");
            return;
        }

        wCompletada = false;
        aCompletada = false;
        sCompletada = false;
        dCompletada = false;
        espacioCompletada = false;
        fCompletada = false;
        tiempoCalidadCorrecta = 0f;

        posicionAnterior = jugador.position;

        tutorialIniciado = true;
        transicionEnCurso = false;

        // Preparamos el primer paso, pero no lo activamos
        // hasta que el jugador pulse SIGUIENTE.
        pasoPendiente = PasoTutorial.Movimiento;
        PasoActual = PasoTutorial.EsperandoSiguienteOrden;

        ActualizarInstruccionMovimiento();
        textoPanel.text = "BIENVENIDO AL TUTORIAL\n\n" +
            "Para comenzar, muévete por el escenario con " +
            "[W][A][S][D] y salta pulsando [ESPACIO].";

        panelInstrucciones.SetActive(true);
        imagenSecuenciaBatuta.SetActive(false);
        botonSiguiente.interactable = true;

        GameManager.Instance.PauseGame();
    }

    private void LateUpdate()
    {
        if (!tutorialIniciado)
            return;

        if (PasoActual == PasoTutorial.Movimiento)
            ComprobarMovimiento();

        if (PasoActual == PasoTutorial.SacarBatuta)
            ComprobarSacarBatuta();

        if (PasoActual == PasoTutorial.MoverBatuta)
            ComprobarMoverBatuta();

        if (PasoActual == PasoTutorial.SeguirMetronomo)
            ComprobarSeguirMetronomo();
    }


    private void ComprobarMoverBatuta()
    {
        if (batutaController == null || manoBatuta == null ||
            secuenciaBatuta == null || secuenciaBatuta.Length == 0)
            return;

        // Solo comprobamos cuando el jugador mantiene pulsado
        // el boton que controla el movimiento de la batuta.
        if (!batutaController.BotonPulsado)
        {
            registrandoMovimientoBatuta = false;
            return;
        }

        // Trabajamos con la posicion local de la mano,
        // que es la que modifica BatutaController.
        Vector2 posicionActual = new Vector2(
            manoBatuta.localPosition.x,
            manoBatuta.localPosition.y
        );

        // Registramos donde comienza el movimiento.
        if (!registrandoMovimientoBatuta)
        {
            posicionInicioBatuta = posicionActual;
            registrandoMovimientoBatuta = true;
            return;
        }

        Vector2 desplazamiento = posicionActual - posicionInicioBatuta;

        // Comprobamos que se haya desplazado suficiente.
        if (desplazamiento.sqrMagnitude <
            distanciaMinimaBatuta * distanciaMinimaBatuta)
            return;

        // Obtenemos la direccion que toca realizar.
        Vector2 direccionEsperada =
            ObtenerVectorDireccion(secuenciaBatuta[indiceMovimientoBatuta]);

        // Comprobamos si el movimiento coincide con la direccion.
        float precision = Vector2.Dot(
            desplazamiento.normalized,
            direccionEsperada
        );

        if (precision >= precisionDireccionBatuta)
        {
            indiceMovimientoBatuta++;

            // El siguiente gesto comienza desde esta posicion.
            posicionInicioBatuta = posicionActual;

            ActualizarInstruccionMoverBatuta();

            if (indiceMovimientoBatuta >= secuenciaBatuta.Length)
            {
                CompletarMoverBatuta();
            }
        }
        else
        {
            // Si el movimiento es incorrecto, no avanzamos.
            // Reiniciamos la referencia para intentar el mismo gesto.
            posicionInicioBatuta = posicionActual;
        }
    }

    private Vector2 ObtenerVectorDireccion(DireccionBatuta direccion)
    {
        switch (direccion)
        {
            case DireccionBatuta.Arriba:
                return Vector2.up;

            case DireccionBatuta.Abajo:
                return Vector2.down;

            case DireccionBatuta.Izquierda:
                return Vector2.left;

            case DireccionBatuta.Derecha:
                return Vector2.right;

            default:
                return Vector2.zero;
        }
    }

    private void ComprobarSeguirMetronomo()
    {
        if (musicScoreManager == null)
            return;

        // Utilizamos la calidad del director, que evalúa su seguimiento del ritmo.
        float calidad = musicScoreManager.CurrentDirectorQuality;
        // Los cuatro segundos tienen que ser consecutivos.
        // La calidad ya la calcula MusicScoreManager a partir del ritmo.
        if (calidad > calidadMinima)
            tiempoCalidadCorrecta += Time.deltaTime;
        else
            tiempoCalidadCorrecta = 0f;

        if (tiempoCalidadCorrecta >= segundosCalidadMantenida)
            CompletarSeguirMetronomo();
    }

    private void ComprobarSacarBatuta()
    {
        if (Keyboard.current == null)
            return;

        Keyboard teclado = Keyboard.current;



        if (!fCompletada && teclado.fKey.wasPressedThisFrame)
        {
            fCompletada = true;
            ActualizarInstruccionSacarBatuta();
            CompletarSacarBatuta();
        }
    }

    private void ComprobarMovimiento()
    {
        Vector3 posicionActual = jugador.position;
        Vector3 desplazamiento = posicionActual - posicionAnterior;

        // Solo comprobamos el desplazamiento horizontal.
        desplazamiento.y = 0f;

        posicionAnterior = posicionActual;

        bool seHaMovido = desplazamiento.sqrMagnitude >
            distanciaMinimaMovimiento * distanciaMinimaMovimiento;

        if (Keyboard.current == null)
            return;

        Keyboard teclado = Keyboard.current;
        bool progreso = false;

        // Las teclas WASD solo cuentan si el jugador se desplaza.
        if (seHaMovido)
        {
            if (!wCompletada && teclado.wKey.isPressed)
            {
                wCompletada = true;
                progreso = true;
            }

            if (!aCompletada && teclado.aKey.isPressed)
            {
                aCompletada = true;
                progreso = true;
            }

            if (!sCompletada && teclado.sKey.isPressed)
            {
                sCompletada = true;
                progreso = true;
            }

            if (!dCompletada && teclado.dKey.isPressed)
            {
                dCompletada = true;
                progreso = true;
            }
        }

        // El salto se comprueba por separado: no exige moverse en horizontal.
        if (!espacioCompletada && teclado.spaceKey.wasPressedThisFrame)
        {
            espacioCompletada = true;
            progreso = true;
        }

        if (!progreso)
            return;

        ActualizarInstruccionMovimiento();

        if (wCompletada && aCompletada &&
            sCompletada && dCompletada && espacioCompletada)
        {
            CompletarMovimiento();
        }
    }

    // Actualización de instrucciones en pantalla
    private void ActualizarInstruccionMovimiento()
    {
        textoInstruccion.text =
            "Muévete con WASD y salta con ESPACIO\n\n" +
            MostrarTecla("W", wCompletada) + "  " +
            MostrarTecla("A", aCompletada) + "  " +
            MostrarTecla("S", sCompletada) + "  " +
            MostrarTecla("D", dCompletada) + "\n" +
            MostrarTecla("ESPACIO", espacioCompletada);
    }

    private void ActualizarInstruccionSacarBatuta()
    {
        textoInstruccion.text =
            "Saca la batuta utilizando la tecla F:\n\n" +
            MostrarTecla("F", fCompletada);
    }


    private void ActualizarInstruccionMoverBatuta()
    {
        textoInstruccion.text =
            "Mantén clic izquierdo y mueve la batuta:\n\n";

        for (int i = 0; i < secuenciaBatuta.Length; i++)
        {
            textoInstruccion.text += MostrarTecla(
                secuenciaBatuta[i].ToString().ToUpperInvariant(),
                i < indiceMovimientoBatuta
            );

            if (i < secuenciaBatuta.Length - 1)
                textoInstruccion.text += " → ";
        }

        if (indiceMovimientoBatuta < secuenciaBatuta.Length)
        {
            textoInstruccion.text += "\n\nAhora: " +
                secuenciaBatuta[indiceMovimientoBatuta].ToString().ToUpperInvariant();
        }
    }


    private void ActualizarInstruccionSeguirMetronomo()
    {
        textoInstruccion.text =
            "Sigue los pulsos del metrónomo\n" +
            "y recupera el ritmo de la canción.";
    }

    private void ActualizarInstruccionCorregirMusicos()
    {
        textoInstruccion.text = "Presta atención a los músicos y corrígelos cuando sea necesario.";
    }

    private string MostrarTecla(string tecla, bool completada)
    {
        if (completada)
            return "<color=#55FF88>[" + tecla + " OK]</color>";

        return "[" + tecla + "]";
    }

    // Comprobaciones de finalización de pasos del tutorial
    private void CompletarMovimiento()
    {
        StartCoroutine(MostrarResultadoYPrepararSiguiente(
            "¡Muy bien! Has completado el movimiento.",
            PasoTutorial.SacarBatuta
        ));
    }

    private void CompletarSacarBatuta()
    {
        StartCoroutine(MostrarResultadoYPrepararSiguiente(
            "¡Muy bien! Has sacado la batuta.",
            PasoTutorial.MoverBatuta
        ));
    }

    private void CompletarMoverBatuta()
    {
        StartCoroutine(MostrarResultadoYPrepararSiguiente(
            "¡Perfecto! Has completado la secuencia de movimientos.",
            PasoTutorial.SeguirMetronomo
        ));
    }

    private void CompletarSeguirMetronomo()
    {
        StartCoroutine(MostrarResultadoYPrepararSiguiente(
            "¡Muy bien! Has conseguido mantener el ritmo.",
            PasoTutorial.CorregirMusicos
        ));
    }

    // Se llamará desde la futura mecánica cuando se cumpla su objetivo.
    public void NotificarCorreccionMusicosCompletada()
    {
        if (!tutorialIniciado || transicionEnCurso ||
            PasoActual != PasoTutorial.CorregirMusicos)
            return;

        StartCoroutine(MostrarResultadoYPrepararSiguiente(
            "¡Perfecto! Has corregido a los músicos.",
            PasoTutorial.TutorialCompletado
        ));
    }

    // Útil para comprobar el final sin tener implementada la mecánica.
    [ContextMenu("DEBUG/Completar corrección de músicos")]
    private void DebugCompletarCorreccionMusicos()
    {
        if (Application.isPlaying)
            NotificarCorreccionMusicosCompletada();
    }

    // Muestra el mensaje de éxito durante unos segundos
    // antes de enseñar las instrucciones del siguiente paso.
    private IEnumerator MostrarResultadoYPrepararSiguiente(
        string mensaje, PasoTutorial siguientePaso)
    {
        transicionEnCurso = true;

        pasoPendiente = siguientePaso;
        PasoActual = PasoTutorial.EsperandoSiguienteOrden;

        textoInstruccion.text = mensaje;

        // Realtime permite que funcione con Time.timeScale = 0.
        yield return new WaitForSecondsRealtime(tiempoEntrePasos);

        GameManager.Instance.PauseGame();
        panelInstrucciones.SetActive(true);
        imagenSecuenciaBatuta.SetActive(pasoPendiente == PasoTutorial.MoverBatuta|| pasoPendiente == PasoTutorial.SeguirMetronomo);

        // Preparamos las instrucciones del siguiente paso.
        switch (pasoPendiente)
        {
            case PasoTutorial.Movimiento:
                textoPanel.text = "BIENVENIDO AL TUTORIAL\n\n" +
                "Para comenzar,\n" +
                "Prueba a moverte por el escenario\n" +
                "mediante las teclas [W][A][S][D]\n" +
                "y salta con [ESPACIO].";
                ActualizarInstruccionMovimiento();
                break;

            case PasoTutorial.SacarBatuta:
                textoPanel.text = "¡Bien hecho!\n\n" +
                "Prueba a sacar la batuta con la tecla [F].";
                ActualizarInstruccionSacarBatuta();
                break;

            case PasoTutorial.MoverBatuta:
                textoPanel.text = "¡Bien hecho!\n\n" +
                "Prueba a mover la batuta manteniendo\n" +
                "El click izquierdo del ratón y moviendo el ratón.\n" +
                "Sigue la secuencia de movimientos que aparece en el diagrama.";
                ActualizarInstruccionMoverBatuta();
                break;

            case PasoTutorial.SeguirMetronomo:
                textoPanel.text = "¡Bien hecho! Ahora comienza la música.\n\n" +
                    "Sigue los pulsos del metrónomo como antes con la batuta para\n" +
                    "recuperar el ritmo de la canción";
                ActualizarInstruccionSeguirMetronomo();
                break;

            case PasoTutorial.CorregirMusicos:
                textoPanel.text = "¡Buen trabajo! Ya sabes seguir el ritmo.\n\n" +
                    "Ahora tendrás que estar atento a los músicos. " +
                    "Cuando alguno empiece a molestar, deberás corregirlo.";
                ActualizarInstruccionCorregirMusicos();
                break;

            case PasoTutorial.TutorialCompletado:
                textoPanel.text = "¡Enhorabuena! Has terminado el tutorial.\n\n" +
                    "Ya sabes moverte, manejar la batuta, seguir el metrónomo " +
                    "y corregir a los músicos.";
                break;

        }

        transicionEnCurso = false;
    }

    // Se llama desde el botón SIGUIENTE del Canvas.
    public void Siguiente()
    {
        if (!tutorialIniciado || transicionEnCurso ||
            PasoActual != PasoTutorial.EsperandoSiguienteOrden)
            return;

        CambiarPasoTutorial(pasoPendiente);

        panelInstrucciones.SetActive(false);
        imagenSecuenciaBatuta.SetActive(false);

        GameManager.Instance.UnpauseGame();

        // La música arranca una sola vez, al cerrar el panel de este paso.
        if (PasoActual == PasoTutorial.SeguirMetronomo)
            GameManager.Instance.IniciarMusica();
    }

    public void CambiarPasoTutorial(PasoTutorial nuevoPaso)
    {
        PasoActual = nuevoPaso;

        switch (nuevoPaso)
        {
            case PasoTutorial.Movimiento:
                posicionAnterior = jugador.position;
                ActualizarInstruccionMovimiento();
                break;

            case PasoTutorial.SacarBatuta:
                ActualizarInstruccionSacarBatuta();
                break;

            case PasoTutorial.MoverBatuta:
                indiceMovimientoBatuta = 0;
                registrandoMovimientoBatuta = false;
                ActualizarInstruccionMoverBatuta();
                break;

            case PasoTutorial.SeguirMetronomo:
                tiempoCalidadCorrecta = 0f;
                ActualizarInstruccionSeguirMetronomo();
                break;

            case PasoTutorial.CorregirMusicos:
                ActualizarInstruccionCorregirMusicos();
                break;

            case PasoTutorial.EsperandoSiguienteOrden:
                panelInstrucciones.SetActive(true);
                GameManager.Instance.PauseGame();
                break;

            case PasoTutorial.TutorialCompletado:
                textoInstruccion.text = "";
                break;

        }
    }
}
