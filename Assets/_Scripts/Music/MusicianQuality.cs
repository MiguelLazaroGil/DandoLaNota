using System.Collections.Generic;
using UnityEngine;

public class MusicianQuality : MonoBehaviour
{
    [Header("Calidad individual (lectura)")]
    [SerializeField, Range(0f, 1f)]
    private float musicianQuality = 1f;

    [SerializeField, Range(0f, 1f)]
    private float baseQuality = 1f;

    [SerializeField, Range(0f, 1f)]
    private float desync = 0f;

    [SerializeField, Range(0f, 1f)]
    private float targetQuality = 1f;

    [Header("Control de calidad")]
    [Tooltip("Velocidad de bajada de la calidad (unidades por segundo).")]
    [SerializeField, Min(0f)]
    private float qualityDecaySpeed = 0.5f;

    [Tooltip("Velocidad de subida de la calidad (unidades por segundo).")]
    [SerializeField, Min(0f)]
    private float qualityRecoverySpeed = 0.2f;

    // Cada accion negativa mantiene una penalizacion hasta ser resuelta.
    private readonly Dictionary<string, float> negativeActions = new();

    private bool groupIsPlaying;

    public float MusicianQualityValue => musicianQuality;
    public float TargetQuality => targetQuality;
    public float Desync => desync;
    public float BaseQuality => baseQuality;

    private void Awake()
    {
        musicianQuality = Mathf.Clamp01(musicianQuality);
        baseQuality = Mathf.Clamp01(baseQuality);
        desync = Mathf.Clamp01(desync);
        targetQuality = musicianQuality;
    }

    private void Update()
    {
        // Fuera de la interpretacion congelamos la calidad actual.
        if (!groupIsPlaying)
            return;

        float dt = Time.deltaTime;

        float totalPenalty = 0f;

        foreach (float penalty in negativeActions.Values)
            totalPenalty += penalty;

        // La resincronizacion del grupo mejora automaticamente la calidad objetivo.
        // Las penalizaciones individuales solo desaparecen al corregirlas.
        targetQuality = Mathf.Clamp01(
            baseQuality * (1f - desync) - totalPenalty
        );

        float speed = musicianQuality > targetQuality
            ? qualityDecaySpeed
            : qualityRecoverySpeed;

        musicianQuality = Mathf.MoveTowards(
            musicianQuality,
            targetQuality,
            speed * dt
        );
    }

    // Lo llama el InstrumentGroup padre con el desfase real de FMOD.
    public void SetSectionState(float normalizedDesync, bool playing)
    {
        desync = Mathf.Clamp01(normalizedDesync);
        groupIsPlaying = playing;
    }

    // Penalizacion puntual que afecta a la calidad base.
    public void TriggerNegativeEvent(float amount)
    {
        baseQuality = Mathf.Clamp01(baseQuality - Mathf.Clamp01(amount));
    }

    // Correccion puntual que recupera parte de la calidad base.
    public void TriggerCorrectiveEvent(float amount)
    {
        baseQuality = Mathf.Clamp01(baseQuality + Mathf.Clamp01(amount));
    }

    // Activa o actualiza una penalizacion mientras dure un sabotaje.
    public void StartNegativeAction(string actionId, float penalty)
    {
        if (string.IsNullOrEmpty(actionId))
            return;

        negativeActions[actionId] = Mathf.Clamp01(penalty);
    }

    // Neutralizar un sabotaje permite recuperar calidad de forma progresiva.
    public void ResolveNegativeAction(string actionId)
    {
        if (string.IsNullOrEmpty(actionId))
            return;

        negativeActions.Remove(actionId);
    }
}
