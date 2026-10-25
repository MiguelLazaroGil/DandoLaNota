
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using FMODUnity;

[System.Serializable]
public class PistaCancion
{
    public InstrumentGroup grupo;
    public EventReference eventoFMOD;
}

[System.Serializable]
public class CancionInfinita
{
    [Range(2, 5)] public int numeroTema = 2;
    public string nombre;

    [Header("Configuracion musical")]
    [Min(1f)] public float bpm = 100f;
    [Min(0.1f)] public float duracionLoop = 19.2f;
    [Min(1)] public int repeticiones = 1;

    [Header("FMOD")]
    public EventReference eventoReloj;
    public List<PistaCancion> pistas = new();
}

public class InfiniteLevelManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private OrchestraAudioManager orchestra;
    [SerializeField] private MusicClockFMOD musicClock;
    [SerializeField] private RhythmTracker rhythmTracker;
    [SerializeField] private AudienceManager audienceManager;
    [SerializeField] private MusicScoreManager musicScoreManager;

    [Header("Canciones disponibles")]
    [SerializeField] private List<CancionInfinita> canciones = new();

    [Header("Configuracion del nivel")]
    [SerializeField] private bool iniciarAutomaticamente = true;
    [SerializeField] private bool evitarRepetirCancion = true;

    [Header("Transiciones")]
    [SerializeField, Min(0f)]
    private float tiempoSilencioEntreCanciones = 3f;

    private bool reanudarSeguimientoDespuesPausa;

    [Header("Derrota")]
    [SerializeField] private GameObject panelDerrota;

    [Header("Estado en vivo (lectura)")]
    [SerializeField] private int temaActualNumero;
    [SerializeField] private int cancionesCompletadas;
    [SerializeField] private float tiempoCancion;
    [SerializeField] private bool nivelActivo;

    private CancionInfinita cancionActual;
    private bool partidaTerminada;

    public int CancionesCompletadas => cancionesCompletadas;
    public int TemaActualNumero => temaActualNumero;
    public bool NivelActivo => nivelActivo;

    private void Start()
{
    if (GameManager.Instance == null)
    {
        Debug.LogError("InfiniteLevelManager: Falta GameManager.");
        enabled = false;
        return;
    }

    if (GameManager.Instance.ModoActual != GameManager.ModoJuego.Infinito)
    {
        enabled = false;
        return;
    }

    if (panelDerrota != null)
        panelDerrota.SetActive(false);

    if (iniciarAutomaticamente)
        IniciarNivel();
}

    public void IniciarNivel()
    {
        if (nivelActivo || partidaTerminada)
            return;

        if (orchestra == null || musicClock == null ||
            audienceManager == null)
        {
            Debug.LogError("InfiniteLevelManager: faltan referencias.");
            return;
        }

        // Siempre comenzamos por el Tema 4.
        CancionInfinita primeraCancion =
            canciones.Find(c => c != null && c.numeroTema == 4);

        if (primeraCancion == null)
        {
            Debug.LogError("No se ha configurado el Tema 4.");
            return;
        }

        cancionesCompletadas = 0;
        audienceManager.ActivarSimulacion(false);

        if (ReproducirCancion(primeraCancion))
            nivelActivo = true;
    }



    private void Update()
    {
        if (!nivelActivo || Time.timeScale == 0f)
            return;

        // Comprobamos si el jugador ha perdido.
        if (audienceManager.CurrentAudience <= 0f)
        {
            PerderPartida();
            return;
        }

        if (cancionActual == null)
            return;

        // Solo para mostrar el tiempo transcurrido en el Inspector.
        if (musicClock.TryObtenerTiempo(out double tiempo))
            tiempoCancion = (float)tiempo;

        // El evento FMOD no tiene loop.
        // Cuando termina, empezamos la transicion.
        if (musicClock.HaTerminadoEvento())
        {
            StartCoroutine(TransicionEntreCanciones());
        }
    }


    private bool ReproducirCancion(CancionInfinita cancion)
    {
        if (cancion == null || cancion.eventoReloj.IsNull)
        {
            Debug.LogError("La cancion no tiene reloj FMOD.");
            return false;
        }

        audienceManager.ActivarSimulacion(false);

        bool estabaDirigiendo =
    reanudarSeguimientoDespuesPausa ||
    (rhythmTracker != null && rhythmTracker.SeguimientoActivo);

        reanudarSeguimientoDespuesPausa = false;

        if (rhythmTracker != null)
            rhythmTracker.DetenerSeguimiento();

        // Detenemos la cancion anterior.
        orchestra.StopOrchestra();

        // Configuramos los nuevos BPM y la duracion.
        orchestra.ConfigurarCancion(
            cancion.bpm,
            cancion.duracionLoop
        );

        musicClock.ConfigurarEvento(cancion.eventoReloj);

        // Limpiamos los eventos instrumentales anteriores.
        foreach (InstrumentGroup grupo in orchestra.Grupos)
        {
            if (grupo != null)
                grupo.ConfigurarEvento(default(EventReference));
        }

        // Asignamos los eventos de la nueva cancion.
        foreach (PistaCancion pista in cancion.pistas)
        {
            if (pista != null && pista.grupo != null)
                pista.grupo.ConfigurarEvento(pista.eventoFMOD);
        }

        // Arrancamos el nuevo reloj y todos los grupos.
        orchestra.StartOrchestra();

        if (!orchestra.IsPlaying)
        {
            Debug.LogError(
                "No se pudo iniciar el Tema " + cancion.numeroTema
            );
            return false;
        }

        // Recuperamos la direccion con el nuevo BPM.
        if (estabaDirigiendo && rhythmTracker != null)
            rhythmTracker.IniciarSeguimiento();

        cancionActual = cancion;
        temaActualNumero = cancion.numeroTema;
        tiempoCancion = 0f;

        audienceManager.ActivarSimulacion(true);

        Debug.Log(
            "REPRODUCIENDO: " + cancion.nombre +
            " | BPM: " + cancion.bpm
        );

        return true;
    }


    private IEnumerator TransicionEntreCanciones()
    {
        // Evitamos que Update vuelva a lanzar otra transicion.
        nivelActivo = false;

        cancionesCompletadas++;

        // Congelamos la audiencia durante el silencio.
        audienceManager.ActivarSimulacion(false);

        // Guardamos si el jugador estaba siguiendo el ritmo.
        reanudarSeguimientoDespuesPausa =
            rhythmTracker != null && rhythmTracker.SeguimientoActivo;

        // Detenemos temporalmente el seguimiento de la batuta.
        if (rhythmTracker != null)
        {
            rhythmTracker.DetenerSeguimiento();
            rhythmTracker.enabled = false;
        }

        // Congelamos los calculos musicales.
        if (musicScoreManager != null)
            musicScoreManager.enabled = false;

        // Paramos todos los instrumentos y el reloj.
        orchestra.StopOrchestra();

        Debug.Log("Cancion terminada. Esperando 3 segundos.");

        // Tres segundos de silencio.
        // El tiempo tambien se detiene si se pausa el juego.
        yield return new WaitForSeconds(tiempoSilencioEntreCanciones);

        if (partidaTerminada)
            yield break;

        // Reactivamos los calculos.
        if (musicScoreManager != null)
            musicScoreManager.enabled = true;

        if (rhythmTracker != null)
            rhythmTracker.enabled = true;

        // Elegimos aleatoriamente el siguiente tema.
        CancionInfinita siguiente = ElegirCancionAleatoria();

        if (siguiente == null || !ReproducirCancion(siguiente))
        {
            Debug.LogError("No se pudo iniciar la siguiente cancion.");
            audienceManager.ActivarSimulacion(false);
            orchestra.StopOrchestra();
            yield break;
        }

        // Reanudamos el nivel.
        nivelActivo = true;
    }


    private CancionInfinita ElegirCancionAleatoria()
    {
        List<CancionInfinita> disponibles = new();

        foreach (CancionInfinita cancion in canciones)
        {
            if (cancion == null)
                continue;

            // Tema 1 excluido. Solo Tema 2, 3, 4 y 5.
            if (cancion.numeroTema < 2 || cancion.numeroTema > 5)
                continue;

            if (evitarRepetirCancion && cancionActual != null &&
                cancion.numeroTema == cancionActual.numeroTema)
                continue;

            disponibles.Add(cancion);
        }

        // Si solamente tenemos un tema configurado,
        // permitimos que se repita.
        if (disponibles.Count == 0)
        {
            foreach (CancionInfinita cancion in canciones)
            {
                if (cancion != null &&
                    cancion.numeroTema >= 2 &&
                    cancion.numeroTema <= 5)
                {
                    disponibles.Add(cancion);
                }
            }
        }

        if (disponibles.Count == 0)
            return null;

        int indice = Random.Range(0, disponibles.Count);
        return disponibles[indice];
    }

    public void PerderPartida()
    {
        if (partidaTerminada)
            return;

        partidaTerminada = true;
        nivelActivo = false;

        audienceManager.ActivarSimulacion(false);

        if (rhythmTracker != null)
            rhythmTracker.DetenerSeguimiento();

        orchestra.StopOrchestra();

        Debug.Log(
            "GAME OVER | Canciones completadas: " +
            cancionesCompletadas
        );

        if (panelDerrota != null)
            panelDerrota.SetActive(true);

        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();
        else
            Time.timeScale = 0f;
    }
}
