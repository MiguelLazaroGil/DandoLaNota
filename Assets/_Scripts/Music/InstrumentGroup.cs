using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[DisallowMultipleComponent]
public class InstrumentGroup : MonoBehaviour
{
    [Header("FMOD - Evento del grupo")]
    [SerializeField] private EventReference audioEvent;

    [Tooltip("Punto desde el que se reproduce el evento 3D. Si no se asigna, se usa este padre.")]
    [SerializeField] private Transform audioPosition;

    [Header("Calculo de desincronizacion del grupo")]
    [Tooltip("Desfase temporal (en segundos) equivalente a penalizacion completa.")]
    [SerializeField, Min(0.01f)]
    private float timeDesyncForZeroQuality = 0.30f;

    [Tooltip("Desviacion de pitch equivalente a penalizacion maxima de velocidad.")]
    [SerializeField, Min(0.01f)]
    private float pitchDesyncForZeroQuality = 0.20f;

    [Tooltip("Peso de la desviacion de pitch, sumado al desfase temporal.")]
    [SerializeField, Range(0f, 1f)]
    private float pitchDesyncWeight = 0.25f;

    [Header("Estado del grupo (lectura)")]
    [SerializeField] private float currentPitch = 1f;
    [SerializeField] private float targetPitch = 1f;
    [SerializeField] private float timeDifference;
    [SerializeField, Range(0f, 1f)] private float normalizedDesync;

    private EventInstance instance;
    private MusicianQuality[] musicians;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public float CurrentPitch => currentPitch;
    public float TimeDifference => timeDifference;
    public float NormalizedDesync => normalizedDesync;

    private void Awake()
    {
        RefreshMusicians();
    }

    [ContextMenu("Actualizar musicos hijos")]
    public void RefreshMusicians()
    {
        // Los musicos pueden variar entre escenas; se buscan en los hijos.
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

        // Desfase circular entre el evento del grupo y el reloj FMOD.
        timeDifference = Mathf.Repeat(
            groupTime - clockTime + loopDuration * 0.5f,
            loopDuration
        ) - loopDuration * 0.5f;

        // Pitch adicional que acerca progresivamente el grupo al reloj.
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

        // El grupo comparte audio y desfase, pero cada hijo tiene su propia calidad.
        // El margen muerto tambien evita penalizar pequeñas diferencias de timeline.
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
            {
                musician.SetSectionState(
                    normalizedDesync,
                    isPlaying
                );
            }
        }
    }

    public int GetMusicianCount()
    {
        int count = 0;
        if (musicians == null) return count;

        foreach (MusicianQuality musician in musicians)
            if (musician != null && musician.isActiveAndEnabled)
                count++;

        return count;
    }

    public float GetTotalMusicianQuality()
    {
        float total = 0f;
        if (musicians == null) return total;

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
    private void OnDestroy()
    {
        StopGroup();
    }

}
