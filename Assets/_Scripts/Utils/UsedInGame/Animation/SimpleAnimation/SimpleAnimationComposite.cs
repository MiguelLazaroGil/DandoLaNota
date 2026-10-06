#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

public class SimpleAnimationComposite : SimpleAnimation
{
    [Header("Configuración del Compuesto")]
    public List<SimpleAnimation> animations = new List<SimpleAnimation>();
    public bool overrideAnimSettings = true;
    public bool addChildrenToList = true;

    protected override void Start()
    {
        if (addChildrenToList && (animations == null || animations.Count == 0))
        {
            FetchChildren();
        }

        SyncSettingsToChildren();

        if (settings.togglePositionOnStart)
        {
            TogglePosition();
        }
    }

    protected override void Update()
    {
        // El compuesto desactiva el movimiento directo de su propio Transform;
        // cada sub-animación ejecuta su propio Update de forma independiente.
    }

    /// <summary>
    /// Busca y registra automáticamente todas las animaciones hijas omitiéndose a sí mismo.
    /// </summary>
    public void FetchChildren()
    {
        animations.Clear();
        var children = GetComponentsInChildren<SimpleAnimation>(true);
        foreach (var child in children)
        {
            if (child != this)
            {
                animations.Add(child);
            }
        }
    }

    /// <summary>
    /// Propaga los parámetros de velocidad, tiempo y curva a las animaciones hijas
    /// manteniendo intactas sus posiciones A/B individuales.
    /// </summary>
    public void SyncSettingsToChildren()
    {
        if (!overrideAnimSettings) return;

        foreach (var anim in animations)
        {
            if (anim == null || anim == this) continue;

            anim.settings.animationType = settings.animationType;
            anim.settings.customCurve = settings.customCurve;
            anim.settings.useAnimSpeed = settings.useAnimSpeed;
            anim.settings.duration = settings.duration;
            anim.settings.moveSpeed = settings.moveSpeed;
            anim.settings.returnSpeedMultiplier = settings.returnSpeedMultiplier;
            anim.settings.positioning = settings.positioning;
        }
    }

    public override float MoveToA()
    {
        SyncSettingsToChildren();
        float maxTime = 0f;
        foreach (var anim in animations)
        {
            if (anim != null)
            {
                float time = anim.MoveToA();
                if (time > maxTime) maxTime = time;
            }
        }
        return maxTime;
    }

    public override float MoveToB()
    {
        SyncSettingsToChildren();
        float maxTime = 0f;
        foreach (var anim in animations)
        {
            if (anim != null)
            {
                float time = anim.MoveToB();
                if (time > maxTime) maxTime = time;
            }
        }
        return maxTime;
    }

    public override float TogglePosition()
    {
        SyncSettingsToChildren();
        float maxTime = 0f;
        foreach (var anim in animations)
        {
            if (anim != null)
            {
                float time = anim.TogglePosition();
                if (time > maxTime) maxTime = time;
            }
        }
        return maxTime;
    }

    public override float MoveToAThenB(float delay)
    {
        SyncSettingsToChildren();
        float maxTime = 0f;
        foreach (var anim in animations)
        {
            if (anim != null)
            {
                float time = anim.MoveToAThenB(delay);
                if (time > maxTime) maxTime = time;
            }
        }
        return maxTime;
    }

    public override float MoveToBThenA(float delay)
    {
        SyncSettingsToChildren();
        float maxTime = 0f;
        foreach (var anim in animations)
        {
            if (anim != null)
            {
                float time = anim.MoveToBThenA(delay);
                if (time > maxTime) maxTime = time;
            }
        }
        return maxTime;
    }

    public override void SnapSwitch()
    {
        foreach (var anim in animations)
        {
            if (anim != null) anim.SnapSwitch();
        }
    }

    public override void Stop()
    {
        foreach (var anim in animations)
        {
            if (anim != null) anim.Stop();
        }
    }

    public override void ApplyPositionFromProgress()
    {
        foreach (var anim in animations)
        {
            if (anim != null)
            {
                anim.settings.currentProgress = settings.currentProgress;
                anim.ApplyPositionFromProgress();
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SimpleAnimationComposite))]
[CanEditMultipleObjects]
public class SimpleAnimationCompositeEditor : Editor
{
    private SimpleAnimationComposite script;

