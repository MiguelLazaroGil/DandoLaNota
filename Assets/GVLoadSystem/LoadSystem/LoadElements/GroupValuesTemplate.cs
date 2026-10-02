using System.Collections.Generic;
using GVLoadSystem.Core;
using GVUtils.Attributes;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace GVLoadSystem
{
    [CreateAssetMenu(menuName = "LoadSystem/GroupValuesTemplate")]
    public class GroupValuesTemplate : ScriptableObject
    {
        [UniqueReference]
        public GroupValues groupValuesReference;
        
        public List<GVField> fields { get => defaultFields; }

        [SerializeField] private List<GVField> defaultFields;

        [Button("Set default values from reference")]
        [ContextMenu("Set default values from reference")]
        public void SetDefaultValuesInTemplate()
        {
            if (groupValuesReference == null)
            {
                Debug.LogWarning("Group values reference is null");
                return;
            }

            if (defaultFields == null) defaultFields = new List<GVField>();

            // Build lookup of existing template fields and entries
            var existingFields = new Dictionary<string, GVField>();
            foreach (var f in defaultFields)
                existingFields[f.fieldName] = f;

            var newFields = new List<GVField>();

            foreach (var gvField in groupValuesReference.fields)
            {
                var existingField = existingFields.TryGetValue(gvField.fieldName, out var ef) ? ef : null;
                var mergedEntries = new List<GVEntry>();

                // Build lookup of existing entries in this field
                var existingEntries = new Dictionary<string, GVEntry>();
                if (existingField != null)
                    foreach (var e in existingField.entries)
                        existingEntries[e.name] = e;

                foreach (var gvEntry in gvField.entries)
                {
                    if (existingEntries.TryGetValue(gvEntry.name, out var existing) &&
                        existing.type == gvEntry.type)
                    {
                        // Entry exists with same type — keep existing value
                        mergedEntries.Add(existing.Clone());
                    }
                    else
                    {
                        // New entry or type changed — use GV value as default
                        mergedEntries.Add(gvEntry.Clone());
                    }
                }

                newFields.Add(new GVField { fieldName = gvField.fieldName, entries = mergedEntries });
            }

            defaultFields = newFields;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
#endif
        }

        public void SetDefaultValuesInSO()
        {

            if (groupValuesReference == null)
            {
                Debug.LogError("Target GroupValues is null");
                return;
            }

            if (defaultFields == null || defaultFields.Count == 0)
            {
                Debug.LogWarning("No default fields set in template. Run SetDefaultValuesInTemplate first.");
                return;
            }

            groupValuesReference.fields = new List<GVField>();
            foreach (var field in defaultFields)
            {
                groupValuesReference.fields.Add(field.Clone());
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(groupValuesReference);
            AssetDatabase.SaveAssets();
#endif
#if LOG_LOADSYSTEM
        Debug.Log($"Applied template defaults to {groupValuesReference.name}");
#endif
        }
        //The template only gives values, it is read only
        public T GetValue<T>(string field, string name)
        {
            var f = defaultFields.Find(f => f.fieldName == field);
            var entry = f?.entries.Find(e => e.name == name);
            return entry != null ? (T)entry.value.GetValue() : default;
        }

        public GroupValuesTemplate Clone()
        {
            var clone = ScriptableObject.CreateInstance<GroupValuesTemplate>();

            clone.CreateFields();
            clone.groupValuesReference = groupValuesReference;
            foreach (var field in fields)
            {
                clone.fields.Add(field.Clone());
            }

            return clone;
        }
        public void CopyFrom(GroupValuesTemplate other)
        {

            defaultFields.Clear();
            groupValuesReference = other.groupValuesReference;
            foreach (var field in other.fields)
            {
                fields.Add(field.Clone());
            }


        }
        public void CreateFields()
        {
            defaultFields = new List<GVField>();
        }
        public void ResetFields()
        {

            foreach (var field in fields)
                foreach (var entry in field.entries)
                    entry.value = GVValueFactory.Create(entry.type);

        }
    }
}