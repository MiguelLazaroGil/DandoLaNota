using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[DisallowMultipleComponent]
public class InstrumentGroup : MonoBehaviour
{
    [Header("FMOD - Evento del grupo")]
    [SerializeField] private EventReference audioEvent;

    [Tooltip("Punto desde el que se reproduce el evento 3D. Si no se asigna, se usa este objeto.")]
    [SerializeField] private Transform audioPosition;

    [Header("FMOD - Volumen y alcance 3D")]
    [Tooltip("Volumen base de esta seccion. 1 = volumen original; 0.5 = mitad de amplitud; 2 = doble amplitud.")]
    [SerializeField, Range(0f, 2f)] private float groupVolume = 1f;

    [Tooltip("Distancia hasta la que el instrumento mantiene su volumen sin atenuacion por distancia (en unidades de Unity).")]
    [SerializeField, Min(0f)] private float minDistance = 8f;

    [Tooltip("Distancia a partir de la que se alcanza el extremo de atenuacion del Spatializer (en unidades de Unity).")]
    [SerializeField, Min(0.01f)] private float maxDistance = 70f;

    [Header("Calculo de desincronizacion del grupo")]
    [Tooltip("Desfase temporal (en segundos) equivalente a penalizacion completa.")]
    [SerializeField, Min(0.01f)] private float timeDesyncForZeroQuality = 0.30f;

    [Tooltip("Desviacion de pitch equivalente a penalizacion maxima de velocidad.")]
    [SerializeField, Min(0.01f)] private float pitchDesyncForZeroQuality = 0.20f;

    [Tooltip("Peso de la desviacion de pitch, sumado al desfase temporal.")]
    [SerializeField, Range(0f, 1f)] private float pitchDesyncWeight = 0.25f;

    [Header("Estado del grupo (lectura)")]
    [SerializeField] private float currentPitch = 1f;
    [SerializeField] private float targetPitch = 1f;
    [SerializeField] private float timeDifference;
    [SerializeField, Range(0f, 1f)] private float normalizedDesync;

    private EventInstance instance;
    private MusicianQuality[] musicians;
    private bool isPlaying;

    // Multiplicador de volumen que puede seguir modificando OrchestraAudioManager.
    private float externalVolume = 1f;

    // Valores aplicados para evitar mandar comandos identicos a FMOD cada frame.
    private float appliedVolume = -1f;
    private float appliedMinDistance = -1f;
    private float appliedMaxDistance = -1f;

    public bool IsPlaying => isPlaying;
    public float CurrentPitch => currentPitch;
    public float TimeDifference => timeDifference;
    public float NormalizedDesync => normalizedDesync;

    private void Awake()
    {
        RefreshMusicians();
    }

