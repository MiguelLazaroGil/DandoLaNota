using UnityEngine;
using UnityEngine.Events;

public class Grabbable : MonoBehaviour
{
    public UnityEvent OnGrabbed;
    public UnityEvent OnDropped;
    public void Grab(Transform pivot)
    {
        //basicorro
        OnGrabbed?.Invoke();
        this.transform.parent = pivot;
    }
    public void Drop()
    {
        OnDropped?.Invoke();
        this.transform.parent = null;
    }
}
