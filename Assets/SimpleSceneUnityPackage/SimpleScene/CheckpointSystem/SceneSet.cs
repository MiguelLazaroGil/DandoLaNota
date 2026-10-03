using System.Collections.Generic;
using UnityEngine;

namespace MLG.SimpleSceneTool.CheckpointSystem
{
    /// <summary>
    /// A named collection of scenes that can be loaded together. It is deliberately
    /// not a checkpoint: a set can represent a menu, a mission, a save-state, a
    /// streaming region, or any other game-specific configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneSet", menuName = "Simple Scene/Scene Set", order = 1)]
    public class SceneSet : ScriptableObject, ISimpleSceneGraphRoot
    {
        [SerializeField, Tooltip("Optional friendly name used by tools and logs. The asset name is used when this is empty.")]
        private string displayName;

        [SerializeField, Tooltip("When enabled, this set remains usable in the Editor but its scenes are omitted from the player build graph.")]
        private bool excludeFromBuild;

        [SerializeField]
        private List<SimpleScene> scenes = new List<SimpleScene>();

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName; }
        }

        public bool ExcludeFromBuild
        {
            get { return excludeFromBuild; }
        }

        public IList<SimpleScene> Scenes
        {
            get { return scenes; }
        }

        public SceneSet(List<SimpleScene> scenes)
        {
            this.scenes = scenes;
        }
        public IEnumerable<SimpleScene> GetSimpleScenesForBuild()
        {
            if (excludeFromBuild || scenes == null)
            {
                yield break;
            }

            foreach (SimpleScene scene in scenes)
            {
                if (scene != null)
                {
                    yield return scene;
                }
            }
        }
        /// <summary>
        /// Combines the scenes from multiple Scene Sets into one deduplicated list, in first-seen
        /// order, using each scene's build index as identity - so the same scene appearing in two
        /// different sets (or twice in one) only ends up in the result once. Used to "mix" several
        /// Scene Sets into a single load via SceneSetLoader.
        /// </summary>
        public static List<SimpleScene> CombineDistinctScenes(IEnumerable<SceneSet> sceneSets)
        {
            List<SimpleScene> combined = new List<SimpleScene>();
            if (sceneSets == null)
            {
                return combined;
            }

            HashSet<int> seenIndexes = new HashSet<int>();
            foreach (SceneSet sceneSet in sceneSets)
            {
                if (sceneSet == null)
                {
                    continue;
                }
                foreach (SimpleScene scene in sceneSet.Scenes)
                {
                    if (scene == null)
                    {
                        continue;
                    }
                    if (seenIndexes.Add(scene.Index))
                    {
                        combined.Add(scene);
                    }
                }
            }
            return combined;
        }
        /// <summary>
        /// Every scene in fromSets that is NOT also present in any of excludingSets, deduplicated
        /// (by build index) and in first-seen order - e.g. Difference(new[]{ level1 }, new[]{
        /// essentials }) gives you "Level 1, minus whatever Essentials already covers".
        /// </summary>
        public static List<SimpleScene> Difference(IEnumerable<SceneSet> fromSets, IEnumerable<SceneSet> excludingSets)
        {
            HashSet<int> excludedIndexes = new HashSet<int>();
            if (excludingSets != null)
            {
                foreach (SceneSet sceneSet in excludingSets)
                {
                    if (sceneSet == null)
                    {
                        continue;
                    }
                    foreach (SimpleScene scene in sceneSet.Scenes)
                    {
                        if (scene != null)
                        {
                            excludedIndexes.Add(scene.Index);
                        }
                    }
                }
            }

            List<SimpleScene> result = new List<SimpleScene>();
            HashSet<int> seenIndexes = new HashSet<int>();
            if (fromSets != null)
            {
                foreach (SceneSet sceneSet in fromSets)
                {
                    if (sceneSet == null)
                    {
                        continue;
                    }
                    foreach (SimpleScene scene in sceneSet.Scenes)
                    {
                        if (scene == null || excludedIndexes.Contains(scene.Index))
                        {
                            continue;
                        }
                        if (seenIndexes.Add(scene.Index))
                        {
                            result.Add(scene);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>Convenience overload for the common single-set-minus-single-set case.</summary>
        public static List<SimpleScene> Difference(SceneSet from, SceneSet excluding)
        {
            return Difference(
                from != null ? new[] { from } : null,
                excluding != null ? new[] { excluding } : null);
        }

        /// <summary>Instance convenience for Difference(this, excluding) - "this Scene Set, minus excluding".</summary>
        public List<SimpleScene> Difference(SceneSet excluding)
        {
            return Difference(this, excluding);
        }


#if UNITY_EDITOR
        public void ReplaceScenes(IEnumerable<SimpleScene> newScenes)
        {
            scenes = newScenes == null
                ? new List<SimpleScene>()
                : new List<SimpleScene>(newScenes);
            UnityEditor.EditorUtility.SetDirty(this);
        }
        public void SetDisplayName(string name)
        {
            this.displayName = name;
        }
        public void SetExcludeFromBuild(bool exclude)
        {
            this.excludeFromBuild = true;
        }
        private void OnValidate()
        {
            if (scenes == null)
            {
                scenes = new List<SimpleScene>();
            }
        }
#endif
    }
}