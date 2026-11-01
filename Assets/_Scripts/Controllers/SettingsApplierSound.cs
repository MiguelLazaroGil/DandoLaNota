using UnityEngine;
using UnityEngine.Audio;

public class SettingsApplierSound : MonoBehaviour
{
    [SerializeField]
    AudioMixer audioMixer;
    public void ApplySoundSettings(bool Mute = false, float MasterVolume = 1f, float MusicVolume = 1f, float SFXVolume = 1f)
    {
        if (Mute)//if mute set to -80 all values
        {
            audioMixer.SetFloat("Master", -80f);
            audioMixer.SetFloat("Music", -80f);
            audioMixer.SetFloat("SFX", -80f);
            return;
        }
        audioMixer.SetFloat("Master", LinearToDecibel(MasterVolume));
        audioMixer.SetFloat("Music", LinearToDecibel(MusicVolume));
        audioMixer.SetFloat("SFX", LinearToDecibel(SFXVolume));
    }
    float LinearToDecibel(float value)
    {
        if (value <= 0.0001f)
            return -80f; // Mute
        return Mathf.Log10(value) * 20f;
    }
}
