
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum ModoJuego
    {
        Tutorial,
        NivelNormal,
        Infinito
    }

    [Header("Configuracion del nivel")]
    [SerializeField] private ModoJuego modoJuego = ModoJuego.Tutorial;

    [Tooltip("Solo para niveles normales. El modo infinito gestiona su propia musica.")]
    [SerializeField] private bool iniciarMusicaAutomaticamente = true;

    [Header("Managers")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private OrchestraAudioManager orchestraAudioManager;
    [SerializeField] private MusicClockFMOD musicClock;
    [SerializeField] private RhythmTracker rhythmTracker;

    public ModoJuego ModoActual => modoJuego;

    public bool MusicaIniciada =>
        orchestraAudioManager != null &&
        musicClock != null &&
        orchestraAudioManager.IsPlaying &&
        musicClock.EstaReproduciendo;

    public bool JuegoPausado { get; private set; }

    // El tutorial limita cuando puede sacarse la batuta.
    // En los otros niveles puede utilizarse libremente.
    public bool PuedeSacarBatuta =>
        !JuegoPausado &&
        (modoJuego != ModoJuego.Tutorial ||
         (tutorialManager != null && tutorialManager.PuedeSacarBatuta));

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Evita heredar una pausa de la escena anterior.
        Time.timeScale = 1f;
        JuegoPausado = false;

        // La musica no debe comenzar automaticamente desde la orquesta.
        if (orchestraAudioManager != null)
            orchestraAudioManager.readytoPlay = false;
    }

    private void Start()
    {
        switch (modoJuego)
        {
            case ModoJuego.Tutorial:
                IniciarModoTutorial();
                break;

            case ModoJuego.NivelNormal:
                IniciarNivelNormal();
                break;

            case ModoJuego.Infinito:
                IniciarModoInfinito();
                break;
        }
    }

    private void IniciarModoTutorial()
    {
        PauseGame();

        if (tutorialManager != null)
            tutorialManager.IniciarTutorial();
        else
            Debug.LogError("GameManager: Falta TutorialManager.");

        // TutorialManager decidira cuando comenzar la musica.
    }

    private void IniciarNivelNormal()
    {
        ConfigurarCursorJuego();

        if (iniciarMusicaAutomaticamente)
            IniciarMusica();
    }

    private void IniciarModoInfinito()
    {
        ConfigurarCursorJuego();

        // InfiniteLevelManager decide cuando comenzar cada cancion.
        // No iniciamos la musica aqui.
    }

    // Utilizado por TutorialManager y por los niveles normales.
    public void IniciarMusica()
    {
        if (modoJuego == ModoJuego.Infinito)
        {
            Debug.LogWarning(
                "GameManager: InfiniteLevelManager controla la musica."
            );
            return;
        }

        if (MusicaIniciada)
            return;

        if (orchestraAudioManager == null || musicClock == null)
        {
            Debug.LogError("GameManager: Faltan referencias musicales.");
            return;
        }

        // Permite reiniciar una pista normal que ya haya terminado.
        if (orchestraAudioManager.IsPlaying)
            orchestraAudioManager.StopOrchestra();

        // StartOrchestra ya inicia MusicClockFMOD.
        orchestraAudioManager.readytoPlay = true;

        if (!MusicaIniciada)
        {
            Debug.LogError(
                "GameManager: No se ha podido iniciar la orquesta."
            );
            return;
        }

        // Si ya tenia la batuta activa, actualizamos
        // el seguimiento al reloj que acaba de comenzar.
        if (rhythmTracker != null && rhythmTracker.SeguimientoActivo)
            rhythmTracker.IniciarSeguimiento();

        // Si se inicia mientras hay un menu, la musica nace pausada.
        if (JuegoPausado)
            orchestraAudioManager.PausarOrquesta(true);

        Debug.Log("GameManager: Musica iniciada.");
    }

    [ContextMenu("DEBUG/Iniciar musica")]
    private void DebugIniciarMusica()
    {
        if (Application.isPlaying)
            IniciarMusica();
    }

    public void PauseGame()
    {
        if (JuegoPausado)
            return;

        // Pausamos todos los eventos FMOD.
        if (orchestraAudioManager != null)
            orchestraAudioManager.PausarOrquesta(true);

        // El tracker utiliza tiempo real y debe pausarse expresamente.
        if (rhythmTracker != null)
            rhythmTracker.PausarSeguimiento(true);

        Time.timeScale = 0f;
        JuegoPausado = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void UnpauseGame()
    {
        if (!JuegoPausado)
            return;

        // Reanudamos los mismos eventos desde su posicion anterior.
        if (orchestraAudioManager != null)
            orchestraAudioManager.PausarOrquesta(false);

        if (rhythmTracker != null)
            rhythmTracker.PausarSeguimiento(false);

        Time.timeScale = 1f;
        JuegoPausado = false;

        ConfigurarCursorJuego();
    }

    private void ConfigurarCursorJuego()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public void ChangeScene(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
