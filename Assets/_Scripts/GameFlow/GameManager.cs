using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Managers")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private OrchestraAudioManager orchestraAudioManager;
    [SerializeField] private MusicClockFMOD musicClock;
    [SerializeField] private RhythmTracker rhythmTracker;

    public bool MusicaIniciada { get; private set; }
    public bool JuegoPausado { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // La musica no debe comenzar automaticamente.
        if (orchestraAudioManager != null)
            orchestraAudioManager.readytoPlay = false;
    }

    private void Start()
    {
        PauseGame();
        MusicaIniciada = false;

        if (tutorialManager != null)
            tutorialManager.IniciarTutorial();
        else
            Debug.LogError("GameManager: Falta TutorialManager.");
    }

    // El tutorial decidira cuando llamar a este metodo.
    public void IniciarMusica()
    {
        if (MusicaIniciada)
            return;

        if (orchestraAudioManager == null || musicClock == null)
        {
            Debug.LogError("GameManager: Faltan referencias musicales.");
            return;
        }

        // OrchestraAudioManager.StartOrchestra() ya arranca MusicClockFMOD.
        // No llamamos de nuevo a IniciarReloj(), porque reiniciaria la timeline.
        orchestraAudioManager.readytoPlay = true;
        MusicaIniciada = orchestraAudioManager.IsPlaying && musicClock.EstaReproduciendo;

        // Si se inicia mediante DEBUG mientras hay un menu, debe nacer pausada.
        if (MusicaIniciada && JuegoPausado)
            orchestraAudioManager.PausarOrquesta(true);

        if (MusicaIniciada)
            Debug.Log("GameManager: Musica iniciada.");
        else
            Debug.LogError("GameManager: No se ha podido iniciar la orquesta.");
    }

    // Permite probar el inicio desde el Inspector durante Play Mode.
    [ContextMenu("DEBUG/Iniciar musica")]
    private void DebugIniciarMusica()
    {
        if (Application.isPlaying)
            IniciarMusica();
    }

    public void UnpauseGame()
    {
        if (!JuegoPausado)
            return;

        // Reanudamos el mismo evento FMOD donde se quedo: no reiniciamos nada.
        if (orchestraAudioManager != null)
            orchestraAudioManager.PausarOrquesta(false);

        if (rhythmTracker != null)
            rhythmTracker.PausarSeguimiento(false);

        Time.timeScale = 1f;
        JuegoPausado = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void PauseGame()
    {
        if (JuegoPausado)
            return;

        // Pausamos el reloj y todos los grupos FMOD de forma conjunta.
        if (orchestraAudioManager != null)
            orchestraAudioManager.PausarOrquesta(true);

        // El tracker utiliza tiempo real; debemos pausarlo expresamente.
        if (rhythmTracker != null)
            rhythmTracker.PausarSeguimiento(true);

        Time.timeScale = 0f;
        JuegoPausado = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
