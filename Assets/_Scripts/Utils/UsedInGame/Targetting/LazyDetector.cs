using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class LazyDetector : MonoBehaviour, ITargeter
{
    private enum DetectionShape { Sphere, Box, VisionCone }

    [SerializeField, ReadOnly] private GameObject lastTargetFetched = null;

    [Header("Physics")]
    [SerializeField] private float detectionRange = 2f;
    [SerializeField] private DetectionShape shape = DetectionShape.Sphere;

    [SerializeField, ShowIf("shape", DetectionShape.VisionCone)]
    [Range(0f, 360f)]
    public float visionAngle = 90f;

    [Header("Filters Configuration")]
    [SerializeField] public TargetFilter filter = new TargetFilter();

    public virtual GameObject[] GetAllTargets()
    {
        GameObject[] rawTargets = GetPhysicsTargets();
        return filter.Filter(rawTargets, transform);
    }

    public GameObject GetTarget()
    {
        return lastTargetFetched = GetClosestItem(GetAllTargets());
    }

    public bool HasTarget()
    {
        return GetAllTargets().Length > 0;
    }

    private GameObject[] GetPhysicsTargets()
    {
        Collider[] hits = System.Array.Empty<Collider>();
        LayerMask mask = filter.PhysicsLayerMask;

        if (shape == DetectionShape.Sphere)
        {
            hits = Physics.OverlapSphere(transform.position, detectionRange, mask);
        }
        else if (shape == DetectionShape.Box)
        {
            hits = Physics.OverlapBox(transform.position, Vector3.one * detectionRange * 0.5f, Quaternion.identity, mask);
        }
        else if (shape == DetectionShape.VisionCone)
        {
            // 1. Broad phase: Get everything within max range using OverlapSphere
            Collider[] sphericalHits = Physics.OverlapSphere(transform.position, detectionRange, mask);

            // 2. Narrow phase: Filter targets that fall within the cone angle
            List<GameObject> coneTargets = new List<GameObject>();
            float halfAngle = visionAngle * 0.5f;

            foreach (Collider col in sphericalHits)
            {
                Vector3 directionToTarget = (col.transform.position - transform.position);

                // Check if target is at the exact same position to prevent division by zero
                if (directionToTarget == Vector3.zero)
                {
                    coneTargets.Add(col.gameObject);
                    continue;
                }

                // Calculate the angle between transform.forward and the direction to the target
                float angleToTarget = Vector3.Angle(transform.forward, directionToTarget.normalized);

                if (angleToTarget <= halfAngle)
                {
                    coneTargets.Add(col.gameObject);
                }
            }
            return coneTargets.ToArray();
        }

        return hits.Select(c => c.gameObject).ToArray();
    }

    private GameObject GetClosestItem(GameObject[] targets)
    {
        GameObject closestItem = null;
        float closestDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;

        foreach (GameObject item in targets)
        {
            if (item == null) continue;
            float distance = Vector3.Distance(currentPosition, item.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestItem = item;
            }
        }

        return closestItem;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (shape == DetectionShape.Sphere)
        {
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
        else if (shape == DetectionShape.Box)
        {
            // Save the previous matrix so we don't affect other Gizmo drawings
            Matrix4x4 previousMatrix = Gizmos.matrix;

            // Apply the object's position, rotation, and scale to the Gizmo rendering pipeline
            Gizmos.matrix = transform.localToWorldMatrix;

            // Draw the wireframe cube centered at the local origin using the detection range as dimensions
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * detectionRange);

            // Optionally draw a subtle semi-transparent fill inside the box
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one * detectionRange);

            // Restore the matrix
            Gizmos.matrix = previousMatrix;
        }
        else if (shape == DetectionShape.VisionCone)
        {
#if UNITY_EDITOR
            float halfAngle = visionAngle * 0.5f;


            Vector3 bottomBoundaryPitch = transform.rotation * Quaternion.Euler(-halfAngle, 0, 0) * Vector3.forward;
            Vector3 topBoundaryPitch = transform.rotation * Quaternion.Euler(halfAngle, 0, 0) * Vector3.forward;
            Vector3 leftBoundaryYaw = transform.rotation * Quaternion.Euler(0, -halfAngle, 0) * Vector3.forward;
            Vector3 rightBoundaryYaw = transform.rotation * Quaternion.Euler(0, halfAngle, 0) * Vector3.forward;

            // 1. Draw horizontal arc (Yaw)
            Handles.color = new Color(0f, 1f, 1f, 0.1f);
            Handles.DrawSolidArc(transform.position, transform.up, leftBoundaryYaw, visionAngle, detectionRange);

            Handles.color = Color.cyan;
            Handles.DrawWireArc(transform.position, transform.up, leftBoundaryYaw, visionAngle, detectionRange);
            Gizmos.DrawRay(transform.position, leftBoundaryYaw * detectionRange);
            Gizmos.DrawRay(transform.position, rightBoundaryYaw * detectionRange);

            // 2. Draw vertical arc (Pitch)
            Handles.color = new Color(0f, 1f, 1f, 0.05f);
            Handles.DrawSolidArc(transform.position, transform.right, bottomBoundaryPitch, visionAngle, detectionRange);

            Handles.color = Color.cyan;
            Handles.DrawWireArc(transform.position, transform.right, bottomBoundaryPitch, visionAngle, detectionRange);
            Gizmos.DrawRay(transform.position, bottomBoundaryPitch * detectionRange);
            Gizmos.DrawRay(transform.position, topBoundaryPitch * detectionRange);

            // 3. Draw end cap ring to seal the cone visually
            Vector3 coneCenterEnd = transform.position + transform.forward * (detectionRange * Mathf.Cos(halfAngle * Mathf.Deg2Rad));
            float endRadius = detectionRange * Mathf.Sin(halfAngle * Mathf.Deg2Rad);
            Handles.DrawWireDisc(coneCenterEnd, transform.forward, endRadius);
#endif
        }
    }
}