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
    [Tooltip("Cuanto baja la calidad del musico en puntos básicos")]
    public float utility = 100f;

    public float GetRawUtility()
    {
        return utility;
    }
    public void ApplySabotagge(Musician target)
    {
        target.currentAction = this;
        ApplyNegativeEffect(target);
        this.Execute(target);
    }
    public void StopSabotagge(Musician target)
    {
        StopNegativeEffect(target);
        this.Stop(target);
        target.currentAction = null;
    }
    protected abstract void Execute(Musician target);

    // NUEVO: se llama cuando una acción continua termina (el jugador la apaga desde la rueda
    // o sale del músico). Sobrescríbelo para limpiar el efecto (ej. StopNegativeEffect).
    protected virtual void Stop(Musician target) { }

    protected void ApplyNegativeEffect(Musician target)
    {
        MusicianQuality quality = target.quality;
        quality.StartNegativeAction(this.name, utility / 100);
    }
    protected void StopNegativeEffect(Musician target)
    {

        MusicianQuality quality = target.quality;
        quality.ResolveNegativeAction(this.name);
    }
    [Obsolete("Las acciones ya no tienen referencia al ghost y son auto contenidas, no es necesario acción externa de manera general")]
    public virtual void OnUpdateAction(GhostEntity ghost, Musician target) { }

    [Obsolete("Las acciones ya no tienen referencia al ghost y son auto contenidas, no es necesario acción externa de manera general")]
    public virtual void StopAction(GhostEntity ghost, Musician target) { }
}