
#if UNITY_EDITOR

using UnityEditor;
#endif
using UnityEngine;
namespace GVUtils.Attributes
{
    public class ReadOnlyAttribute : PropertyAttribute
    {
        public ReadOnlyAttribute() { }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    internal class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false; // Desactiva la edici�n
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true; // Vuelve a activar para otros campos
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

#endif
}