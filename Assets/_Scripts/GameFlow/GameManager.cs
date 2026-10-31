
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

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
    [SerializeField] public RhythmTracker rhythmTracker;

    [Header("Control de camara al pausar")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Frames que se ignora el movimiento del raton despues de reanudar.")]
    [SerializeField, Min(1)] private int framesIgnorarMirada = 2;

    private InputAction lookAction;
    private Coroutine reactivarMiradaCoroutine;
    private bool miradaActivaAntesDePausar;

    public ModoJuego ModoActual => modoJuego;

    public bool MusicaIniciada =>
        orchestraAudioManager != null &&
        musicClock != null &&
        orchestraAudioManager.IsPlaying &&
        musicClock.EstaReproduciendo;

    public bool JuegoPausado { get; private set; }

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

        Time.timeScale = 1f;
        JuegoPausado = false;

        if (orchestraAudioManager != null)
            orchestraAudioManager.readytoPlay = false;
    }

    private void Start()
    {
        BuscarAccionMirada();

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
    }

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

        if (orchestraAudioManager.IsPlaying)
            orchestraAudioManager.StopOrchestra();

        orchestraAudioManager.readytoPlay = true;

        if (!MusicaIniciada)
        {
            Debug.LogError(
                "GameManager: No se ha podido iniciar la orquesta."
            );
            return;
        }

        if (rhythmTracker != null && rhythmTracker.SeguimientoActivo)
            rhythmTracker.IniciarSeguimiento();

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

    // ============================================================
    // PAUSA Y REANUDACION
    // ============================================================

    public void PauseGame()
    {
        if (JuegoPausado)
            return;

        if (orchestraAudioManager != null)
            orchestraAudioManager.PausarOrquesta(true);

        if (rhythmTracker != null)
            rhythmTracker.PausarSeguimiento(true);

        // Desactivar Look antes de liberar el cursor.
        BloquearMirada();

        Time.timeScale = 0f;
        JuegoPausado = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void UnpauseGame()
    {
        if (!JuegoPausado)
            return;

        if (orchestraAudioManager != null)
            orchestraAudioManager.PausarOrquesta(false);

        if (rhythmTracker != null)
            rhythmTracker.PausarSeguimiento(false);

        Time.timeScale = 1f;
        JuegoPausado = false;

        ConfigurarCursorJuego();

        // Recuperar Look unos frames despues de bloquear cursor.
        SolicitarReactivacionMirada();
    }

    public void ExitGame()
    {
        if (!JuegoPausado)
            return;

        if (orchestraAudioManager != null)
            orchestraAudioManager.PausarOrquesta(false);

        if (rhythmTracker != null)
            rhythmTracker.PausarSeguimiento(false);

        Time.timeScale = 1f;
        JuegoPausado = false;

        SolicitarReactivacionMirada();
    }

    private void ConfigurarCursorJuego()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ============================================================
    // GESTION DE LA MIRADA
    // ============================================================

    private void BuscarAccionMirada()
    {
        // Intentar localizar el PlayerInput del jugador
        // a partir del RhythmTracker, si no esta asignado.
        if (playerInput == null && rhythmTracker != null)
            playerInput = rhythmTracker.GetComponentInParent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogWarning(
                "GameManager: asigna el PlayerInput del jugador " +
                "para controlar la mirada durante las pausas."
            );
            return;
        }

        if (playerInput.actions == null)
            return;

        lookAction = playerInput.actions.FindAction("Look", false);

        if (lookAction == null)
        {
            Debug.LogWarning(
                "GameManager: no existe la accion Look en el PlayerInput."
            );
        }
    }

    private void BloquearMirada()
    {
        // Cancelar cualquier reactivacion pendiente.
        if (reactivarMiradaCoroutine != null)
        {
            StopCoroutine(reactivarMiradaCoroutine);
            reactivarMiradaCoroutine = null;
        }

        if (lookAction == null)
            BuscarAccionMirada();

        if (lookAction == null)
            return;

        // Recordar si estaba activa.
        // Se mantiene en true si una reactivacion pendiente
        // se ha cancelado por una nueva pausa.
        miradaActivaAntesDePausar |= lookAction.enabled;

        lookAction.Disable();
    }

    private void SolicitarReactivacionMirada()
    {
        if (lookAction == null)
            BuscarAccionMirada();

        if (lookAction == null || !miradaActivaAntesDePausar)
            return;

        if (reactivarMiradaCoroutine != null)
            StopCoroutine(reactivarMiradaCoroutine);

        reactivarMiradaCoroutine = StartCoroutine(
            ReactivarMiradaCoroutine()
        );
    }

    private IEnumerator ReactivarMiradaCoroutine()
    {
        // Esperamos frames, no segundos, para descartar
        // el delta del raton al recapturar el cursor.
        for (int i = 0; i < framesIgnorarMirada; i++)
            yield return null;

        if (!JuegoPausado && lookAction != null)
        {
            if (miradaActivaAntesDePausar)
                lookAction.Enable();

            miradaActivaAntesDePausar = false;
        }

        reactivarMiradaCoroutine = null;
    }

    // ============================================================
    // CAMBIOS DE ESCENA
    // ============================================================

    public void ChangeScene(string sceneName)
    {
        if (reactivarMiradaCoroutine != null)
        {
            StopCoroutine(reactivarMiradaCoroutine);
            reactivarMiradaCoroutine = null;
        }

        Time.timeScale = 1f;
        JuegoPausado = false;

        if (rhythmTracker != null)
            rhythmTracker.DetenerSeguimiento();

        if (orchestraAudioManager != null)
            orchestraAudioManager.StopOrchestra();

        if (sceneName == "MainMenu")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    public void ChangeSceneToMainMenu()
    {
        ChangeScene("MainMenu");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
