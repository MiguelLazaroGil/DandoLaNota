using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Formats.Alembic.Importer; // Namespace estándar del paquete Alembic de Unity

[RequireComponent(typeof(AlembicStreamPlayer))]
public class AlembicTimeController : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Total time in seconds.")]
    [SerializeField] private float duration = 2.0f;
    public float Duration => duration;

    [Tooltip("If active it plays in reverse.")]
    [SerializeField] public bool playReverse = false;

    [Tooltip("Plays on awake")]
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] public UnityEvent onEndAnimation;
    private AlembicStreamPlayer alembicPlayer;
    private float elapsedTime = 0f;
    private float startTime;
    private float endTime;
    private bool isPlaying = false;
    public float alembicStart { get => startTime; }
    public float alembicEnd { get => endTime; }

    public float TimeAlembic { get => elapsedTime; set => elapsedTime = value; }

    private void Awake()
    {
        alembicPlayer = GetComponent<AlembicStreamPlayer>();
        alembicPlayer.enabled = false;
        alembicPlayer.enabled = true;

        if (alembicPlayer != null)
        {
            // Capturamos el rango de tiempo nativo del archivo Alembic
            startTime = 0;
            endTime = alembicPlayer.EndTime - alembicPlayer.StartTime;
        }
    }

    private void Start()
    {
        if (playOnAwake)
        {
            PlayAlembic();
        }
    }

    /// <summary>
    /// Inicia o reinicia la reproducción del Alembic según los parámetros actuales.
    /// </summary>
    public void PlayAlembic()
    {
        elapsedTime = 0f;
        isPlaying = true;
    }

    private void Update()
    {
        if (!isPlaying || alembicPlayer == null) return;

        // Avanzamos el temporizador interno
        elapsedTime += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsedTime / duration);

        float targetTime;

        if (playReverse)
        {
            // Desde el final hasta el principio
            targetTime = Mathf.Lerp(endTime, startTime, progress);
        }
        else
        {
            // Desde el principio hasta el final
            targetTime = Mathf.Lerp(startTime, endTime, progress);
        }

        // Asignamos el tiempo calculado al reproductor de Alembic
        alembicPlayer.CurrentTime = targetTime;

        // Comprobamos si ha terminado el tiempo establecido
        if (progress >= 1f)
        {
            isPlaying = false;
            onEndAnimation.Invoke();
        }
    }
}