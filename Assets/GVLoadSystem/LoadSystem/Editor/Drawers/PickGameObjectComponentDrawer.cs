#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace GVLoadSystem.GVEditor
{

    [CustomPropertyDrawer(typeof(PickGameObjectComponentAttribute))]
    public class PickGameObjectComponentDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Property debe ser de tipo GameObjectComponentReference
            if (property.propertyType != SerializedPropertyType.Generic)
            {
                EditorGUI.LabelField(position, label.text, "Use PickGameObjectComponent with GameObjectComponentReference");
                return;
            }

            // Sacamos las propiedades internas
            SerializedProperty gameObjectProp = property.FindPropertyRelative("gameObject");
            SerializedProperty selectedIndexProp = property.FindPropertyRelative("selectedComponentIndex");

            EditorGUI.BeginProperty(position, label, property);

            // Dividir el espacio para dos controles: GameObject y popup componente
            float halfWidth = position.width / 2;
            Rect goRect = new Rect(position.x, position.y, halfWidth - 4, position.height);
            Rect popupRect = new Rect(position.x + halfWidth, position.y, halfWidth, position.height);

            // Dibujamos el campo para asignar el GameObject
            EditorGUI.PropertyField(goRect, gameObjectProp, GUIContent.none);

            // Si hay GameObject asignado, mostramos popup para seleccionar componente
            if (gameObjectProp.objectReferenceValue != null)
            {
                GameObject go = (GameObject)gameObjectProp.objectReferenceValue;
                MonoBehaviour[] components = go.GetComponents<MonoBehaviour>();
                string[] componentNames = new string[components.Length];
                for (int i = 0; i < components.Length; i++)
                    componentNames[i] = components[i].GetType().Name;

                // Validar �ndice
                if (selectedIndexProp.intValue < 0 || selectedIndexProp.intValue >= components.Length)
                    selectedIndexProp.intValue = 0;

                selectedIndexProp.intValue = EditorGUI.Popup(popupRect, selectedIndexProp.intValue, componentNames);
            }
            else
            {
                // Si no hay GameObject asignado, deshabilitar el popup
                EditorGUI.LabelField(popupRect, "Assign GameObject");
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif