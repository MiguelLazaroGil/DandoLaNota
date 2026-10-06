#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using MLG.DelayedActionsTool;
public enum AnimationType { Lineal, EaseIn, EaseOut, EaseInOut, CustomCurve }

public enum LocalDirection { Up, Down, Left, Right, Forward, Back }
public enum PositionMode { ManualAB, DirectionAndDistance }
public enum PositionType { local, world }

[System.Serializable]
public class AnimationSettings
{
    [Header("Posicionamiento")]
    public PositionMode positionMode = PositionMode.ManualAB;
    public PositionType positioning = PositionType.local;


    // Campos para DirectionAndDistance
    [HideInInspector] public LocalDirection pushDirection = LocalDirection.Forward;
    [HideInInspector] public float distance = 1.0f;

    // Campos para ManualAB
    [HideInInspector] public Vector3 positionA;
    [HideInInspector] public Vector3 positionB;
    [HideInInspector] public bool posAIsOriginalPos = true;

    [Header("Configuración de Animación")]
    public AnimationType animationType = AnimationType.Lineal;

    [ShowIf("animationType", AnimationType.CustomCurve)]
    public AnimationCurve customCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);


    [Range(0f, 1f), Tooltip("Progreso inicial entre A (0) y B (1) en Runtime")]
    public float initialProgress = 0f;
    [Tooltip("Cambia hacia el otro lado al iniciar el juego, valores intermedios de progreso cuentan como lo que más cerca esté (0.3 => 0, luego se mueve hacia el 1)")]
    public bool togglePositionOnStart = false;

    public bool useAnimSpeed = false;
    [HideIf("useAnimSpeed")] public float duration = 1.0f;
    [ShowIf("useAnimSpeed")] public float moveSpeed = 2.0f;

    [Tooltip("Multiplicador de velocidad/duración para el viaje de vuelta (de B hacia A)")]
    public float returnSpeedMultiplier = 1.0f;

   [ReadOnly, Range(0,1), Tooltip("Progreso Actual del objeto en la animación")]
    public float currentProgress = 0f;
}

public class SimpleAnimation : MonoBehaviour, IAnimation
{

    public AnimationSettings settings = new AnimationSettings();

    private bool isMoving = false;
    private bool goingToB = true;

    protected virtual void Start()
    {
        if (UsesOriginalPosAsA())
        {
            settings.positionA = GetCurrentTransformPosition();
        }

        // Aplicar progreso inicial en runtime
        settings.currentProgress = Mathf.Clamp01(settings.initialProgress);
        ApplyPositionFromProgress();
        goingToB = settings.currentProgress >= 0.5f;

        if (settings.togglePositionOnStart)
        {
            TogglePosition();
        }
    }

    /// <summary>
    /// Indica si el modo actual utiliza la posición original del Transform como A.
    /// </summary>
    public virtual bool UsesOriginalPosAsA()
    {
        return settings.positionMode == PositionMode.DirectionAndDistance || settings.posAIsOriginalPos;
    }

    /// <summary>
    /// Retorna la posición A efectiva.
    /// </summary>
    public virtual Vector3 GetPositionA()
    {
        return settings.positionA;
    }

    /// <summary>
    /// Forzado explícito para actualizar A desde la escena únicamente cuando está en reposo.
    /// </summary>
    public virtual void UpdateOriginalPositionA()
    {
        if (UsesOriginalPosAsA() && settings.currentProgress <= 0.0001f)
        {
            settings.positionA = GetCurrentTransformPosition();
        }
    }

    /// <summary>
    /// Retorna la posición B calculada según el modo.
    /// </summary>
    public virtual Vector3 GetPositionB()
    {
        if (settings.positionMode == PositionMode.ManualAB)
        {
            return settings.positionB;
        }

        Vector3 originA = GetPositionA();
        Vector3 dirVector = GetVectorFromDirection(settings.pushDirection);
        return originA + (dirVector * settings.distance);
    }