    private void OnValidate()
    {
        minDistance = Mathf.Max(0f, minDistance);
        maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);
    }

    // Permite ajustar volumen y distancias desde el Inspector incluso en Play Mode.
    private void LateUpdate()
    {
        if (isPlaying && instance.isValid())
            ApplyAudioSettings();
    }

    [ContextMenu("Actualizar musicos hijos")]
    public void RefreshMusicians()
    {
        musicians = GetComponentsInChildren<MusicianQuality>(true);
    }

    public bool StartGroup()
    {
        if (isPlaying)
            return true;

        if (audioEvent.IsNull)
        {
            Debug.LogWarning($"FMOD event missing in group {name}.");
            return false;
        }

        RefreshMusicians();
        instance = RuntimeManager.CreateInstance(audioEvent);

        Transform position = audioPosition != null ? audioPosition : transform;
        RuntimeManager.AttachInstanceToGameObject(instance, position.gameObject);

        currentPitch = 1f;
        targetPitch = 1f;
        timeDifference = 0f;
        normalizedDesync = 0f;
        instance.setPitch(1f);

        // Cada instancia recibe sus propios valores, antes de arrancar.
        ApplyAudioSettings(force: true);

        FMOD.RESULT result = instance.start();
        if (result != FMOD.RESULT.OK)
        {
            Debug.LogError($"Could not start group {name}: {result}");
            instance.release();
            instance = default;
            return false;
        }

        isPlaying = true;
        UpdateMusicians();
        return true;
    }

    public void StopGroup()
    {
        if (instance.isValid())
        {
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
            instance = default;
        }

        isPlaying = false;
        currentPitch = 1f;
        targetPitch = 1f;
        UpdateMusicians();
    }

    private void ApplyAudioSettings(bool force = false)
    {
        if (!instance.isValid())
            return;

        float volume = Mathf.Max(0f, groupVolume * externalVolume);
        float min = Mathf.Max(0f, minDistance);
        float max = Mathf.Max(min + 0.01f, maxDistance);

        if (force || !Mathf.Approximately(appliedVolume, volume))
        {
            instance.setVolume(volume);
            appliedVolume = volume;
        }

        // Primero el maximo para evitar que un minimo nuevo supere al maximo anterior.
        if (force || !Mathf.Approximately(appliedMaxDistance, max))
        {
            instance.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, max);
            appliedMaxDistance = max;
        }

        if (force || !Mathf.Approximately(appliedMinDistance, min))
        {
            instance.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, min);
            appliedMinDistance = min;
        }
    }

    public void CalculateNewPitch(float deviation)
    {
        if (!isPlaying || !instance.isValid())
            return;

        targetPitch = Random.Range(1f - deviation, 1f + deviation);
    }

    public void UpdateGroup(
        float dt,
        float clockTime,
        float loopDuration,
        float synchronizationFactor,
        float synchronizationStrength,
        float maxSynchronizationPitch,
        float synchronizationDeadZone,
        float pitchSmoothing)
    {
        if (!isPlaying || !instance.isValid() || loopDuration <= 0f)
            return;

        if (instance.getTimelinePosition(out int groupPosition) != FMOD.RESULT.OK)
            return;

        float groupTime = groupPosition / 1000f;

        timeDifference = Mathf.Repeat(
            groupTime - clockTime + loopDuration * 0.5f,
            loopDuration
        ) - loopDuration * 0.5f;

        float pitchCorrection = 0f;
        if (Mathf.Abs(timeDifference) > synchronizationDeadZone)
        {
            pitchCorrection = Mathf.Clamp(
                -timeDifference * synchronizationStrength,
                -maxSynchronizationPitch,
                maxSynchronizationPitch
            );
            pitchCorrection *= synchronizationFactor;
        }

        float finalTargetPitch = targetPitch + pitchCorrection;
        currentPitch = Mathf.MoveTowards(
            currentPitch,
            finalTargetPitch,
            pitchSmoothing * dt
        );
        instance.setPitch(currentPitch);

        float timePenalty = Mathf.Clamp01(
            (Mathf.Abs(timeDifference) - synchronizationDeadZone) /
            Mathf.Max(0.001f, timeDesyncForZeroQuality - synchronizationDeadZone)
        );

        float pitchPenalty = Mathf.Clamp01(
            Mathf.Abs(currentPitch - 1f) / pitchDesyncForZeroQuality
        );

        normalizedDesync = Mathf.Clamp01(
            timePenalty + pitchPenalty * pitchDesyncWeight
        );
        UpdateMusicians();
    }

    public void SynchronizeWithClock(int position)
    {
        if (!isPlaying || !instance.isValid())
            return;

        instance.setTimelinePosition(position);
        currentPitch = 1f;
        targetPitch = 1f;
        timeDifference = 0f;
        normalizedDesync = 0f;
        instance.setPitch(1f);
        UpdateMusicians();
    }

    private void UpdateMusicians()
    {
        if (musicians == null)
            return;

        foreach (MusicianQuality musician in musicians)
        {
            if (musician != null)
                musician.SetSectionState(normalizedDesync, isPlaying);
        }
    }

    public int GetMusicianCount()
    {
        int count = 0;
        if (musicians == null)
            return count;

        foreach (MusicianQuality musician in musicians)
            if (musician != null && musician.isActiveAndEnabled)
                count++;

        return count;
    }

    public float GetTotalMusicianQuality()
    {
        float total = 0f;
        if (musicians == null)
            return total;

        foreach (MusicianQuality musician in musicians)
            if (musician != null && musician.isActiveAndEnabled)
                total += musician.MusicianQualityValue;

        return total;
    }

    public float GetAverageQuality()
    {
        int count = GetMusicianCount();
        return count > 0 ? GetTotalMusicianQuality() / count : 1f;
    }

    public void PausarGrupo(bool pausar)
    {
        if (isPlaying && instance.isValid())
            instance.setPaused(pausar);
    }

    public bool TieneEventoAsignado => !audioEvent.IsNull;

    public void ConfigurarEvento(EventReference nuevoEvento)
    {
        if (isPlaying)
        {
            Debug.LogWarning("Deten el grupo antes de cambiar su evento.");
            return;
        }

        audioEvent = nuevoEvento;
    }

    // Conserva el control de volumen de los managers, sin perder el ajuste base del grupo.
    public void EstablecerVolumen(float volumen)
    {
        externalVolume = Mathf.Clamp01(volumen);
        ApplyAudioSettings();
    }

    private void OnDestroy()
    {
        StopGroup();
    }
}
