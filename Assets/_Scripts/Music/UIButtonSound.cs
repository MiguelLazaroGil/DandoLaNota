
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class UIButtonSound : MonoBehaviour
{
    [Header("FMOD Sound")]
    [SerializeField] private EventReference soundEvent;

    [Header("Sound Settings")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Min(0f)] private float cooldown = 0.8f;

    private float lastPlayTime = Mathf.NegativeInfinity;

    public void PlaySound(float s)
    {
        if (soundEvent.IsNull)
            return;

        if (Time.unscaledTime - lastPlayTime < cooldown)
            return;

        EventInstance sound = RuntimeManager.CreateInstance(soundEvent);

        if (!sound.isValid())
            return;

        sound.setVolume(s);
        sound.start();
        sound.release();

        lastPlayTime = Time.unscaledTime;
    }
}