    public virtual Vector3 GetCurrentTransformPosition()
    {
        return settings.positioning == PositionType.local ? transform.localPosition : transform.position;
    }

    private Vector3 GetVectorFromDirection(LocalDirection dir) => dir switch
    {
        LocalDirection.Up => Vector3.up,
        LocalDirection.Down => Vector3.down,
        LocalDirection.Left => Vector3.left,
        LocalDirection.Right => Vector3.right,
        LocalDirection.Forward => Vector3.forward,
        LocalDirection.Back => Vector3.back,
        _ => Vector3.forward
    };

    protected virtual void Update()
    {
        if (!isMoving) return;

        Vector3 posA = GetPositionA();
        Vector3 posB = GetPositionB();

        float effectiveMultiplier = goingToB ? 1.0f : settings.returnSpeedMultiplier;
        float speedFactor = Mathf.Max(0.0001f, effectiveMultiplier);

        float step = Time.deltaTime;
        if (settings.useAnimSpeed)
        {
            float totalDist = Vector3.Distance(posA, posB);
            if (totalDist > 0.0001f)
            {
                step *= (settings.moveSpeed * speedFactor) / totalDist;
            }
        }
        else
        {
            step *= speedFactor / Mathf.Max(0.0001f, settings.duration);
        }

        if (goingToB)
        {
            settings.currentProgress += step;
            if (settings.currentProgress >= 1f)
            {
                settings.currentProgress = 1f;
                isMoving = false;
            }
        }
        else
        {
            settings.currentProgress -= step;
            if (settings.currentProgress <= 0f)
            {
                settings.currentProgress = 0f;
                isMoving = false;
            }
        }

        ApplyPositionFromProgress();
    }

    public virtual void ApplyPositionFromProgress()
    {
        // Evaluamos t pasando la curva configurada
        float easedT = EvaluateEasing(settings.currentProgress, settings.animationType, settings.customCurve);
        Vector3 currentPos = Vector3.Lerp(GetPositionA(), GetPositionB(), easedT);

        if (settings.positioning == PositionType.local)
            transform.localPosition = currentPos;
        else
            transform.position = currentPos;
    }
    /// </summary>
    public virtual float CalculateTimeBetween(float fromProgress, float toProgress, bool isGoingToB)
    {
        float distanceProgress = Mathf.Abs(toProgress - fromProgress);
        if (distanceProgress <= 0.0001f) return 0f;

        float effectiveMultiplier = isGoingToB ? 1.0f : settings.returnSpeedMultiplier;
        float speedFactor = Mathf.Max(0.0001f, effectiveMultiplier);

        if (settings.useAnimSpeed)
        {
            float totalDist = Vector3.Distance(GetPositionA(), GetPositionB());
            if (totalDist <= 0.0001f) return 0f;

            float remainingDistance = totalDist * distanceProgress;
            float speed = settings.moveSpeed * speedFactor;
            return remainingDistance / Mathf.Max(0.0001f, speed);
        }
        else
        {
            float baseDuration = settings.duration / speedFactor;
            return baseDuration * distanceProgress;
        }
    }
    /// <summary>
    /// Calcula el tiempo en segundos que tardará en ir del progreso actual al progreso objetivo.
    /// </summary>
    public virtual float CalculateRemainingTime(float targetProgress, bool isGoingToB)
    {
        return CalculateTimeBetween(settings.currentProgress, targetProgress, isGoingToB);
    }

    public virtual float MoveToA()
    {
        DelayedActions.Abort(this);
        float remainingTime = CalculateRemainingTime(0f, false);
        goingToB = false;
        isMoving = true;
        return remainingTime;
    }
    public virtual float MoveToB()
    {
        DelayedActions.Abort(this);
        float remainingTime = CalculateRemainingTime(1f, true);
        goingToB = true;
        isMoving = true;
        return remainingTime;
    }
    public virtual float TogglePosition()
    {
        return !goingToB ? MoveToB() : MoveToA();
    }

