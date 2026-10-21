using UnityEngine;
using UnityEngine.Events;

public class Grabbable : MonoBehaviour
{
    public UnityEvent OnGrabbed;
    public UnityEvent OnDropped;
    bool usedGravity;
    bool usedKinematic;
    public void Grab(Transform pivot)
    {

        OnGrabbed?.Invoke();
        transform.position = pivot.position;
        transform.SetParent(pivot);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb)
        {
            usedGravity = rb.useGravity;
            rb.useGravity = false;
            usedKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }
    }
    public void Drop()
    {
        OnDropped?.Invoke();
        transform.SetParent(null);
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb)
        {

            rb.useGravity = usedGravity;
            rb.isKinematic = usedKinematic;
        }
    }
}
