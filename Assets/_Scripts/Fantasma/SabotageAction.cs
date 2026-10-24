using System;
using UnityEngine;

public abstract class SabotageAction : ScriptableObject
{
    [Header("Info para la Rueda")]
    public string actionName;
    public Sprite icon;

    [Header("Reglas de Gameplay")]
    public float energyCost = 15f;
    public bool isContinuous = false; 
    public bool playerOnly = false;

    public float utility = 100f;

    public float GetRawUtility()
    {
        return utility;
    }
    public abstract void Execute(Musician target);

    [Obsolete("Las acciones ya no tienen referencia al ghost y son auto contenidas, no es necesario acción externa de manera general")]
    public virtual void OnUpdateAction(GhostEntity ghost, Musician target) { }

    [Obsolete("Las acciones ya no tienen referencia al ghost y son auto contenidas, no es necesario acción externa de manera general")]
    public virtual void StopAction(GhostEntity ghost, Musician target) { }
}