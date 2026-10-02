using System;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


[AttributeUsage(AttributeTargets.Method)]
public class ButtonAttribute : Attribute
{
    public string Label;

    public ButtonAttribute(string label = null)
    {
        Label = label;
    }
}
#if UNITY_EDITOR

[CustomEditor(typeof(MonoBehaviour), true)]
[CanEditMultipleObjects]
public class ButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var targetType = target.GetType();
        var methods = targetType.GetMethods(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            var buttonAttr = method.GetCustomAttribute<ButtonAttribute>();
            if (buttonAttr == null) continue;

            string label = string.IsNullOrEmpty(buttonAttr.Label)
                ? method.Name
                : buttonAttr.Label;

            if (GUILayout.Button(label))
            {
                Undo.SetCurrentGroupName(label);
                int undoGroup = Undo.GetCurrentGroup();

                foreach (var obj in targets)
                {
                    Undo.RegisterCompleteObjectUndo(obj, label);
                    method.Invoke(obj, null);
                    EditorUtility.SetDirty(obj);
                }

                Undo.CollapseUndoOperations(undoGroup);
            }
        }
    }
}
#endif