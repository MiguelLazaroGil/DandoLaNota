using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GVLoadSystem.Core;
using GVLoadSystem.Interfaces;
using GVUtils.Attributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GVLoadSystem.Examples
{
    [RequireComponent(typeof(Volume))]
    public class CameraViewPortSettings : MonoBehaviour, ILoaderUser
    {
        public GVEntryReference viewPortReference;//reference to where viewport settings are stored
        private ISettingsContainer container;

        Volume cameraVolume;
        ColorAdjustments colorAdjustments;

        [Button("Look for values")]
        public void Init()
        {
            SubscribeToValuesChange();

            OnValuesChange();

        }
        public void OnValuesChange()//whenever values change this method is called
        {
            //cache the whole class
            CameraViewPortSettingsData cameraViewPortSettings = container.GetValue<CameraViewPortSettingsData>(viewPortReference.EntryKey);

            if (cameraViewPortSettings == null)
            {
                Debug.LogError("[CameraViewPortSettings]No data found in entry");
                return;
            }
            Debug.Log("[CameraViewPortSettings]Settings found: \n MaxVBrighness " + cameraViewPortSettings.maxValueBrightness +
            ", BaseVContrast " + cameraViewPortSettings.baseValueContrast);
            cameraVolume = GetComponent<Volume>();

            if (cameraVolume.profile.TryGet(out colorAdjustments) && cameraVolume != null)
            {
                float bt = cameraViewPortSettings.brightness;
                float brightness = Mathf.Lerp(cameraViewPortSettings.baseValueBrightness, cameraViewPortSettings.maxValueBrightness, bt);
                colorAdjustments.postExposure.value = cameraViewPortSettings.brightness;

                float ct = cameraViewPortSettings.contrast;

                float contrast = Mathf.Lerp(cameraViewPortSettings.baseValueContrast, cameraViewPortSettings.maxValueContrast, ct);

                colorAdjustments.contrast.value = contrast;

            }
            else
            {
                Debug.LogWarning("[CameraViewPortSettings] ColorAdjustments not found in Volume Profile.");
            }


        }
        public void SubscribeToValuesChange()
        {
            container = SettingsContainerLocator.Get();

            if (container != null)//if there is a settingsContainer, subscribe OnValuesChange method
                container.SubscribeToSettingsChange(OnValuesChange);

        }
    }
    [CustomGVData("ViewPortSettings")]
    public class CameraViewPortSettingsData //A custom class that contains all viewport settings related data
    {
        [SerializeField] public float baseValueBrightness = -1;
        [SerializeField] public float maxValueBrightness = 0.8f;
        [SerializeField] public float brightness;
        [SerializeField] public float baseValueContrast = 0;
        [SerializeField] public float maxValueContrast = 100;
        [SerializeField] public float contrast;
    }
}