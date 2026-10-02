using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
// Solo se puede usar en campos (variables)
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class RequireInterfaceAttribute : PropertyAttribute
{
    public Type InterfaceType { get; private set; }

    public RequireInterfaceAttribute(Type interfaceType)
    {
        this.InterfaceType = interfaceType;
    }
}
#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(RequireInterfaceAttribute))]
public class RequireInterfaceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Recuperamos el atributo para saber qué interfaz estamos pidiendo
        RequireInterfaceAttribute requiredAttribute = attribute as RequireInterfaceAttribute;

        // Comprobamos que se esté aplicando a una referencia a un objeto
        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
            EditorGUI.HelpBox(position, "RequireInterface solo funciona con referencias a objetos (MonoBehaviour).", MessageType.Error);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        // Detectamos si el usuario arrastra algo nuevo
        EditorGUI.BeginChangeCheck();

        // Dibujamos el campo clásico de Unity
        UnityEngine.Object obj = EditorGUI.ObjectField(position, label, property.objectReferenceValue, requiredAttribute.InterfaceType, true);
        if (EditorGUI.EndChangeCheck())
        {
            if (obj == null)
            {
                // Si el usuario borra el campo, lo dejamos en null
                property.objectReferenceValue = null;
            }
            else
            {
                // Si ha arrastrado un GameObject, intentamos sacar el componente con la interfaz
                if (obj is GameObject go)
                {
                    var component = go.GetComponent(requiredAttribute.InterfaceType);
                    if (component != null)
                    {
                        property.objectReferenceValue = component;
                    }
                    else
                    {
                        Debug.LogWarning($"El GameObject '{go.name}' no tiene ningún componente que implemente {requiredAttribute.InterfaceType.Name}.");
                        property.objectReferenceValue = null; // Rechazamos el objeto
                    }
                }
                // Si ha arrastrado directamente un Componente
                else if (requiredAttribute.InterfaceType.IsAssignableFrom(obj.GetType()))
                {
                    property.objectReferenceValue = obj;
                }
                else
                {
                    Debug.LogWarning($"El objeto '{obj.name}' no implementa la interfaz {requiredAttribute.InterfaceType.Name}.");
                    property.objectReferenceValue = null; // Rechazamos el objeto
                }
            }
        }

        EditorGUI.EndProperty();
    }
}
#endif