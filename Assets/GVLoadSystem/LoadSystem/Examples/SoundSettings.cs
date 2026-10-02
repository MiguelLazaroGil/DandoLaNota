
using System;
using System.Linq;
using GVLoadSystem.Core;
using GVLoadSystem.GVEditor;
using GVLoadSystem.Interfaces;
using GVUtils.Attributes;
using UnityEngine;
using UnityEngine.Audio;
namespace GVLoadSystem.Examples
{
    [Serializable]
    public class SoundSettingsApplier : MonoBehaviour, ILoaderUser
    {
        [SerializeField] private AudioMixer audioMixer; // Mixer general del proyecto
        [SerializeField] private GVEntryReference soundReference;
        ISettingsContainer container;
        [Button("Look for values")]
        public void Init()
        {
            SubscribeToValuesChange();

            OnValuesChange();

        }

        public void OnValuesChange()
        {
            SoundSettingsData soundSettingsData = container.GetValue<SoundSettingsData>(soundReference.EntryKey);

            if (soundSettingsData == null)
            {
                Debug.LogError("[SoundSettingsApplier]Sound settings data not found");
                return;
            }
            Debug.Log("[SoundSettingsApplier]Settings found: \nMute " + soundSettingsData.mute + ", MasterVolume " + soundSettingsData.masterVolume);
            if (audioMixer == null)
            {
                Debug.LogError("Assign an audio mixer to apply settings");
                return;
            }
            if (soundSettingsData.mute)//if mute set to -80 all values
            {
                audioMixer.SetFloat("MasterVolume", -80f);
                audioMixer.SetFloat("MusicVolume", -80f);
                audioMixer.SetFloat("SFXVolume", -80f);
                audioMixer.SetFloat("InterfaceVolume", -80f);
                return;
            }

            // Convert  [0,1] lineal to dB (logarithmic)
            audioMixer.SetFloat("MasterVolume", LinearToDecibel(soundSettingsData.masterVolume));
            audioMixer.SetFloat("MusicVolume", LinearToDecibel(soundSettingsData.musicVolume));
            audioMixer.SetFloat("SFXVolume", LinearToDecibel(soundSettingsData.SFXVolume));
            audioMixer.SetFloat("InterfaceVolume", LinearToDecibel(soundSettingsData.interfaceVolume));
        }

        public void SubscribeToValuesChange()
        {
            container = SettingsContainerLocator.Get();
            if (container != null)
                container.SubscribeToSettingsChange(OnValuesChange);

        }


        float LinearToDecibel(float value)
        {
            if (value <= 0.0001f)
                return -80f; // Mute
            return Mathf.Log10(value) * 20f;
        }
    }
    [CustomGVData("SoundSettings")]
    public class SoundSettingsData
    {
        public bool mute = false;
        [GVRange(0, 1)]
        public float masterVolume = -80f;
        [GVRange(0, 1)]
        public float musicVolume = -80f;
        [GVRange(0, 1)]
        public float SFXVolume = -80f;
        [GVRange(0, 1)]
        public float interfaceVolume = -80f;

    }
}