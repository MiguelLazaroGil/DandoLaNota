using MLG.DelayedActionsTool;
using UnityEngine;
using UnityEngine.Events;

public class RepeatedAction : MonoBehaviour
{
    public UnityEvent actions;
    public float delay;
    public bool startRightAway = true;

    private void OnEnable()
    {
        if(startRightAway)
        {
            actions?.Invoke();
        }

        DelayedActions.Sequence(this)
            .Wait(delay)
            .Do(() => 
        {
            actions?.Invoke();
        })
            .LoopInfinite();
    }
    private void OnDisable()
    {
        DelayedActions.Abort(this);
    }
}