    private void OnEnable()
    {
        script = (SimpleAnimationComposite)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty animationsProp = serializedObject.FindProperty("animations");
        SerializedProperty overrideSettingsProp = serializedObject.FindProperty("overrideAnimSettings");
        SerializedProperty addChildrenProp = serializedObject.FindProperty("addChildrenToList");
        SerializedProperty settingsProp = serializedObject.FindProperty("settings");

        SerializedProperty initialProgProp = settingsProp.FindPropertyRelative("initialProgress");
        SerializedProperty currentProgProp = settingsProp.FindPropertyRelative("currentProgress");

        EditorGUILayout.LabelField("Gestión del Compuesto", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(addChildrenProp, new GUIContent("Auto Buscar Hijas"));
        EditorGUILayout.PropertyField(overrideSettingsProp, new GUIContent("Sobrescribir Ajustes en Hijas"));

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Buscar / Refrescar Animaciones Hijas", GUILayout.Height(24)))
        {
            DoUndoComposite("Refrescar Animaciones Hijas", s => s.FetchChildren());
        }

        EditorGUILayout.PropertyField(animationsProp, true);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Configuración de Tiempo y Curvas", EditorStyles.boldLabel);

        SerializedProperty animTypeProp = settingsProp.FindPropertyRelative("animationType");
        EditorGUILayout.PropertyField(animTypeProp);
        if ((AnimationType)animTypeProp.enumValueIndex == AnimationType.CustomCurve)
        {
            EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("customCurve"));
        }

        SerializedProperty useSpeedProp = settingsProp.FindPropertyRelative("useAnimSpeed");
        EditorGUILayout.PropertyField(useSpeedProp);
        if (useSpeedProp.boolValue)
        {
            EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("moveSpeed"));
        }
        else
        {
            EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("duration"));
        }

        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("returnSpeedMultiplier"));
        EditorGUILayout.PropertyField(initialProgProp);
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("togglePositionOnStart"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Control de Posición en Editor (Todas las Hijas)", EditorStyles.boldLabel);

        if (script.overrideAnimSettings)
        {
            script.SyncSettingsToChildren();
        }

        EditorGUI.BeginChangeCheck();
        float newProgress = EditorGUILayout.Slider("Progreso Conjunto", currentProgProp.floatValue, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            DoUndoComposite("Cambio de progreso conjunto", s =>
            {
                s.settings.currentProgress = newProgress;
                s.ApplyPositionFromProgress();
            });
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap a Posición A (Todas)"))
        {
            DoUndoComposite("Snap A Conjunto", s =>
            {
                s.settings.currentProgress = 0f;
                s.ApplyPositionFromProgress();
            });
        }
        if (GUILayout.Button("Snap a Posición B (Todas)"))
        {
            DoUndoComposite("Snap B Conjunto", s =>
            {
                s.settings.currentProgress = 1f;
                s.ApplyPositionFromProgress();
            });
        }
        EditorGUILayout.EndHorizontal();

        if (Mathf.Abs(currentProgProp.floatValue - initialProgProp.floatValue) > 0.01f)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"⚠️ El progreso actual en escena ({currentProgProp.floatValue:P0}) difiere del inicial ({initialProgProp.floatValue:P0}).",
                EditorStyles.wordWrappedMiniLabel
            );
            if (GUILayout.Button("Fijar este progreso como Inicial (Todas)", GUILayout.Height(22)))
            {
                DoUndoComposite("Fijar Progreso Inicial Conjunto", s =>
                {
                    s.settings.initialProgress = s.settings.currentProgress;
                    foreach (var anim in s.animations)
                    {
                        if (anim != null) anim.settings.initialProgress = s.settings.currentProgress;
                    }
                });
            }
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DoUndoComposite(string label, System.Action<SimpleAnimationComposite> action)
    {
        Undo.SetCurrentGroupName(label);
        int group = Undo.GetCurrentGroup();
        foreach (var targetObj in targets)
        {
            var comp = (SimpleAnimationComposite)targetObj;
            Undo.RecordObject(comp, label);
            Undo.RecordObject(comp.transform, label);

            foreach (var anim in comp.animations)
            {
                if (anim == null) continue;
                Undo.RecordObject(anim, label);
                Undo.RecordObject(anim.transform, label);
            }

            action.Invoke(comp);

            EditorUtility.SetDirty(comp);
            EditorUtility.SetDirty(comp.transform);
            foreach (var anim in comp.animations)
            {
                if (anim == null) continue;
                EditorUtility.SetDirty(anim);
                EditorUtility.SetDirty(anim.transform);
            }
        }
        Undo.CollapseUndoOperations(group);
    }
}
#endif