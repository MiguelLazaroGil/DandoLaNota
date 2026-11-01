using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations; // Necesario para ParentConstraint
using UnityEngine.Events;

[RequireComponent(typeof(ParentConstraint))]
public class Grabbable : NetworkBehaviour
{
    [Header("Events")]
    public UnityEvent OnGrabbed;
    public UnityEvent OnDropped;

    [Header("When dropped/thrown")]
    [SerializeField, Range(1, 20)] int velocitySamples = 6;
    [SerializeField] float throwMultiplier = 1f;
    [SerializeField] float maxThrowSpeed = 20f;          // m/s
    [SerializeField] float maxThrowAngularSpeed = 30f;

    [Header("When grabbed transform configuration")]
    [SerializeField] bool IsworldPositionStaying = false;
    [SerializeField] Vector3 grabPositionOffset = Vector3.zero;
    [SerializeField] Vector3 grabRotationOffset = Vector3.zero;
    [SerializeField] List<Collider> myColliders = new();

    Rigidbody rb;
    ParentConstraint parentConstraint;

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

    List<Collider> combinedColliders = new List<Collider>();

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Obtener el ParentConstraint o añadirlo en runtime si no existe
        if (!TryGetComponent(out parentConstraint))
        {
            parentConstraint = gameObject.AddComponent<ParentConstraint>();
        }

