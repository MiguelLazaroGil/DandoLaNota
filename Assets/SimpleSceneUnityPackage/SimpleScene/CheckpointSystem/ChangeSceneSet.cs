using System;
using System.Collections.Generic;
using UnityEngine;

namespace MLG.SimpleSceneTool.CheckpointSystem
{
    public enum SceneSetLoadMode
    {
        Single,
        Multiple
    }

    /// <summary>
    /// Inspector-friendly entry point for loading a Scene Set (or a mix of several) from
    /// buttons, triggers, or other MonoBehaviour workflows. All actual scene work goes through
    /// SceneSetLoader - this component never touches SceneManager directly.
    /// </summary>
    public class ChangeSceneSet : MonoBehaviour
    {
        public SceneSetLoadMode mode = SceneSetLoadMode.Single;

        [Tooltip("Used when Mode is Single.")]
        public SceneSet sceneSet;

        [Tooltip("Used when Mode is Multiple - scenes from every set here are combined (duplicates removed) into one load.")]
        public List<SceneSet> sceneSets = new List<SceneSet>();

        [Tooltip("When enabled (default), scenes no longer needed are unloaded as soon as the new ones activate. When disabled, they stay loaded until UnloadUnusedScenes is called.")]
        public bool unloadUnusedImmediately = true;

        [Header("Reload")]
        [Tooltip("Scene Sets whose scenes should be reloaded by ReloadConfigured().")]
        public List<SceneSet> sceneSetsToReload = new List<SceneSet>();

        [Tooltip("Scene Sets to exclude from the reload - e.g. put Essentials here so reloading Level 1 doesn't reload it too.")]
        public List<SceneSet> sceneSetsToExcludeFromReload = new List<SceneSet>();

        /// <summary>Loads whichever Scene Set(s) this component is currently configured for, right away.</summary>
        public void LoadSceneSetNow()
        {
            SceneSetLoader.Load(ResolveSceneSets(), false, null, unloadUnusedImmediately);
        }

        /// <summary>
        /// Starts loading this component's Scene Set(s) in the background. Nothing currently
        /// visible changes until ActivatePendingPreload is called, or CancelPendingPreload to
        /// back out and stay put.
        /// </summary>
        public void PreloadSceneSet()
        {
            SceneSetLoader.Load(ResolveSceneSets(), true, null, unloadUnusedImmediately);
        }

        public void ActivatePendingPreload()
        {
            SceneSetLoader.ActivatePendingPreload();
        }

        public void CancelPendingPreload()
        {
            SceneSetLoader.CancelPendingPreload();
        }

        /// <summary>Unloads every scene left hanging by a previous load that used unloadUnusedImmediately = false.</summary>
        public void UnloadUnusedScenes()
        {
            SceneSetLoader.UnloadUnusedScenes();
        }

        /// <summary>
        /// Reloads sceneSetsToReload, minus whatever sceneSetsToExcludeFromReload already
        /// covers - e.g. sceneSetsToReload = [Level1], sceneSetsToExcludeFromReload =
        /// [Essentials] refreshes Level 1's own content without touching Essentials.
        /// </summary>
        public void ReloadConfigured()
        {
            List<SimpleScene> toReload = SceneSet.Difference(sceneSetsToReload, sceneSetsToExcludeFromReload);
            SceneSetLoader.ReloadScenes(toReload);
        }

        /// <summary>True once a preload started from this component has finished loading and can be activated instantly.</summary>
        public bool IsPreloadReady => SceneSetLoader.IsPendingPreloadReady;

        /// <summary>How many scenes are currently loaded but unused, waiting for UnloadUnusedScenes.</summary>
        public int UnusedSceneCount => SceneSetLoader.UnusedSceneCount;

        /// <summary>True while a ReloadConfigured() call is mid-flight.</summary>
        public bool IsReloadInProgress => SceneSetLoader.IsReloadInProgress;

        private IReadOnlyList<SceneSet> ResolveSceneSets()
        {
            if (mode == SceneSetLoadMode.Single)
            {
                return sceneSet != null ? new[] { sceneSet } : Array.Empty<SceneSet>();
            }
            return sceneSets;
        }
    }
}