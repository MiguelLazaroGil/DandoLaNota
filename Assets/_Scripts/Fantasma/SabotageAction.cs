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

    public abstract void Execute(GhostEntity ghost, Musician target);

    public virtual void OnUpdateAction(GhostEntity ghost, Musician target) { }

    public virtual void StopAction(GhostEntity ghost, Musician target) { }
}