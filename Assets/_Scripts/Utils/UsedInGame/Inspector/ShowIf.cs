
#if UNITY_EDITOR

using UnityEditor;
#endif
using UnityEngine;

public class ShowIfAttribute : PropertyAttribute
{

    public string fieldName;
    public object expectedValue;
    public bool comparison;

#if UNITY_6000_0_OR_NEWER
    public ShowIfAttribute(string fieldName, bool expectedValue = true) : base(applyToCollection: true)
    {
#else
    public ShowIfAttribute(string fieldName, bool expectedValue = true)
    {
#endif
        this.fieldName = fieldName;
        this.expectedValue = expectedValue;
        this.comparison = true;
    }
#if UNITY_6000_0_OR_NEWER
    public ShowIfAttribute(string fieldName, object expectedValue) : base(applyToCollection: true)
    {
#else
    public ShowIfAttribute(string fieldName, object expectedValue)
    {
#endif
        this.fieldName = fieldName;
        this.expectedValue = expectedValue;
        this.comparison = true;
    }
}

public class HideIfAttribute : ShowIfAttribute
{
    public HideIfAttribute(string booleanFieldName) : base(booleanFieldName, true)
    {
        this.comparison = false;

    }
    public HideIfAttribute(string booleanFieldName, object expectedValue) : base(booleanFieldName, expectedValue)
    {
        this.comparison = false;
    }
}
#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(HideIfAttribute),true)] 
internal class HideIfDrawer : ShowIfDrawer
{
}
[CustomPropertyDrawer(typeof(ShowIfAttribute),true)]
internal class ShowIfDrawer : PropertyDrawer
{
    bool isVisible = true;
    private SerializedProperty FindRelativeProperty(SerializedProperty property, string fieldName)
    {
        string path = property.propertyPath;

        // Remove last part (the current field)
        int lastDotIndex = path.LastIndexOf('.');
        if (lastDotIndex < 0)
        {
            // root level
            return property.serializedObject.FindProperty(fieldName);
        }

        string parentPath = path.Substring(0, lastDotIndex);

        // Handle arrays properly (important!)
        // parentPath could be: items.Array.data[0]
        string fullPath = parentPath + "." + fieldName;

        SerializedProperty result = property.serializedObject.FindProperty(fullPath);

        if (result != null)
            return result;

        //Fallback: walk up the hierarchy (VERY important for lists)
        while (lastDotIndex >= 0)
        {
            parentPath = parentPath.Substring(0, lastDotIndex);
            fullPath = parentPath + "." + fieldName;

            result = property.serializedObject.FindProperty(fullPath);
            if (result != null)
                return result;

            lastDotIndex = parentPath.LastIndexOf('.');
        }

        // final fallback: global search
        return property.serializedObject.FindProperty(fieldName);
    }
    
    private bool CheckExpectedValue(SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute attr = (ShowIfAttribute)attribute;
        SerializedProperty other = FindRelativeProperty(property, attr.fieldName);

        if (other == null)
        {
            Debug.LogWarning($"{label.text}: field '{attr.fieldName}' not found.");
            return true;
        }

        switch (other.propertyType)
        {
            case SerializedPropertyType.Boolean:
                if (attr.expectedValue is bool b)
                {
                    return other.boolValue.Equals(attr.expectedValue);
                }
                Debug.LogWarning($"Expected value for boolean comparison must be of type bool. Found: {attr.expectedValue} ({attr.expectedValue.GetType()})");
                break;

            case SerializedPropertyType.Enum:
                // compare by name OR index
                if (attr.expectedValue is string str)
                    return other.enumNames[other.enumValueIndex] == str;

                if (attr.expectedValue is int i)
                    return other.enumValueIndex == i;
                if (attr.expectedValue.GetType().IsEnum)
                    return other.enumNames[other.enumValueIndex] == attr.expectedValue.ToString();
                Debug.LogWarning($"Unsupported expectedValue type for enum comparison: {attr.expectedValue.GetType()}. Supported values include: enum itself, int, string (coinciding with a value)");

                break;

            case SerializedPropertyType.Integer:
                if (attr.expectedValue is int)
                    return other.intValue.Equals(attr.expectedValue);
                Debug.LogWarning($"Expected value for integer comparison must be of type int. Found: {attr.expectedValue} ({attr.expectedValue.GetType()})");
                break;

            case SerializedPropertyType.Float:
                if (attr.expectedValue is float f)
                    return Mathf.Approximately(other.floatValue, (float)attr.expectedValue);
                Debug.LogWarning($"Expected value for float comparison must be of type float. Found: {attr.expectedValue} ({attr.expectedValue.GetType()})");
                break;

            case SerializedPropertyType.String:
                if (attr.expectedValue is string s)
                    return other.stringValue.Equals(attr.expectedValue);
                Debug.LogWarning($"Expected value for string comparison must be of type string. Found: {attr.expectedValue} ({attr.expectedValue.GetType()})");
                break;
            case SerializedPropertyType.ObjectReference:
                if(attr.expectedValue is bool expectedBool && expectedBool== true)
                {
                    return other.objectReferenceValue == null;
                }
                if(attr.expectedValue is null)
                {
                    return other.objectReferenceValue == null;
                }
                
                Debug.LogWarning($"ShowIf/HideIf with ObjectReference cannot expect a value, it wil only check for nulls, (show if its null or hide if its null), [ShowIf(\"Attribute\")] works fine. Found: {attr.expectedValue}");

                break;

            default:
                Debug.LogWarning($"Unsupported type: {other.propertyType}");
                return true;
        }

        return true;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute att = (ShowIfAttribute)attribute;
        isVisible = CheckExpectedValue(property, label) == att.comparison;
        

        if (isVisible)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
        else
        {
            return;
        }

    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {


        if (isVisible)
        {

            return EditorGUI.GetPropertyHeight(property, label, true);
        }
        else
        {
            return 0;
        }

      
    }
}
#endif