    public virtual float MoveToAThenB(float delay)
    {
        DelayedActions.Abort(this);

        float timeToA = CalculateRemainingTime(0f, false);
        float waitDelay = Mathf.Max(0f, delay);
        float fullTimeB = CalculateTimeBetween(0f, 1f, true);
        float totalTime = timeToA + waitDelay + fullTimeB;

        var sequence = DelayedActions.Sequence(this)
            .Do(() =>
            {
                goingToB = false;
                isMoving = true;
            })
            .Wait(timeToA);

        if (waitDelay > 0f)
        {
            sequence.Wait(waitDelay);
        }

        sequence.Do(() =>
        {
            goingToB = true;
            isMoving = true;
        })
        .Start();

        return totalTime;
    }

    public virtual float MoveToBThenA(float delay)
    {
        DelayedActions.Abort(this);

        float timeToB = CalculateRemainingTime(1f, true);
        float waitDelay = Mathf.Max(0f, delay);
        float fullTimeA = CalculateTimeBetween(1f, 0f, false);
        float totalTime = timeToB + waitDelay + fullTimeA;

        var sequence = DelayedActions.Sequence(this)
            .Do(() =>
            {
                goingToB = true;
                isMoving = true;
            })
            .Wait(timeToB).Wait(waitDelay)
            .Do(() =>
            {
                goingToB = false;
                isMoving = true;
            })
            .Start();

        return totalTime;
    }
    public virtual void SnapSwitch()
    {
        goingToB = !goingToB;
        settings.currentProgress = goingToB ? 1f : 0f;
        ApplyPositionFromProgress();
    }
    /// <summary>
    /// Detiene el movimiento de la animación inmediatamente en su posición actual.
    /// </summary>
    public virtual void Stop()
    {
        isMoving = false;
    }
    public static float EvaluateEasing(float t, AnimationType type, AnimationCurve customCurve = null) => type switch
    {
        AnimationType.EaseIn => t * t,
        AnimationType.EaseOut => t * (2f - t),
        AnimationType.EaseInOut => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t,
        AnimationType.CustomCurve => customCurve != null ? customCurve.Evaluate(t) : t,
        _ => t
    };

}
#if UNITY_EDITOR

[CustomEditor(typeof(SimpleAnimation))]
[CanEditMultipleObjects]
public class SimpleAnimationEditor : Editor
{
    private SimpleAnimation script;

