using UnityEngine;
using GVLoadSystem.Encryption;
using GVLoadSystem.GVDebug;


#if UNITY_EDITOR
using GVLoadSystem.GVEditor;
#endif

namespace GVLoadSystem
{
    internal static class GroupValuesDependencySolver
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
#if UNITY_EDITOR
            var settings = GroupValuesProjectSettings.instance;
            if (settings == null) return;

            SetSaveSubfolder(settings.saveSubfolder);
            
            if (settings.enableDebugOverlay)
            {
                SetOverlayConfig(
                    settings.enableDebugOverlay,
                    settings.debugKey,
                    settings.overlayEditMode,
                    settings.freezeTimeOnOverlay);
            }
#endif
        }

        public static void SetSaveSubfolder(string subfolder)
        {
            DeviceKeyProvider.SetSaveSubfolder(subfolder);
        }

        public static void SetOverlayConfig(bool enable, KeyCode key, bool editMode, bool freeze)
        {
            GroupValuesDebugOverlayConfig.enableOverlay = enable;
            GroupValuesDebugOverlayConfig.debugKey = key;
            GroupValuesDebugOverlayConfig.editMode = editMode;
            GroupValuesDebugOverlayConfig.freezeOnOpen = freeze;
        }
    }
}