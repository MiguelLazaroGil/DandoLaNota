using UnityEngine;
using UnityEngine.Events;

public class Grabbable : MonoBehaviour
{
    public UnityEvent OnGrabbed;
    public UnityEvent OnDropped;
    bool usedGravity;
    public void Grab(Transform pivot)
    {
        BatutaController batuta = Object.FindAnyObjectByType<BatutaController>();

        if (batuta != null)
        {
            batuta.DesactivarModoDirector();
        }

        //basicorro
        OnGrabbed?.Invoke();
        transform.position=pivot.position;
        this.transform.parent = pivot;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb)
        {
            usedGravity = rb.useGravity;
            rb.useGravity = false;
        }
    }
    public void Drop()
    {
        OnDropped?.Invoke();
        this.transform.parent = null;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb)
        {
           
            rb.useGravity = usedGravity;
        }
    }
}
