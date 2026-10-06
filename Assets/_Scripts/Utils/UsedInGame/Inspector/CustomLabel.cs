#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class CustomLabelAttribute : PropertyAttribute
{
    public string label;
    public bool hideLabel;

    /// <summary>
    /// Si pones label vacío y hideLabel = true, no se dibuja etiqueta.
    /// </summary>
    public CustomLabelAttribute(string label = "", bool applyToCollection = true) : base (applyToCollection)
    {
        this.label = label;
        this.hideLabel = false;
    }
    public CustomLabelAttribute()
    {
        hideLabel = true;
    }
}

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(CustomLabelAttribute))]
public class CustomLabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CustomLabelAttribute custom = (CustomLabelAttribute)attribute;

        // Si hideLabel es true, dibuja solo el campo
        if (custom.hideLabel)
        {
            EditorGUI.PropertyField(position, property, GUIContent.none, true);
        }
        else
        {
            // Si hay un label personalizado, usarlo
            GUIContent newLabel = string.IsNullOrEmpty(custom.label) ? label : new GUIContent(custom.label);
            EditorGUI.PropertyField(position, property, newLabel, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif