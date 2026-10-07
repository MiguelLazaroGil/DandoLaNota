using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using MLG.DelayedActionsTool;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider))]
public class OnTriggerEvent : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent onTriggerEnter;
    public UnityEvent onTriggerExit;
    public UnityEvent onTriggerStay;

    [Header("Filters Configuration")]
    [SerializeField] private TargetFilter filter = new TargetFilter();

    [Header("Params")]
    [SerializeField] private bool checkOnStay = false;
    [SerializeField, Tooltip("The OnTriggerEnter and OnTriggerExit events will only play once each, except for OnTriggerStay")]
    private bool isOneTime = false;
    [SerializeField] private bool destroyOnEnter = false;
    [SerializeField] private bool destroyOnExit = false;

    [Space]
    [SerializeField] private bool delayedEvents = false;
    [SerializeField] private float delayEnter = 0.0f;
    [SerializeField] private float delayExit = 0.0f;

    private float originalDelayEnter = 0.0f;
    private float originalDelayExit = 0.0f;
    private bool isTriggeredEnter = false;
    private bool isTriggeredExit = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[OnTriggerEvent] No hay Collider adjunto en {gameObject.name}.", this);
        }
        else
        {
            col.isTrigger = true;
        }

        if (!delayedEvents)
        {
            delayEnter = 0.0f;
            delayExit = 0.0f;
        }

        originalDelayEnter = delayEnter;
        originalDelayExit = delayExit;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isOneTime && isTriggeredEnter) return;

        if (CheckIsTarget(other))
        {
            isTriggeredEnter = true;

            if (!delayedEvents)
            {
                onTriggerEnter?.Invoke();
                if (destroyOnEnter) Destroy(gameObject);
            }
            else
            {
                DelayedActions.Do(this, delayEnter, () => { onTriggerEnter?.Invoke(); }, "OnTriggerEvent: Entered");
                if (destroyOnEnter)
                {
                    Destroy(gameObject, delayEnter + 0.1f);
                }
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!checkOnStay) return;

        if (CheckIsTarget(other))
        {
            onTriggerStay?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isOneTime && isTriggeredExit) return;

        if (CheckIsTarget(other))
        {
            isTriggeredExit = true;

            if (!delayedEvents)
            {
                onTriggerExit?.Invoke();
                if (destroyOnExit) Destroy(gameObject);
            }
            else
            {
                DelayedActions.Do(this, delayExit, () => { onTriggerExit?.Invoke(); }, "OnTriggerEvent: Exited");
                if (destroyOnExit)
                {
                    Destroy(gameObject, delayExit + 0.1f);
                }
            }
        }
    }

    private bool CheckIsTarget(Collider other)
    {
        return filter.IsValidTarget(other.gameObject, transform);
    }
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null || !col.enabled) return;

        // Guardamos la matriz original de Gizmos para no afectar a otros scripts
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Asignamos la matriz del transform local (aplica rotación, posición y escala automáticamente)
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;

        // Evaluamos el tipo exacto de Collider
        if (col is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else if (col is MeshCollider meshCol && meshCol.sharedMesh != null)
        {
            Gizmos.DrawWireMesh(meshCol.sharedMesh, Vector3.zero, Quaternion.identity, Vector3.one);
        }
        else if (col is CapsuleCollider capsule)
        {
            DrawWireCapsule(capsule);
        }
        else
        {
            // Fallback genérico usando los bounds globales si es un collider no estándar
            Gizmos.matrix = oldMatrix;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            return;
        }

        // Restauramos la matriz original
        Gizmos.matrix = oldMatrix;
    }

    private void DrawWireCapsule(CapsuleCollider capsule)
    {
        Vector3 center = capsule.center;
        float radius = capsule.radius;
        float height = Mathf.Max(capsule.height, radius * 2f);
        float sideLength = (height * 0.5f) - radius;

        // Orientación según la propiedad Direction del CapsuleCollider (0 = X, 1 = Y, 2 = Z)
        Vector3 dir = Vector3.up;
        Vector3 right = Vector3.right;
        Vector3 forward = Vector3.forward;

        if (capsule.direction == 0) // Eje X
        {
            dir = Vector3.right;
            right = Vector3.up;
            forward = Vector3.forward;
        }
        else if (capsule.direction == 2) // Eje Z
        {
            dir = Vector3.forward;
            right = Vector3.right;
            forward = Vector3.up;
        }

        Vector3 top = center + dir * sideLength;
        Vector3 bottom = center - dir * sideLength;

        // Esferas para los extremos
        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);

        // Líneas laterales que conectan la cápsula
        Gizmos.DrawLine(top + right * radius, bottom + right * radius);
        Gizmos.DrawLine(top - right * radius, bottom - right * radius);
        Gizmos.DrawLine(top + forward * radius, bottom + forward * radius);
        Gizmos.DrawLine(top - forward * radius, bottom - forward * radius);
    }

    private void OnDisable()
    {
        DelayedActions.Abort(this);
        isTriggeredEnter = false;
        isTriggeredExit = false;
        delayEnter = originalDelayEnter;
        delayExit = originalDelayExit;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(OnTriggerEvent))]
    internal class OnTriggerEventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Dibujar campo m_Script deshabilitado
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            GUI.enabled = true;

            // Filtros unificados
            EditorGUILayout.PropertyField(serializedObject.FindProperty("filter"), true);
            EditorGUILayout.Space();

            // Configuración de Parámetros
            SerializedProperty checkOnStayProp = serializedObject.FindProperty("checkOnStay");
            EditorGUILayout.PropertyField(checkOnStayProp);

            SerializedProperty isOneTimeProp = serializedObject.FindProperty("isOneTime");
            EditorGUILayout.PropertyField(isOneTimeProp);

            if (isOneTimeProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyOnEnter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyOnExit"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty delayedEventsProp = serializedObject.FindProperty("delayedEvents");
            EditorGUILayout.PropertyField(delayedEventsProp);

            if (delayedEventsProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("delayEnter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("delayExit"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);

            // Dibujar eventos
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onTriggerEnter"));

            if (checkOnStayProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onTriggerStay"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("onTriggerExit"));

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}