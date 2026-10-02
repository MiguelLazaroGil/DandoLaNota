#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using GVLoadSystem.Core;

namespace GVLoadSystem.GVEditor
{

    internal class GroupValuesIconAssigner : AssetPostprocessor
    {

        const string Key = "GroupValuesIconsAssigned";

        [InitializeOnLoadMethod]

        static void ProjectStartup()
        {

            if (SessionState.GetBool(Key, false))
                return;

            SessionState.SetBool(Key, true);

            EditorApplication.delayCall += AssignIcons;
        }

        static void AssignIcons()
        {
            // Cargar el Sprite
            Sprite icon = FindIcon("GroupValuesIcon");
            if (icon == null)
            {

                return;
            }
            Sprite iconT = FindIcon("GroupValuesTemplateIcon");
            if (iconT == null)
            {
                return;
            }
            // Cargar todos los GroupValues en Resources
            GroupValues[] gvs = Resources.LoadAll<GroupValues>("");
#if LOG_LOADSYSTEM
        Debug.Log("Asigning icons to " + gvs.Length + " GroupValues");
#endif

            foreach (var gv in gvs)
            {
                if (gv == null) continue;
#if LOG_LOADSYSTEM
            Debug.Log("Asigning icon to: " + gv.name);
#endif

                EditorGUIUtility.SetIconForObject(gv, icon.texture);
                EditorUtility.SetDirty(gv);
            }
            GroupValuesTemplate[] gvTs = Resources.LoadAll<GroupValuesTemplate>("");
#if LOG_LOADSYSTEM
        Debug.Log("Asigning icons to " + gvTs.Length + " GroupValuesTemplate");
#endif
            foreach (var gvt in gvTs)
            {
                if (gvt == null) continue;
#if LOG_LOADSYSTEM
            Debug.Log("Asigning icon to: " + gvt.name);
#endif

                EditorGUIUtility.SetIconForObject(gvt, iconT.texture);
                EditorUtility.SetDirty(gvt);
            }

            AssetDatabase.SaveAssets();
        }
        private static Sprite FindIcon(string key)
        {
            string guid = AssetDatabase.FindAssets($"{key} t:Sprite").FirstOrDefault();

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("Could not find .png GroupValuesIcon");
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Load el Sprite
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (icon == null)
            {
                Debug.LogError("Error loading icon as Sprite from: " + path);
                return null;
            }
            return icon;
        }
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {

            // Cargar el Sprite
            Sprite icon = FindIcon("GroupValues");
            if (icon == null)
            {

                return;
            }
            Sprite iconT = FindIcon("GroupValuesTemplate");
            if (iconT == null)
            {
                return;
            }
            foreach (var path in importedAssets)
            {

                var asset = AssetDatabase.LoadAssetAtPath<GroupValues>(path);

                if (asset != null)
                {
                    EditorGUIUtility.SetIconForObject(asset, icon.texture);
                    EditorUtility.SetDirty(asset);
                    continue;
                }
                var assetT = AssetDatabase.LoadAssetAtPath<GroupValuesTemplate>(path);
                if (assetT != null)
                {
                    EditorGUIUtility.SetIconForObject(assetT, iconT.texture);
                    EditorUtility.SetDirty(assetT);
                }


            }


        }
    }
}
#endif