using UnityEngine;
using System.Threading.Tasks;

using GVLoadSystem.GVEditor;
using GVLoadSystem.Encryption;
using GVUtils.Attributes;
using System.Collections;







#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GVLoadSystem.Core
{
    internal enum LoadType { SYNC, ASYNC }
    [DefaultExecutionOrder(-100)]
    public class LoaderMono : MonoBehaviour
    {
        [Header("LoaderMono Settings")]
        [Tooltip("Automatically load values on Awake.")]
        [SerializeField] private bool loadOnAwake = true;
        [Tooltip("Save data when object is disabled. Note that this can cause issues if the object is disabled during scene unload, so use with caution.")]
        [SerializeField] private bool saveOnDisable = false;
        [Tooltip("Save data when application quits. Note that this can cause issues if the application is killed forcefully, so use with caution.")]
        [SerializeField] private bool saveOnQuit = true;
        [ShowIf("loadOnAwake || saveOnDisable || saveOnQuit",true)]
        [Tooltip("Loading type that can be choosed when loading/saving automatically. If automatic loading/saving is disabled, this setting has no effect.")]
        [SerializeField] private LoadType loadType = LoadType.SYNC;
        [SerializeField] private ALoader loader = new();

        void Awake()
        {
            // In builds the encryption settings are baked into the ALoader's serialized
            // fields (encryptionMethod + password) by GroupValuesProjectSettings.Save(),
            // so no runtime lookup is needed — the values are already correct.
            // In Editor Play Mode we do the same: the values were applied on last Save.

            if (loadOnAwake)
            {

                if (loadType == LoadType.SYNC)
                {
                    loader.LoadValues();
                }
                else
                {
                    StartCoroutine(LoadAsyncEnumerator());
                }

            }
        }
        IEnumerator LoadAsyncEnumerator()
        {
            Task loadTask = LoadDataAsync();
            yield return new WaitUntil(() => loadTask.IsCompleted);

            if (loadTask.IsFaulted)
                Debug.LogError("[LoaderMono] Load failed: " + loadTask.Exception);
        }
        IEnumerator SaveAsyncEnumerator()
        {
            Task saveTask = SaveDataAsync();
            yield return new WaitUntil(() => saveTask.IsCompleted);

            if (saveTask.IsFaulted)
                Debug.LogError("[LoaderMono] Save failed: " + saveTask.Exception);
        }
        void OnDisable()
        {
            if (saveOnDisable)
            {
                if (loadType == LoadType.SYNC)
                {
                    SaveData();
                }
                else
                {
                    StartCoroutine(SaveAsyncEnumerator());
                }
            }
            
        }
        void OnApplicationQuit()
        {
            if (saveOnQuit)
            {
                if (loadType == LoadType.SYNC)
                {
                    SaveData();
                }
                else
                {
                    StartCoroutine(SaveAsyncEnumerator());
                }
            }
               
        }

        // ── Load ──────────────────────────────────────────────────────────
        [ContextMenu("Load Data")]
        [Button("Load Data")]
        public void LoadData() => loader.LoadValues();
        public async Task LoadDataAsync() => await loader.LoadValuesAsync();
        public GroupValues LoadValues() => loader.LoadValues();

        // ── Save ──────────────────────────────────────────────────────────
        [Button("Save Data")]
        [ContextMenu("Save Data")]
        public void SaveData() => loader.SaveValues();
        public void SaveData(GroupValues values) => loader.SaveValues(values);
        public async Task SaveDataAsync() => await loader.SaveValuesAsync();

        // ── Reset ─────────────────────────────────────────────────────────
        [Button("Reset Data")]
        [ContextMenu("Reset Data")]
        public void ResetData() => loader.ResetToDefaults();

        // ── Misc ──────────────────────────────────────────────────────────
        public void RemoveLoadedValues() => loader.RemoveLoadedValues();
        public void ChangeAssetName(string newName) => loader.ChangeAssetName(newName);
        public T GetValue<T>(string key) => loader.GetValue<T>(key);
        public void SetValue<T>(string key, T value) => loader.SetValue(key, value);

        /// Called by GroupValuesProjectSettings.UpdateAllLoaders() to bake
        /// encryption settings into this component so they are saved with the scene/prefab.
        public void ApplySettings(EncryptionMethod method, string salt, bool backups, string subfolder)
        {
            loader.SetEncryptionSettings(method, salt);
            loader.SetBackupSettings(backups);
            loader.SetSaveSubfolder(subfolder);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("AutoFindPathByName")]
        [Button("AutoFindPath")]
        public void AutoFindPath() => loader.AutoResolveFromResources();
#endif
    }
#if UNITY_EDITOR


    [CustomEditor(typeof(LoaderMono))]
    public class LoaderMonoEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var target = (LoaderMono)this.target;
            var settings = GroupValuesProjectSettings.instance;
            var t = GVThemeManager.Current;

            // Toolbar row — action buttons + optional help button
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = t.buttonPrimary;
            if (GUILayout.Button("Load", GUILayout.Height(28)))
                target.LoadData();

            GUI.backgroundColor = t.buttonSuccess;
            if (GUILayout.Button("Save", GUILayout.Height(28)))
                target.SaveData();

            GUI.backgroundColor = t.buttonWarning;
            if (GUILayout.Button("Reset", GUILayout.Height(28)))
                target.ResetData();

            // Help button — shown if enabled in Project Settings
            if (settings == null || settings.showHelpButton)
            {
                GUI.backgroundColor = t.buttonNeutral;
                var helpStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                helpStyle.normal.textColor = t.textPrimary;
                if (GUILayout.Button("?", helpStyle,
                                      GUILayout.Width(28), GUILayout.Height(28)))
                    GroupValuesDocumentationWindow.OpenAtTarget("Loaders");
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            serializedObject.Update();

            // Draw all fields except m_Script
            var prop = serializedObject.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
            {
                if (prop.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();

        }

        GroupValues _lastGV;
        SerializedObject _fieldsSO;
        UnityEditorInternal.ReorderableList _fieldsList;

        void EnsureFieldsList(GroupValues gv)
        {
            if (_lastGV == gv && _fieldsList != null) return;
            _lastGV = gv;
            _fieldsSO = new SerializedObject(gv);
            _fieldsList = GroupValuesEditor.BuildFieldsList(_fieldsSO);
        }
    }

#endif
}