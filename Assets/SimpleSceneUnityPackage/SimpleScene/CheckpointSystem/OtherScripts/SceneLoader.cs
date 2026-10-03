using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MLG.SimpleSceneTool.CheckpointSystem
{
    /// <summary>
    /// Low-level multi-scene loading primitive, operating purely on build indexes. Internal -
    /// SceneSetLoader is the only class anything outside this file should call for gameplay
    /// scene transitions.
    ///
    /// Unity cannot discard a scene load that was started with allowSceneActivation = false
    /// before it has activated at least once - see CancelPendingLoad's remarks.
    /// </summary>
    internal static class SceneLoader
    {
        private sealed class PendingBatch
        {
            public readonly List<AsyncOperation> Operations = new List<AsyncOperation>();
            public readonly List<int> SceneIndexes = new List<int>();
            public Action OnActivated;
        }

        private static PendingBatch pendingBatch;

        public static bool HasPendingLoad => pendingBatch != null;

        /// <summary>
        /// True once every scene in the pending batch has finished loading data and is sitting
        /// at the activation gate - ready for ActivatePendingLoad to commit with no further wait.
        /// </summary>
        public static bool IsPendingLoadReady
        {
            get
            {
                if (pendingBatch == null)
                {
                    return false;
                }
                foreach (AsyncOperation operation in pendingBatch.Operations)
                {
                    if (operation.progress < 0.9f)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Starts loading sceneBuildIndexes additively. With waitForActivation false, they're
        /// activated immediately (onActivated still fires, just on the next frame or two - scene
        /// activation is never truly synchronous in Unity). With waitForActivation true, they
        /// load in the background and stay inert until ActivatePendingLoad or CancelPendingLoad
        /// is called.
        /// </summary>
        public static bool LoadScenes(IList<int> sceneBuildIndexes, bool waitForActivation, Action onActivated = null)
        {
            if (sceneBuildIndexes == null || sceneBuildIndexes.Count == 0)
            {
                onActivated?.Invoke();
                return true;
            }

            if (HasPendingLoad)
            {
                Debug.LogError("A scene batch is already pending activation. Call ActivatePendingLoad or CancelPendingLoad first.");
                return false;
            }

            PendingBatch batch = new PendingBatch { OnActivated = onActivated };
            foreach (int sceneIndex in sceneBuildIndexes)
            {
                if (!IsBuildScene(sceneIndex))
                {
                    Debug.LogError("Tried to load a scene that is not in Build Settings: " + sceneIndex);
                    continue;
                }
                AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Additive);
                if (operation == null)
                {
                    Debug.LogError("Unity could not start loading scene " + sceneIndex + ".");
                    continue;
                }
                operation.allowSceneActivation = false;
                batch.Operations.Add(operation);
                batch.SceneIndexes.Add(sceneIndex);
            }

            pendingBatch = batch;

            if (!waitForActivation)
            {
                ActivatePendingLoad();
            }
            return true;
        }

        /// <summary>Commits the pending batch: opens the activation gate, waits for it to finish, then notifies.</summary>
        public static void ActivatePendingLoad()
        {
            if (pendingBatch == null)
            {
                return;
            }
            PendingBatch batch = pendingBatch;
            pendingBatch = null;
            Runner.Instance.StartCoroutine(ActivateAndNotify(batch));
        }

        private static IEnumerator ActivateAndNotify(PendingBatch batch)
        {
            foreach (AsyncOperation operation in batch.Operations)
            {
                operation.allowSceneActivation = true;
            }
            foreach (AsyncOperation operation in batch.Operations)
            {
                while (!operation.isDone)
                {
                    yield return null;
                }
            }
            batch.OnActivated?.Invoke();
        }

        /// <summary>
        /// Abandons the pending batch. Unity provides no way to discard a scene load started
        /// with allowSceneActivation = false before it activates, so this lets every pending
        /// scene finish activating (running its Awake/OnEnable/Start for one frame) and then
        /// immediately unloads it. There is no cleaner option available.
        /// </summary>
        public static void CancelPendingLoad(Action onCancelled = null)
        {
            if (pendingBatch == null)
            {
                onCancelled?.Invoke();
                return;
            }
            PendingBatch batch = pendingBatch;
            pendingBatch = null;
            Runner.Instance.StartCoroutine(ActivateThenUnload(batch, onCancelled));
        }

        private static IEnumerator ActivateThenUnload(PendingBatch batch, Action onCancelled)
        {
            foreach (AsyncOperation operation in batch.Operations)
            {
                operation.allowSceneActivation = true;
            }
            foreach (AsyncOperation operation in batch.Operations)
            {
                while (!operation.isDone)
                {
                    yield return null;
                }
            }
            foreach (int sceneIndex in batch.SceneIndexes)
            {
                if (!IsBuildScene(sceneIndex))
                {
                    continue;
                }
                Scene scene = SceneManager.GetSceneByBuildIndex(sceneIndex);
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(scene);
                }
            }
            onCancelled?.Invoke();
        }

        public static void UnloadScenes(IList<int> sceneBuildIndexes)
        {
            if (sceneBuildIndexes == null)
            {
                return;
            }
            foreach (int sceneIndex in sceneBuildIndexes)
            {
                if (IsBuildScene(sceneIndex))
                {
                    SceneManager.UnloadSceneAsync(sceneIndex);
                }
            }
        }

        /// <summary>Same as UnloadScenes, but waits for every unload to actually finish before notifying - used by ReloadScenes.</summary>
        public static void UnloadScenesAndNotify(IList<int> sceneBuildIndexes, Action onUnloaded)
        {
            if (sceneBuildIndexes == null || sceneBuildIndexes.Count == 0)
            {
                onUnloaded?.Invoke();
                return;
            }
            Runner.Instance.StartCoroutine(UnloadAndNotify(sceneBuildIndexes, onUnloaded));
        }

        private static IEnumerator UnloadAndNotify(IList<int> sceneBuildIndexes, Action onUnloaded)
        {
            List<AsyncOperation> operations = new List<AsyncOperation>();
            foreach (int sceneIndex in sceneBuildIndexes)
            {
                if (!IsBuildScene(sceneIndex))
                {
                    continue;
                }
                Scene scene = SceneManager.GetSceneByBuildIndex(sceneIndex);
                if (scene.IsValid() && scene.isLoaded)
                {
                    AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                    if (operation != null)
                    {
                        operations.Add(operation);
                    }
                }
            }
            foreach (AsyncOperation operation in operations)
            {
                while (!operation.isDone)
                {
                    yield return null;
                }
            }
            onUnloaded?.Invoke();
        }

        private static bool IsBuildScene(int sceneIndex)
        {
            return sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings;
        }

        /// <summary>Hidden, auto-created MonoBehaviour that exists purely so these static methods can run coroutines.</summary>
        private sealed class Runner : MonoBehaviour
        {
            private static Runner instance;

            public static Runner Instance
            {
                get
                {
                    if (instance == null)
                    {
                        GameObject runnerObject = new GameObject("SceneLoader (runtime)");
                        runnerObject.hideFlags = HideFlags.HideInHierarchy;
                        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
                        instance = runnerObject.AddComponent<Runner>();
                    }
                    return instance;
                }
            }
        }
    }
}