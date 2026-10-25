using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpringDownForceDouble : MonoBehaviour
{

    [SerializeField]
    Rigidbody rb;

    [Header("SPRING PROPERTIES")]
    [SerializeField] Vector3 originOffset = Vector3.zero;
    [SerializeField] Vector3 rayVector = Vector3.down;
    [SerializeField] float legsSeparation = 0.5f;
    [SerializeField] float height = 1.0f;
    [SerializeField] float SpringStrength = 2.0f;
    [SerializeField] float SpringDamper = 1.5f;
    [SerializeField] LayerMask IgnoreMask;
    RaycastHit hitRight;
    RaycastHit hitLeft;
    bool hitRightDetected;
    bool hitLeftDetected;




    private void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

    }
    void FixedUpdate()
    {
        // Calculamos el origen base aplicando el offset local
        Vector3 baseOrigin = transform.TransformPoint(originOffset);

        // --- RAYCASTA DERECHA ---
        Vector3 rightOrigin = baseOrigin + transform.right * legsSeparation / 2;
        hitRightDetected = Physics.Raycast(rightOrigin, rayVector, out hitRight, height, ~IgnoreMask);

        if (hitRightDetected)
        {
            Vector3 vel = rb.linearVelocity;
            Vector3 otherVel = Vector3.zero;
            Rigidbody hitBody = hitRight.rigidbody;
            if (hitBody != null)
            {
                otherVel = hitBody.linearVelocity;
            }
            float rayDirVel = Vector3.Dot(rayVector, vel);
            float otherDirVel = Vector3.Dot(rayVector, otherVel);

            float relVel = rayDirVel - otherDirVel;
            float x = hitRight.distance - height;
            float springForce = (x * SpringStrength) - (relVel * SpringDamper);
            rb.AddForce((rayVector * springForce) / 2);
        }

        // --- RAYCASTA IZQUIERDA ---
        Vector3 leftOrigin = baseOrigin - transform.right * legsSeparation / 2;
        hitLeftDetected = Physics.Raycast(leftOrigin, rayVector, out hitLeft, height, ~IgnoreMask);

        if (hitLeftDetected)
        {
            Vector3 vel = rb.linearVelocity;
            Vector3 otherVel = Vector3.zero;
            Rigidbody hitBody = hitLeft.rigidbody;
            if (hitBody != null)
            {
                otherVel = hitBody.linearVelocity;
            }
            float rayDirVel = Vector3.Dot(rayVector, vel);
            float otherDirVel = Vector3.Dot(rayVector, otherVel);

            float relVel = rayDirVel - otherDirVel;
            float x = hitLeft.distance - height;
            float springForce = (x * SpringStrength) - (relVel * SpringDamper);
            rb.AddForce((rayVector * springForce) / 2);
        }
    }
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 baseOrigin = transform.TransformPoint(originOffset);

        Vector3 rightOrigin = baseOrigin + transform.right * legsSeparation / 2;
        Vector3 leftOrigin = baseOrigin - transform.right * legsSeparation / 2;

        float rightLength = hitRightDetected ? hitRight.distance : height;
        float leftLength = hitLeftDetected ? hitLeft.distance : height;

        Gizmos.DrawRay(rightOrigin, rayVector * rightLength);
        Gizmos.DrawRay(leftOrigin, rayVector * leftLength);

        Gizmos.DrawSphere(rightOrigin + rayVector * rightLength, 0.1f);
        Gizmos.DrawSphere(leftOrigin + rayVector * leftLength, 0.1f);
    }
#endif

}
