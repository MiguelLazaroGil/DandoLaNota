using UnityEngine;
using UnityEngine.Events;

public class Grabbable : MonoBehaviour
{
    public UnityEvent OnGrabbed;
    public UnityEvent OnDropped;
    [SerializeField, Range(1, 20)] int velocitySamples = 6;
    [SerializeField] float throwMultiplier = 1f;
    [SerializeField] float maxThrowSpeed = 20f;          // m/s
    [SerializeField] float maxThrowAngularSpeed = 30f;
    Rigidbody rb;
    Transform originalParent;
    bool usedGravity;
    bool usedKinematic;

    public bool IsHeld { get; private set; }
    public Vector3 HeldVelocity { get; private set; }
    public Vector3 HeldAngularVelocity { get; private set; }


    Vector3[] linSamples;
    Vector3[] angSamples;
    int sampleIndex;
    int sampleCount;
    Vector3 lastPos;
    Quaternion lastRot;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        linSamples = new Vector3[velocitySamples];
        angSamples = new Vector3[velocitySamples];
    }
    public void Grab(Transform pivot)
    {
        if (IsHeld) return;
        IsHeld = true;
        originalParent = transform.parent;

        if (rb)
        {
            usedGravity = rb.useGravity;
            usedKinematic = rb.isKinematic;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        transform.SetParent(pivot);
        transform.position = pivot.position;

        ResetSamples();
        OnGrabbed?.Invoke();
    }

    /// <summary>Suelta conservando el momento que llevaba.</summary>
    public void Drop() => Release(Vector3.zero);

    /// <summary>Suelta conservando el momento y añadiendo una velocidad extra (botón de lanzar).</summary>
    public void Throw(Vector3 extraVelocity) => Release(extraVelocity);

    void Release(Vector3 extraVelocity)
    {
        if (!IsHeld) return;
        IsHeld = false;

        transform.SetParent(originalParent, worldPositionStays: true);

        if (rb)
        {
            // Primero quitar kinematic: a un rb kinemático no se le puede asignar velocidad
            rb.isKinematic = usedKinematic;
            rb.useGravity = usedGravity;

            if (!rb.isKinematic)
            {
                Vector3 v = Vector3.ClampMagnitude(HeldVelocity * throwMultiplier + extraVelocity, maxThrowSpeed);
                Vector3 w = Vector3.ClampMagnitude(HeldAngularVelocity, maxThrowAngularSpeed);

                rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, maxThrowAngularSpeed);
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = v;
#else
                rb.velocity = v;
#endif
                rb.angularVelocity = w;
            }
        }

        OnDropped?.Invoke();
    }

    // LateUpdate: después de animación, Animation Rigging y física → posición real que se ve
    void LateUpdate()
    {
        if (!IsHeld) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

        Vector3 lin = (pos - lastPos) / dt;

        Quaternion dq = rot * Quaternion.Inverse(lastRot);
        dq.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        Vector3 ang = (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || Mathf.Approximately(angle, 0f))
            ? Vector3.zero
            : axis.normalized * (angle * Mathf.Deg2Rad / dt);

        lastPos = pos;
        lastRot = rot;

        linSamples[sampleIndex] = lin;
        angSamples[sampleIndex] = ang;
        sampleIndex = (sampleIndex + 1) % linSamples.Length;
        sampleCount = Mathf.Min(sampleCount + 1, linSamples.Length);

        Vector3 linSum = Vector3.zero, angSum = Vector3.zero;
        for (int i = 0; i < sampleCount; i++)
        {
            linSum += linSamples[i];
            angSum += angSamples[i];
        }
        HeldVelocity = linSum / sampleCount;
        HeldAngularVelocity = angSum / sampleCount;
    }

    void ResetSamples()
    {
        sampleIndex = 0;
        sampleCount = 0;
        lastPos = transform.position;
        lastRot = transform.rotation;
        HeldVelocity = Vector3.zero;
        HeldAngularVelocity = Vector3.zero;
    }
}
