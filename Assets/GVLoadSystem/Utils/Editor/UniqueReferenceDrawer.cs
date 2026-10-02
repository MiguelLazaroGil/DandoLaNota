
#if UNITY_EDITOR
using GVUtils.Attributes;
using UnityEditor;
using UnityEngine;

namespace GVUtils.GVEditor
{

    [CustomPropertyDrawer(typeof(UniqueReferenceAttribute))]
    public class UniqueReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();

            var newValue = EditorGUI.ObjectField(
                position,
                label,
                property.objectReferenceValue,
                fieldInfo.FieldType,
                false
            );

            if (EditorGUI.EndChangeCheck())
            {
                if (IsReferenceUsed(newValue, property.serializedObject.targetObject, property))
                {
                    Debug.LogWarning("Reference already used by another asset.");
                }
                else
                {
                    property.objectReferenceValue = newValue;
                }
            }

            EditorGUI.EndProperty();
        }

        bool IsReferenceUsed(Object reference, Object current, SerializedProperty property)
        {
            if (reference == null) return false;

            var guids = AssetDatabase.FindAssets($"t:{current.GetType().Name}");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, current.GetType());

                if (asset == current) continue;

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(property.propertyPath);

                if (prop != null && prop.objectReferenceValue == reference)
                    return true;
            }

            return false;
        }
    }
}
#endif