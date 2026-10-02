
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MLG.DelayedActionsTool;
using Codice.CM.Client.Differences;
#if UNITY_EDITOR
using UnityEditor;
#endif
public enum TypeOfTargeting
{
    Tag,
    SpecificGameObject
}
public class OnTriggerEvent : MonoBehaviour
{

    [Header("Events")]
    public UnityEvent onTriggerEnter;
    public UnityEvent onTriggerExit;
    public UnityEvent onTriggerStay;

    [Header("Targeting")]
    public TypeOfTargeting targetingType = TypeOfTargeting.Tag;
    [SerializeField, Tooltip("The tag to check for the events."), ShowIf("targetingType", TypeOfTargeting.Tag)]
    string checkedTag = "Player";
    [SerializeField, ShowIf("targetingType", TypeOfTargeting.Tag)]
    bool useOtherTags = false;
    [SerializeField, HideIf("useOtherTags",false), ShowIf("targetingType", TypeOfTargeting.Tag)]
    private List<string> otherCheckedTags = new();

    [SerializeField, ShowIf("targetingType", TypeOfTargeting.SpecificGameObject)]
    private List<GameObject> specificGameObjectsToCheck = new List<GameObject>();
    [Space]
    [Header("Params")]
    [SerializeField]
    private bool checkOnStay = false;
    [SerializeField, Tooltip("The OnTriggerEnter and OnTriggerExit events will only play once each, except for OnTriggerStay")]
    private bool isOneTime = false;
    private bool performedEnter = false;
    private bool performedExit = false;
    [SerializeField]
    private bool DestroyOnEnter = false;
    [SerializeField]
    private bool DestroyOnExit = false;
    [Space]
    [SerializeField]
    private bool delayedEvents = false;
    [SerializeField]
    private float delayEnter = 0.0f;
    private float orginalDelayEnter = 0.0f;
    [SerializeField]
    private float delayExit = 0.0f;
    private float orginalDelayExit = 0.0f;

    private bool isTriggeredEnter = false;
    private bool isTriggeredExit = false;
    private void Awake()
    {
        var temp = GetComponent<Collider>();
        if (temp == null)
        {
            Debug.LogError("No hay trigger para este ontrigger event");
        }
        else
        {
            temp.isTrigger = true;
        }
        if (!delayedEvents)
        {
            delayEnter = 0.0f;
            delayExit = 0.0f;
        }
        orginalDelayEnter = delayEnter;
        orginalDelayExit = delayExit;

        if (!useOtherTags)
        {
            otherCheckedTags.Clear();
        }
        otherCheckedTags.Add(checkedTag);

    }

    private void OnTriggerEnter(Collider other)
    {
        if (isOneTime && isTriggeredEnter)
        {
            return;
        }
       
        if (CheckIsTarget(other))
        {
            isTriggeredEnter = true;
            if (!delayedEvents)
            {
                onTriggerEnter?.Invoke();
            }
            else
            {

                DelayedActions.Do(this, delayEnter, () => {onTriggerEnter?.Invoke();}, "OnTriggerEvent: Entered");
                if (DestroyOnEnter)
                {
                    Destroy(gameObject, delayEnter+0.1f);
                }
            }
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (!checkOnStay) { return; }

        if (CheckIsTarget(other))
        {
            onTriggerStay?.Invoke();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (isOneTime && isTriggeredExit)
        {
            return;
        }
        if (CheckIsTarget(other))
        {
            isTriggeredExit = true;
            if (!delayedEvents)
            {
                onTriggerExit?.Invoke();
            }
            else
            {
                performedExit = isOneTime;
                DelayedActions.Do(this, delayExit, () =>{ onTriggerExit?.Invoke(); }, "OnTriggerEvent: Exited");

                if (DestroyOnExit)
                {
                    Destroy(gameObject, delayExit+0.1f);
                }
            }
        }
    }
    private bool CheckIsTarget(Collider other)
    {
        bool isTarget = false;
        switch (targetingType)
        {
            case TypeOfTargeting.Tag:
                isTarget = otherCheckedTags.Contains(other.tag);
                break;
            case TypeOfTargeting.SpecificGameObject:
                isTarget = specificGameObjectsToCheck.Contains(other.gameObject);
                break;
        }
        return isTarget;
    }
   
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (GetComponent<Collider>() != null)
        {
            Gizmos.DrawWireCube(transform.position, GetComponent<Collider>().bounds.size);
        }

    }
    private void OnDisable()
    {
        DelayedActions.Abort(this);
        isTriggeredEnter = false;
        isTriggeredExit = false;
        performedEnter = false;
        performedExit = false;
        delayEnter = orginalDelayEnter;
        delayExit = orginalDelayExit;
    }


#if UNITY_EDITOR

    [CustomEditor(typeof(OnTriggerEvent))]
    internal class OnTriggerEventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            bool isOneTimeValue = serializedObject.FindProperty("isOneTime").boolValue;
            bool delayedEventsValue = serializedObject.FindProperty("delayedEvents").boolValue;
            bool checkOnStayValue = serializedObject.FindProperty("checkOnStay").boolValue;
            bool useOtherTagsValue = serializedObject.FindProperty("useOtherTags").boolValue;
            /* SerializedProperty onTriggerEnter = serializedObject.FindProperty("onTriggerEnter");
             SerializedProperty onTriggerExit = serializedObject.FindProperty("onTriggerExit");
             SerializedProperty onTriggerStay = serializedObject.FindProperty("onTriggerStay");
             SerializedProperty checkedTags = serializedObject.FindProperty("checkedTags");
             SerializedProperty checkOnStay = serializedObject.FindProperty("checkOnStay");
             SerializedProperty isOneTime = serializedObject.FindProperty("isOneTime");
             SerializedProperty DestroyOnEnter = serializedObject.FindProperty("DestroyOnEnter");
             SerializedProperty DestroyOnExit = serializedObject.FindProperty("DestroyOnExit");
             SerializedProperty delayedEvents = serializedObject.FindProperty("delayedEvents");
             SerializedProperty delayEnter = serializedObject.FindProperty("delayEnter");
             SerializedProperty delayExit = serializedObject.FindProperty("delayExit");*/
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true);
            GUI.enabled = false;
            EditorGUILayout.PropertyField(property);
            GUI.enabled = true;
            // property.NextVisible(false); //quitarse algunos que no se usan
            //    property.Next(false);
            property.Next(false);
            property.Next(false);
            while (property.Next(false))
            {
                if (property.propertyPath == "otherCheckedTags")
                {
                    if (!useOtherTagsValue)
                    {
                        continue;
                    }
                }
                if (property.propertyPath == "onTriggerStay")
                {
                    if (!checkOnStayValue) //Solo dibujarla si checkOnStay es true
                    {
                        continue;
                    }
                  
                }
                if (property.propertyPath == "DestroyOnEnter" || property.propertyPath == "DestroyOnExit")
                {

                    if (isOneTimeValue) //Solo dibujarla si es onetime
                    {
                        EditorGUI.indentLevel++;

                        EditorGUILayout.PropertyField(property);
                        EditorGUI.indentLevel--;
                    }
                    continue; //para no volverla a dibujar
                }

                if (property.propertyPath == "delayEnter" || property.propertyPath == "delayExit")
                {
                    if (delayedEventsValue)
                    {
                        EditorGUI.indentLevel++;

                        EditorGUILayout.PropertyField(property);
                        EditorGUI.indentLevel--;
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(property);
            }


            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}

