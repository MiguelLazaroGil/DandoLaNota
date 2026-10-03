using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MLG.SimpleSceneTool.CheckpointSystem
{
    /// <summary>
    /// The single entry point for loading Scene Sets at runtime. SceneLoader (internal) is a
    /// low-level primitive this class builds on - nothing outside this file should touch
    /// SceneManager directly for gameplay scene transitions.
    ///
    /// Supports loading a single Scene Set, or "mixing" several together in one call - Load
    /// accepts either. When multiple Scene Sets are given, their scenes are combined via
    /// SceneSet.CombineDistinctScenes (duplicates removed by build index) before loading.
    ///
    /// Supports a genuine "preload, then decide" workflow: call Load(..., waitForActivation:
    /// true) to load scenes in the background without showing them yet, then either
    /// ActivatePendingPreload() to commit or CancelPendingPreload() to abandon it and stay
    /// exactly where you were. Calling Load again while one is already pending automatically
    /// cancels the old one first.
    ///
    /// Supports separating "load the new scenes" from "unload what's no longer needed": pass
    /// unloadUnusedImmediately: false and the leftover scenes stay loaded (just unused) until
    /// you call UnloadUnusedScenes() yourself.
    ///
    /// Supports reloading a specific subset of currently-loaded scenes without touching
    /// anything else - see ReloadScenes. Combined with SceneSet.Difference, this is how you
    /// reload "Level 1" without also reloading its shared "Essentials" scenes:
    ///     SceneSetLoader.ReloadScenes(SceneSet.Difference(level1, essentials));
    ///
    /// One Unity limitation this can't avoid: cancelling a pending preload still briefly
    /// activates its scenes before unloading them (Unity has no way to discard a scene load
    /// that hasn't activated at least once) - see SceneLoader.CancelPendingLoad for why.
    /// </summary>
    public static class SceneSetLoader
    {
        private sealed class PendingTransition
        {
            public IReadOnlyList<SceneSet> TargetSceneSets;
            public List<int> DesiredSceneIndexes;
            public List<int> ScenesToUnload;
            public bool UnloadImmediately;
            public Action OnActivated;
        }

        private static IReadOnlyList<SceneSet> lastLoadedSceneSets = Array.Empty<SceneSet>();
        private static PendingTransition pendingTransition;
        private static readonly HashSet<int> unusedSceneIndexes = new HashSet<int>();
        private static bool reloadInProgress;

        /// <summary>The Scene Set(s) most recently committed - one entry for a normal load, several for a mixed one.</summary>
        public static IReadOnlyList<SceneSet> LastLoadedSceneSets => lastLoadedSceneSets;

        /// <summary>The Scene Set(s) currently being preloaded, or an empty list if nothing is pending.</summary>
        public static IReadOnlyList<SceneSet> PendingSceneSets => pendingTransition?.TargetSceneSets ?? Array.Empty<SceneSet>();

        public static bool HasPendingPreload => pendingTransition != null;

        /// <summary>True once the pending preload has finished loading and can be activated with no further wait.</summary>
        public static bool IsPendingPreloadReady => HasPendingPreload && SceneLoader.IsPendingLoadReady;

        /// <summary>How many scenes are currently loaded but no longer wanted, waiting for UnloadUnusedScenes.</summary>
        public static int UnusedSceneCount => unusedSceneIndexes.Count;

        /// <summary>True while a ReloadScenes call is mid-flight.</summary>
        public static bool IsReloadInProgress => reloadInProgress;

        /// <summary>Loads a single Scene Set. Equivalent to Load(new[] { sceneSet }, ...).</summary>
        public static bool Load(
            SceneSet sceneSet,
            bool waitForActivation = false,
            Action onActivated = null,
            bool unloadUnusedImmediately = true)
        {
            return Load(sceneSet != null ? new[] { sceneSet } : null, waitForActivation, onActivated, unloadUnusedImmediately);
        }

        /// <summary>
        /// Loads the combined scenes of one or more Scene Sets (duplicates removed - see
        /// SceneSet.CombineDistinctScenes). With waitForActivation false (the default), this
        /// happens as soon as Unity can manage it - nothing currently loaded is touched until
        /// the new scenes are ready. With waitForActivation true, the new scenes load in the
        /// background and nothing visible changes until ActivatePendingPreload is called;
        /// onActivated, if provided, fires once the swap is actually complete either way.
        ///
        /// With unloadUnusedImmediately true (the default), scenes no longer in the desired set
        /// are unloaded as part of that same commit. With false, they're left loaded - tracked
        /// as "unused" until you call UnloadUnusedScenes() to actually free them.
        /// </summary>
        public static bool Load(
            IReadOnlyList<SceneSet> sceneSets,
            bool waitForActivation = false,
            Action onActivated = null,
            bool unloadUnusedImmediately = true)
        {
            if (HasPendingPreload)
            {
                CancelPendingPreload();
            }

            List<int> desiredSceneIndexes;
            if (!TryGetSceneIndexes(sceneSets, out desiredSceneIndexes))
            {
                return false;
            }

            List<int> loadedSceneIndexes = GetLoadedSceneIndexes();
            List<int> scenesToLoad = Difference(desiredSceneIndexes, loadedSceneIndexes);
            List<int> scenesToUnload = Difference(loadedSceneIndexes, desiredSceneIndexes);

            PendingTransition transition = new PendingTransition
            {
                TargetSceneSets = sceneSets,
                DesiredSceneIndexes = desiredSceneIndexes,
                ScenesToUnload = scenesToUnload,
                UnloadImmediately = unloadUnusedImmediately,
                OnActivated = onActivated
            };
            pendingTransition = transition;

            bool started = SceneLoader.LoadScenes(scenesToLoad, waitForActivation, () => Commit(transition));
            if (!started)
            {
                pendingTransition = null;
                return false;
            }
            return true;
        }

        /// <summary>Commits the pending preload: activates its scenes, then unloads (or defers) whatever's no longer needed.</summary>
        public static void ActivatePendingPreload()
        {
            if (!HasPendingPreload)
            {
                Debug.LogError("There is no pending preload to activate.");
                return;
            }
            SceneLoader.ActivatePendingLoad();
        }

        /// <summary>
        /// Abandons the pending preload and leaves the currently active scenes untouched - see
        /// this class's remarks for the one Unity-imposed caveat.
        /// </summary>
        public static void CancelPendingPreload(Action onCancelled = null)
        {
            if (!HasPendingPreload)
            {
                onCancelled?.Invoke();
                return;
            }
            pendingTransition = null;
            SceneLoader.CancelPendingLoad(onCancelled);
        }

        public static bool ReloadLast(bool waitForActivation = false, Action onActivated = null, bool unloadUnusedImmediately = true)
        {
            if (lastLoadedSceneSets == null || lastLoadedSceneSets.Count == 0)
            {
                Debug.LogError("No scene set has been loaded yet.");
                return false;
            }
            return Load(lastLoadedSceneSets, waitForActivation, onActivated, unloadUnusedImmediately);
        }

        /// <summary>
        /// Unloads every scene left loaded-but-unused by a previous Load(...,
        /// unloadUnusedImmediately: false) call, as long as nothing since has asked for it
        /// again. Safe to call any time, including when nothing is actually pending (a no-op).
        /// </summary>
        public static void UnloadUnusedScenes()
        {
            if (unusedSceneIndexes.Count == 0)
            {
                return;
            }
            List<int> toUnload = new List<int>(unusedSceneIndexes);
            unusedSceneIndexes.Clear();
            SceneLoader.UnloadScenes(toUnload);
        }

        /// <summary>Convenience overload taking Scene Sets directly - reloads their combined scenes (see SceneSet.CombineDistinctScenes).</summary>
        public static bool ReloadScenes(IReadOnlyList<SceneSet> sceneSets, Action onReloaded = null)
        {
            return ReloadScenes(SceneSet.CombineDistinctScenes(sceneSets), onReloaded);
        }

        /// <summary>
        /// Unloads and immediately reloads exactly the given scenes - scenes currently loaded
        /// for any other reason (a different Scene Set, or previously deferred via
        /// unloadUnusedImmediately: false) are left completely untouched. Scenes in the list
        /// that aren't currently loaded are simply loaded, no unload needed. LastLoadedSceneSets
        /// is unchanged - a reload refreshes content, it doesn't represent moving to a different
        /// Scene Set combination.
        ///
        /// Typical use: SceneSetLoader.ReloadScenes(SceneSet.Difference(level1, essentials))
        /// resets everything Level 1-specific without touching the shared Essentials scenes.
        /// </summary>
        public static bool ReloadScenes(IReadOnlyList<SimpleScene> scenesToReload, Action onReloaded = null)
        {
            if (reloadInProgress)
            {
                Debug.LogError("A reload is already in progress. Wait for it to finish before starting another.");
                return false;
            }
            if (HasPendingPreload)
            {
                CancelPendingPreload();
            }

            List<int> targetIndexes;
            if (!TryGetSceneIndexesFromScenes(scenesToReload, out targetIndexes))
            {
                return false;
            }

            foreach (int index in targetIndexes)
            {
                unusedSceneIndexes.Remove(index); // actively reloading it, so it's no longer "unused"
            }

            List<int> loadedSceneIndexes = GetLoadedSceneIndexes();
            List<int> currentlyLoadedTargets = new List<int>();
            foreach (int index in targetIndexes)
            {
                if (loadedSceneIndexes.Contains(index))
                {
                    currentlyLoadedTargets.Add(index);
                }
            }

            reloadInProgress = true;
            SceneLoader.UnloadScenesAndNotify(currentlyLoadedTargets, () =>
            {
                SceneLoader.LoadScenes(targetIndexes, false, () =>
                {
                    reloadInProgress = false;
                    onReloaded?.Invoke();
                });
            });
            return true;
        }

        private static void Commit(PendingTransition transition)
        {
            if (pendingTransition != transition)
            {
                return; // superseded by a newer Load()/cancel before this one got here
            }

            if (transition.UnloadImmediately)
            {
                SceneLoader.UnloadScenes(transition.ScenesToUnload);
            }
            else
            {
                foreach (int index in transition.ScenesToUnload)
                {
                    unusedSceneIndexes.Add(index);
                }
            }

            // Anything this transition actually wants is, by definition, not unused anymore -
            // even if an earlier transition parked it in the pool.
            foreach (int index in transition.DesiredSceneIndexes)
            {
                unusedSceneIndexes.Remove(index);
            }

            lastLoadedSceneSets = transition.TargetSceneSets;
            pendingTransition = null;
            transition.OnActivated?.Invoke();
        }

        private static bool TryGetSceneIndexes(IReadOnlyList<SceneSet> sceneSets, out List<int> indexes)
        {
            indexes = new List<int>();
            if (sceneSets == null || sceneSets.Count == 0)
            {
                Debug.LogError("Cannot load an empty combination of Scene Sets.");
                return false;
            }

            foreach (SceneSet sceneSet in sceneSets)
            {
                if (sceneSet == null)
                {
                    Debug.LogError("One of the Scene Sets to load is null.");
                    return false;
                }
            }

            List<SimpleScene> combinedScenes = SceneSet.CombineDistinctScenes(sceneSets);
            foreach (SimpleScene scene in combinedScenes)
            {
                int index = scene.Index;
                if (index < 0 || index >= SceneManager.sceneCountInBuildSettings)
                {
                    Debug.LogError("One of the combined Scene Sets contains a scene that is not in Build Settings.");
                    return false;
                }
                indexes.Add(index);
            }

            if (indexes.Count == 0)
            {
                Debug.LogError(sceneSets.Count > 1
                    ? "The combined Scene Sets have no scenes."
                    : "Scene Set '" + sceneSets[0].DisplayName + "' has no scenes.");
                return false;
            }
            return true;
        }

        private static bool TryGetSceneIndexesFromScenes(IReadOnlyList<SimpleScene> scenes, out List<int> indexes)
        {
            indexes = new List<int>();
            if (scenes == null || scenes.Count == 0)
            {
                Debug.LogError("Cannot reload an empty scene list.");
                return false;
            }

            HashSet<int> seen = new HashSet<int>();
            foreach (SimpleScene scene in scenes)
            {
                if (scene == null)
                {
                    continue;
                }
                int index = scene.Index;
                if (index < 0 || index >= SceneManager.sceneCountInBuildSettings)
                {
                    Debug.LogError("One of the scenes to reload is not in Build Settings.");
                    return false;
                }
                if (seen.Add(index))
                {
                    indexes.Add(index);
                }
            }

            if (indexes.Count == 0)
            {
                Debug.LogError("Cannot reload an empty scene list.");
                return false;
            }
            return true;
        }

        private static List<int> GetLoadedSceneIndexes()
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.buildIndex >= 0)
                {
                    indexes.Add(scene.buildIndex);
                }
            }
            return indexes;
        }

        private static List<int> Difference(List<int> first, List<int> second)
        {
            HashSet<int> secondSet = new HashSet<int>(second);
            List<int> result = new List<int>();
            foreach (int item in first)
            {
                if (!secondSet.Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }
    }
}