    private void OnEnable()
    {
        script = (SimpleAnimation)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty settingsProp = serializedObject.FindProperty("settings");
        SerializedProperty posModeProp = settingsProp.FindPropertyRelative("positionMode");
        SerializedProperty initialProgProp = settingsProp.FindPropertyRelative("initialProgress");
        SerializedProperty currentProgProp = settingsProp.FindPropertyRelative("currentProgress");

        DrawDefaultInspector();

        PositionMode currentMode = (PositionMode)posModeProp.enumValueIndex;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Configuración de Posiciones", EditorStyles.boldLabel);

        // Si el objeto está en reposo (progreso == 0), SÍ actualizamos la A inicial desde el Transform
        if (currentProgProp.floatValue <= 0.0001f)
        {
            script.UpdateOriginalPositionA();
        }

        if (currentMode == PositionMode.DirectionAndDistance)
        {
            EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("pushDirection"));
            EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("distance"));
        }
        else if (currentMode == PositionMode.ManualAB)
        {
            DrawManualPositions(settingsProp);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Control de Posición en Editor", EditorStyles.boldLabel);

        // Slider de previsualización editable
        EditorGUI.BeginChangeCheck();
        float newProgress = EditorGUILayout.Slider("Progreso Actual", currentProgProp.floatValue, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(script.transform, "Cambio de progreso");
            Undo.RecordObject(script, "Cambio de progreso");

            currentProgProp.floatValue = newProgress;
            serializedObject.ApplyModifiedProperties();

            // Mover físicamente el objeto primero usando la A ya almacenada
            script.ApplyPositionFromProgress();

            // Solo actualizar la A de reposo SI el nuevo progreso fijado es exactamente 0
            if (newProgress <= 0.0001f)
            {
                script.UpdateOriginalPositionA();
            }

            EditorUtility.SetDirty(script.transform);
            EditorUtility.SetDirty(script);
        }

        EditorGUILayout.Space();
        if (currentProgProp.floatValue > 0.5f)
        {
            string buttonLabel = currentMode == PositionMode.ManualAB ? "Cambiar a Posición A" : "Cambiar a Posición Inicial";
            if (GUILayout.Button(buttonLabel))
            {
                DoUndo("Snap Switch", s => {
                    s.settings.currentProgress = 0f;
                    s.ApplyPositionFromProgress();
                    s.UpdateOriginalPositionA();
                });
            }
        }
        else
        {
            string buttonLabel = currentMode == PositionMode.ManualAB ? "Cambiar a Posición B" : "Cambiar a Posición Final";

            if (GUILayout.Button(buttonLabel))
            {
                DoUndo("Snap Switch", s => {
                    s.settings.currentProgress = 1f;
                    s.ApplyPositionFromProgress();
                });
            }
        }

        // Cartel informativo cuando se está previsualizando (progreso > 0) y A depende de la posición inicial
        if (currentProgProp.floatValue > 0.0001f && script.UsesOriginalPosAsA())
        {
            string posName = currentMode == PositionMode.ManualAB ? "A" : "Inicial";
            EditorGUILayout.HelpBox(
                "Modo previsualización activo (Progreso > 0). La posición '"+ posName +"' se mantiene congelada. Mover el objeto en la escena no cambiará la posición 'A' base hasta que el progreso vuelva a 0.",
                MessageType.Info
            );
        }

        // Advertencia y botón para sincronizar el progreso inicial con el progreso actual
        if (Mathf.Abs(currentProgProp.floatValue - initialProgProp.floatValue) > 0.01f)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(
                $"⚠️ La posición en la escena ({currentProgProp.floatValue:P0}) no coincide con el progreso inicial configurado para el juego ({initialProgProp.floatValue:P0}).",
                EditorStyles.wordWrappedMiniLabel
            );

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Fijar este progreso como Inicial", GUILayout.Height(22)))
            {
                DoUndo("Establecer Progreso Inicial", s =>
                {
                    s.settings.initialProgress = s.settings.currentProgress;
                });
            }

            EditorGUILayout.EndVertical();
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawManualPositions(SerializedProperty settingsProp)
    {
        SerializedProperty posA = settingsProp.FindPropertyRelative("positionA");
        SerializedProperty posB = settingsProp.FindPropertyRelative("positionB");
        SerializedProperty posAIsOriginal = settingsProp.FindPropertyRelative("posAIsOriginalPos");

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(posAIsOriginal);
        bool posAIsOriginalChanged = EditorGUI.EndChangeCheck();

        // Al desmarcar "posAIsOriginalPos", asignamos a 'positionA' la posición calculada que tenía
        if (posAIsOriginalChanged && !posAIsOriginal.boolValue)
        {
            posA.vector3Value = script.GetPositionA();
        }

        if (!posAIsOriginal.boolValue)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(posA);
            if (GUILayout.Button("Set A", GUILayout.Width(60)))
            {
                DoUndo("Set A", s => {
                    s.settings.positionA = s.GetCurrentTransformPosition();
                });
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(posB);
        if (GUILayout.Button("Set B", GUILayout.Width(60)))
        {
            DoUndo("Set B", s => {
                s.settings.positionB = s.GetCurrentTransformPosition();
            });
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DoUndo(string label, System.Action<SimpleAnimation> action)
    {
        Undo.SetCurrentGroupName(label);
        int group = Undo.GetCurrentGroup();
        foreach (var obj in targets)
        {
            var s = (SimpleAnimation)obj;
            Undo.RecordObject(s, label);
            Undo.RecordObject(s.transform, label);
            action.Invoke(s);
            EditorUtility.SetDirty(s);
            EditorUtility.SetDirty(s.transform);
        }
        Undo.CollapseUndoOperations(group);
    }
}
#endif