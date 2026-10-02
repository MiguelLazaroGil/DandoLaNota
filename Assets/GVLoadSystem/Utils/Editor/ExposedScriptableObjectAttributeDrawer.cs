#if UNITY_EDITOR

using GVUtils.Attributes;
using UnityEditor;
using UnityEngine;
namespace GVUtils.GVEditor
{

    [CustomPropertyDrawer(typeof(ExposedScriptableObjectAttribute))]
    public class ExposedScriptableObjectAttributeDrawer : PropertyDrawer
    {
        Editor _editor;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property, label, true);

            if (property.objectReferenceValue == null) return;

            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, GUIContent.none);

            if (!property.isExpanded) return;

            EditorGUI.indentLevel++;
            Editor.CreateCachedEditor(property.objectReferenceValue, null, ref _editor);

            if (_editor != null)
            {
                // Draw without the m_Script row
                _editor.serializedObject.Update();
                var prop = _editor.serializedObject.GetIterator();
                prop.NextVisible(true);
                while (prop.NextVisible(false))
                {
                    if (prop.propertyPath == "m_Script") continue;
                    EditorGUILayout.PropertyField(prop, true);
                }
                _editor.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.indentLevel--;
        }
    }
}
#endif