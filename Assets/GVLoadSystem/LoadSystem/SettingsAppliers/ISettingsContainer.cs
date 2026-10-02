using System;

namespace GVLoadSystem.Interfaces
{
    public interface ISettingsContainer
    {
        public void SubscribeToSettingsChange(Action onChange);
        public T GetValue<T>(string key);
        public void SetValue<T>(string key, T value);
    }

    /// <summary>
    /// Lightweight service locator for ISettingsContainer.
    /// SettingsManager registers/unregisters itself here.
    /// UISettingsElement uses Get() instead of FindObjectsByType.
    /// </summary>
    public static class SettingsContainerLocator
    {
        static ISettingsContainer _instance;

        public static void Register(ISettingsContainer container)
        {
            if (_instance != null && _instance != container)
                UnityEngine.Debug.LogWarning(
                    "[SettingsContainerLocator] A container was already registered. Overwriting.");
            _instance = container;
        }

        public static void Unregister(ISettingsContainer container)
        {
            if (_instance == container) _instance = null;
        }

        public static ISettingsContainer Get() => _instance;

        public static bool HasContainer => _instance != null;
    }
}