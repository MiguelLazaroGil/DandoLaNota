using System.Collections;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private OrchestraAudioManager orchestraAudioManager;
    [SerializeField] private MusicClockFMOD musicClock;
    [SerializeField] public RhythmTracker rhythmTracker;
    [SerializeField] private AudienceManager audienceManager;
    [SerializeField] private MusicScoreManager musicScoreManager;
    [Header("Inicio de la canción")]
    [SerializeField, Min(0f)]
    private float tiempoEsperaInicial = 5f;
    [Header("Paneles")]
    [SerializeField] private GameObject panelVictoria;
    [SerializeField] private GameObject panelDerrota;

    [Header("Configuracion")]
    [SerializeField] private bool iniciarAutomaticamente = true;

    [Header("Estado en vivo (lectura)")]
    [SerializeField] private bool nivelActivo;
    [SerializeField] private bool nivelTerminado;
    [SerializeField] private float tiempoTranscurrido;

    public bool NivelActivo => nivelActivo;
    public bool NivelTerminado => nivelTerminado;
    private bool iniciandoNivel;

    private void Start()
    {
        if (panelVictoria != null)
            panelVictoria.SetActive(false);

        if (panelDerrota != null)
            panelDerrota.SetActive(false);

        if (iniciarAutomaticamente)
            IniciarNivel();
    }

    
public void IniciarNivel()
{
    if (nivelActivo || nivelTerminado || iniciandoNivel)
        return;

    if (GameManager.Instance == null ||
        orchestraAudioManager == null ||
        musicClock == null ||
        audienceManager == null)
    {
        Debug.LogError("LevelManager: faltan referencias.");
        return;
    }

    if (GameManager.Instance.ModoActual !=
        GameManager.ModoJuego.NivelNormal)
    {
        Debug.LogError("LevelManager: modo de juego incorrecto.");
        return;
    }

    iniciandoNivel = true;

    // Detenemos la audiencia y la puntuacion durante la espera.
    audienceManager.ActivarSimulacion(false);

    if (musicScoreManager != null)
        musicScoreManager.enabled = false;

    StartCoroutine(EsperarEIniciarMusica());
}

private IEnumerator EsperarEIniciarMusica()
{
    Debug.Log("La musica comenzara en " +
              tiempoEsperaInicial + " segundos.");

    yield return new WaitForSeconds(tiempoEsperaInicial);

    if (musicScoreManager != null)
        musicScoreManager.enabled = true;

    GameManager.Instance.IniciarMusica();

    iniciandoNivel = false;

    if (!orchestraAudioManager.IsPlaying ||
        !musicClock.EstaReproduciendo)
    {
        Debug.LogError("LevelManager: no se pudo iniciar la musica.");
        yield break;
    }

    tiempoTranscurrido = 0f;
    nivelActivo = true;

    audienceManager.ActivarSimulacion(true);

    Debug.Log("LevelManager: nivel iniciado.");
}


    private void Update()
    {
        if (!nivelActivo || nivelTerminado || Time.timeScale == 0f)
            return;

        // Condicion de derrota.
        if (audienceManager.CurrentAudience <= 0f)
        {
            PerderNivel();
            return;
        }

        // Tiempo transcurrido de la cancion.
        if (musicClock.TryObtenerTiempo(out double tiempo))
            tiempoTranscurrido = (float)tiempo;

        // La cancion termina naturalmente en FMOD.
        if (musicClock.HaTerminadoEvento())
        {
            GanarNivel();
        }
    }

    public void GanarNivel()
    {
        if (nivelTerminado || !nivelActivo)
            return;

        FinalizarNivel();

        Debug.Log("LEVEL COMPLETED: HAS GANADO.");

        if (panelVictoria != null)
            panelVictoria.SetActive(true);

        GameManager.Instance.PauseGame();
    }

    public void PerderNivel()
    {
        if (nivelTerminado || !nivelActivo)
            return;

        FinalizarNivel();

        Debug.Log("GAME OVER: HAS PERDIDO.");

        if (panelDerrota != null)
            panelDerrota.SetActive(true);

        GameManager.Instance.PauseGame();
    }

    private void FinalizarNivel()
    {
        nivelActivo = false;
        nivelTerminado = true;

        // Detenemos los cambios de audiencia.
        audienceManager.ActivarSimulacion(false);

        // Detenemos el seguimiento de la batuta.
        if (rhythmTracker != null)
            rhythmTracker.DetenerSeguimiento();

        // Detenemos toda la orquesta y el reloj.
        orchestraAudioManager.StopOrchestra();
    }
}
