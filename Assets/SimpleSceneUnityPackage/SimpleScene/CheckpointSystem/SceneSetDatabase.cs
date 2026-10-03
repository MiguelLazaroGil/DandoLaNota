using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MLG.SimpleSceneTool.CheckpointSystem
{
    /// <summary>
    /// Optional registry for scene sets. It provides an initial set at runtime and
    /// acts as an automatically discovered graph root, which makes it suitable for
    /// Resources or other bootstrapped loading schemes.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneSetDatabase", menuName = "Simple Scene/Scene Set Database", order = 2)]
    public class SceneSetDatabase : ScriptableObject, ISimpleSceneAutoGraphRoot
    {


        [SerializeField, Tooltip("Disable this for a database used only by editor tests. Its listed scene sets are omitted from the player build graph.")]
        private bool excludeFromBuild;


        [SerializeField]
        private List<SceneSet> sceneSets = new List<SceneSet>();

#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private bool expandSceneSetsInInspector;
#endif

        public bool IncludeInSimpleSceneBuild
        {
            get { return !excludeFromBuild; }
        }

        public IList<SceneSet> SceneSets
        {
            get { return sceneSets; }
        }

        public bool Contains(SceneSet sceneSet)
        {
            return sceneSet != null && sceneSets.Contains(sceneSet);
        }

        public bool AddSceneSet(SceneSet sceneSet)
        {
            if (sceneSet == null || Contains(sceneSet))
            {
                return false;
            }

            sceneSets.Add(sceneSet);
            return true;
        }

        public int AddSceneSets(IEnumerable<SceneSet> sets)
        {
            if (sets == null)
            {
                return 0;
            }

            int addedCount = 0;
            foreach (SceneSet sceneSet in sets)
            {
                if (AddSceneSet(sceneSet))
                {
                    addedCount++;
                }
            }
            return addedCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sceneSets == null)
            {
                sceneSets = new List<SceneSet>();
            }
        }

#endif
    }
}