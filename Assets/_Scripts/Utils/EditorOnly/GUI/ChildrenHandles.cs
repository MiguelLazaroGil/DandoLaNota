

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

[ExecuteInEditMode]
#endif
public class ChildrenHandles : MonoBehaviour
{
#if UNITY_EDITOR
    public bool showHandles = true; // Puedes activarlo/desactivarlo desde el Inspector
    [SerializeField, Range(0, 3)]
    float sphereSize = 0.5f;
    private void OnDrawGizmosSelected()
    {
        //Dibujar una bola roja en el objeto
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, sphereSize);
        Gizmos.color = Color.green;
        foreach (Transform t in transform)
        {
            Gizmos.DrawSphere(t.position, sphereSize);
        }
    }
#endif
}
#if UNITY_EDITOR
[CustomEditor(typeof(ChildrenHandles))]
public class ChildrenHandlesEditor : Editor
{
    void OnSceneGUI()
    {
        ChildrenHandles script = (ChildrenHandles)target;
        if (!script.showHandles) return; // Si est� desactivado, no dibuja nada

        Transform parent = script.transform;
        if (parent.childCount == 0) return; // Si no tiene hijos, no hace nada

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            switch (Tools.current)
            {
                case Tool.Move:
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.PositionHandle(child.position, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(child, "Move Child");
                        child.position = newPos;
                    }
                    break;

                case Tool.Rotate:
                    EditorGUI.BeginChangeCheck();
                    Quaternion newRot = Handles.RotationHandle(child.rotation, child.position);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(child, "Rotate Child");
                        child.rotation = newRot;
                    }
                    break;

                case Tool.Scale:
                    EditorGUI.BeginChangeCheck();
                    Vector3 newScale = Handles.ScaleHandle(child.localScale, child.position, child.rotation, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(child, "Scale Child");
                        child.localScale = newScale;
                    }
                    break;
            }
        }
    }
}
#endif
