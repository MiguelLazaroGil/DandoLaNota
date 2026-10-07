using UnityEngine;
using UnityEngine.Events;

using MLG.DelayedActionsTool;
public class EventPlayer : MonoBehaviour
{

    [SerializeField]
    private UnityEvent events;
    [SerializeField]
    bool playOnStart;
    [SerializeField]
    bool playOnEnable;
    public float delay;

    [SerializeField]
    bool verbose = false;
    private void Start()
    {
        if (playOnStart)
        {
            PlayEvents();
        }
    }
    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayEvents();
        }
    }
    public  void PlayEvents() {
        if (events != null && verbose)
        {
            Debug.Log("Eventos ejecutados: " + gameObject.name);
        }
        if (delay > 0f)
        {
            DelayedActions.Do(this, delay, InvokeEvents);
        }
        else
        {
            InvokeEvents();
        }
 
    }
    private void InvokeEvents()
    {
        events?.Invoke();
    }


}
