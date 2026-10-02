using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR 
using UnityEditor;
#endif

public class CodedEventPlayer : MonoBehaviour
{
    [SerializeField, Tooltip("The events will play if playCodedEvents is called with this code.")]
    int code;
    [SerializeField]
    bool useOtherCodes;
    [SerializeField, Tooltip("The events will also play if playCodedEvents is called with any of the added codes.")]
    List<int> extraCodes;
    [Space]
    [SerializeField]
    private UnityEvent events;
  
    public  void PlayCodedEvents(int code) {
        //Do not play checking
        if ( this.code != code)
        {
            if (!useOtherCodes) return;
            if(!extraCodes.Contains(code)) { return; }
        }
        events?.Invoke();
    }


}
#if UNITY_EDITOR

[CustomEditor(typeof(CodedEventPlayer))]
public class CodedEventPlayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SerializedProperty code = serializedObject.FindProperty("code");
        SerializedProperty useOtherCodes = serializedObject.FindProperty("useOtherCodes");
        SerializedProperty extraCodes = serializedObject.FindProperty("extraCodes");
        SerializedProperty events = serializedObject.FindProperty("events");

        SerializedProperty property = serializedObject.GetIterator();
        property.NextVisible(true);
        GUI.enabled = false;
        EditorGUILayout.PropertyField(property);
        GUI.enabled = true;

        EditorGUILayout.PropertyField(code);
        EditorGUILayout.PropertyField(useOtherCodes);

        if (useOtherCodes.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(extraCodes, true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(events);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
