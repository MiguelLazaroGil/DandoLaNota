
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum PasoTutorial
    {
        Movimiento,
        EsperandoSiguienteOrden
    }

    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private TMP_Text textoInstruccion;

    [Header("Movimiento")]
    [SerializeField, Min(0.0001f)]
    private float distanciaMinimaMovimiento = 0.001f;

    public PasoTutorial PasoActual { get; private set; }

    // Permite controlar cuando se puede sacar la batuta.
    public bool PuedeSacarBatuta =>
        tutorialIniciado && PasoActual != PasoTutorial.Movimiento;

    private bool tutorialIniciado;

    private bool wCompletada;
    private bool aCompletada;
    private bool sCompletada;
    private bool dCompletada;

    private Vector3 posicionAnterior;

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
        if (jugador == null || textoInstruccion == null)
        {
            Debug.LogError("TutorialManager: Faltan referencias.");
            return;
        }

        wCompletada = false;
        aCompletada = false;
        sCompletada = false;
        dCompletada = false;

        posicionAnterior = jugador.position;

        PasoActual = PasoTutorial.Movimiento;
        tutorialIniciado = true;

        ActualizarInstruccionMovimiento();
    }

    private void LateUpdate()
    {
        if (!tutorialIniciado)
            return;

        if (PasoActual == PasoTutorial.Movimiento)
            ComprobarMovimiento();
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

        if (!seHaMovido || Keyboard.current == null)
            return;

        Keyboard teclado = Keyboard.current;

        bool progreso = false;

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

        if (!progreso)
            return;

        ActualizarInstruccionMovimiento();

        if (wCompletada && aCompletada &&
            sCompletada && dCompletada)
        {
            CompletarMovimiento();
        }
    }

    private void ActualizarInstruccionMovimiento()
    {
        textoInstruccion.text =
            "Muévete utilizando WASD\n\n" +
            MostrarTecla("W", wCompletada) + "  " +
            MostrarTecla("A", aCompletada) + "  " +
            MostrarTecla("S", sCompletada) + "  " +
            MostrarTecla("D", dCompletada);
    }

    private string MostrarTecla(string tecla, bool completada)
    {
        if (completada)
            return "<color=#55FF88>[" + tecla + " OK]</color>";

        return "[" + tecla + "]";
    }

    private void CompletarMovimiento()
    {
        PasoActual = PasoTutorial.EsperandoSiguienteOrden;

        textoInstruccion.text =
            "¡Muy bien! Has completado el movimiento.";

        Debug.Log("Tutorial: Movimiento completado.");
    }
}