        linSamples = new Vector3[velocitySamples];
        angSamples = new Vector3[velocitySamples];
    }

    // ========================================================================
    // 1. FLUJO DE AGARRAR (GRAB)
    // ========================================================================

    public void Grab(Transform pivot)
    {
        if (IsHeld) return;

        if (IsNetworkActive)
        {
            if (IsServer)
            {
                PerformGrabServer(pivot);
            }
            else
            {
                RequestGrabServer(pivot);
            }
        }
        else
        {
            ExecuteGrabLocal(pivot);
        }
    }

    private void RequestGrabServer(Transform pivot)
    {
        if (pivot == null) return;

        NetworkObject pivotNetObj = pivot.GetComponentInParent<NetworkObject>();
        if (pivotNetObj != null)
        {
            string relativePath = GetRelativePath(pivotNetObj.transform, pivot);
            GrabServerRpc(pivotNetObj, relativePath);
        }
        else
        {
            Debug.LogWarning($"[Grabbable] No se puede agarrar en red: el objeto '{pivot.name}' no tiene un NetworkObject en sus padres.");
        }
    }

    private void PerformGrabServer(Transform pivot)
    {
        if (IsHeld) return;

        ExecuteGrabLocal(pivot);

        NetworkObject pivotNetObj = pivot != null ? pivot.GetComponentInParent<NetworkObject>() : null;
        string relativePath = pivotNetObj != null ? GetRelativePath(pivotNetObj.transform, pivot) : "";

        NotifyGrabClientRpc(pivotNetObj != null ? pivotNetObj : default(NetworkObjectReference), relativePath);
    }

    [Rpc(SendTo.Server)]
    private void GrabServerRpc(NetworkObjectReference pivotNetObjRef, string relativePath)
    {
        if (pivotNetObjRef.TryGet(out NetworkObject netObj))
        {
            Transform pivot = GetTransformFromPath(netObj.transform, relativePath);
            PerformGrabServer(pivot);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyGrabClientRpc(NetworkObjectReference pivotNetObjRef, string relativePath)
    {
        if (IsServer) return; // El servidor ya lo ejecutó

        Transform pivot = null;
        if (pivotNetObjRef.TryGet(out NetworkObject netObj))
        {
            pivot = GetTransformFromPath(netObj.transform, relativePath);
        }
        ExecuteGrabLocal(pivot);
    }

    private void ExecuteGrabLocal(Transform pivot)
    {
        if (IsHeld) return;
        IsHeld = true;

        if (rb)
        {
            usedGravity = rb.useGravity;
            usedKinematic = rb.isKinematic;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (pivot != null)
        {
            // 1. Ignorar colisiones con el portador
            combinedColliders.Clear();
            Transform childTransform = pivot.transform.parent != null ? pivot.transform.parent : pivot;
            combinedColliders.AddRange(childTransform.GetComponentsInChildren<Collider>(true));
            combinedColliders.AddRange(pivot.GetComponentsInParent<Collider>(true));

            foreach (var carrierCol in combinedColliders)
            {
                foreach (var myCol in myColliders)
                {
                    if (carrierCol != null && myCol != null)
                    {
                        Physics.IgnoreCollision(carrierCol, myCol, true);
                    }
                }
            }

            // 2. Configurar el ParentConstraint nativo
            parentConstraint.constraintActive = false;

            while (parentConstraint.sourceCount > 0)
            {
                parentConstraint.RemoveSource(0);
            }

            ConstraintSource source = new ConstraintSource
            {
                sourceTransform = pivot,
                weight = 1f
            };
            parentConstraint.AddSource(source);

            // Aplicar los offsets de posición y rotación
            if (IsworldPositionStaying)
            {
                parentConstraint.SetTranslationOffset(0, pivot.InverseTransformPoint(transform.position));
                parentConstraint.SetRotationOffset(0, (Quaternion.Inverse(pivot.rotation) * transform.rotation).eulerAngles);
            }
            else
            {
                parentConstraint.SetTranslationOffset(0, grabPositionOffset);
                parentConstraint.SetRotationOffset(0, grabRotationOffset);
            }

            parentConstraint.constraintActive = true;
        }

        ResetSamples();
        OnGrabbed?.Invoke();
    }

    // ========================================================================
    // 2. FLUJO DE SOLTAR / LANZAR (RELEASE / DROP / THROW)
    // ========================================================================

    public void Drop() => Release(Vector3.zero);

    public void Throw(Vector3 extraVelocity) => Release(extraVelocity);

    private void Release(Vector3 extraVelocity)
    {
        if (!IsHeld) return;

        if (IsNetworkActive)
        {
            if (IsServer)
            {
                PerformReleaseServer(extraVelocity);
            }
            else
            {
                ReleaseServerRpc(extraVelocity);
            }
        }
        else
        {
            ExecuteReleaseLocal(extraVelocity);
        }
    }

    private void PerformReleaseServer(Vector3 extraVelocity)
    {
        if (!IsHeld) return;

        ExecuteReleaseLocal(extraVelocity);
        NotifyReleaseClientRpc(extraVelocity);
    }

    [Rpc(SendTo.Server)]
    private void ReleaseServerRpc(Vector3 extraVelocity)
    {
        PerformReleaseServer(extraVelocity);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyReleaseClientRpc(Vector3 extraVelocity)
    {
        if (IsServer) return; // El servidor ya lo ejecutó

        ExecuteReleaseLocal(extraVelocity);
    }

    private void ExecuteReleaseLocal(Vector3 extraVelocity)
    {
        if (!IsHeld) return;
        IsHeld = false;

        // Desactivar el constraint para liberar el transform
        if (parentConstraint != null)
        {
            parentConstraint.constraintActive = false;
        }

        if (rb)
        {
            rb.isKinematic = usedKinematic;
            rb.isKinematic = false;
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

        foreach (var carrierCol in combinedColliders)
        {
            foreach (var myCol in myColliders)
            {
                if (carrierCol != null && myCol != null)
                {
                    Physics.IgnoreCollision(carrierCol, myCol, false);
                }
            }
        }
        combinedColliders.Clear();

        OnDropped?.Invoke();
    }

    // ========================================================================
    // 3. CÁLCULO DE VELOCIDAD DE LANZAMIENTO
    // ========================================================================

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

    // ========================================================================
    // MÉTODOS AUXILIARES DE RUTAS EN JERARQUÍA
    // ========================================================================

    private string GetRelativePath(Transform root, Transform target)
    {
        if (root == target) return "";
        string path = target.name;
        Transform current = target.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private Transform GetTransformFromPath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path)) return root;
        return root.Find(path);
    }
}