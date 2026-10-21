
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Managers")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private OrchestraAudioManager orchestraAudioManager;
    [SerializeField] private MusicClockFMOD musicClock;

    public bool MusicaIniciada { get; private set; }

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

        orchestraAudioManager.readytoPlay = true;
        musicClock.IniciarReloj();

        MusicaIniciada = true;

        Debug.Log("GameManager: Musica iniciada.");
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
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public void PauseGame()
    {
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
