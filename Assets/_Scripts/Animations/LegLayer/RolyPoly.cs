using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RolyPoly : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;
    [SerializeField]
    protected Quaternion _uprightJointTargetRot = Quaternion.Euler(0, 90, 0);
    [SerializeField] protected Quaternion offsetRotation;

    [SerializeField]
    protected float _uprightJointSpringStrength = 1;
    [SerializeField]
    protected float _uprightJointSpringDamper = 1;
    [Header("Freeze Rotations")]
    [SerializeField] protected bool FreezeX = false;
    [SerializeField] protected bool FreezeY = false;
    [SerializeField] protected bool FreezeZ = false;
    [Header("Upright Thresholds")]
    [SerializeField] private float thresholdX = 1f; // Tolerancia en grados para el eje X
    [SerializeField] private float thresholdY = 1f; // Tolerancia en grados para el eje Y
    [SerializeField] private float thresholdZ = 1f; // Tolerancia en grados para el eje Z

    void Start()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

    }
    public void UpdateTargetRot(Vector3 lookDirection)
    {
        _uprightJointTargetRot = Quaternion.LookRotation(lookDirection) * offsetRotation;
    }

    public void UpdateTargetRotFromTransform(Transform copyTransform)
    {
        UpdateTargetRot(copyTransform.forward);
    }
    // Update is called once per frame
    void Update()
    {
        UpdateUprightForce();
    }
    public void UpdateUprightForce()
    {
        Quaternion characterCurrent = transform.rotation;
        Quaternion toGoal = ShortestRotation(_uprightJointTargetRot, characterCurrent);
        Vector3 euler = toGoal.eulerAngles;
        float ex = Mathf.DeltaAngle(0, euler.x);
        float ey = Mathf.DeltaAngle(0, euler.y);
        float ez = Mathf.DeltaAngle(0, euler.z);

        // 2. Aplicamos el umbral (threshold) por cada eje de forma independiente
        if (Mathf.Abs(ex) < thresholdX) ex = 0f;
        if (Mathf.Abs(ey) < thresholdY) ey = 0f;
        if (Mathf.Abs(ez) < thresholdZ) ez = 0f;

        // 3. Si todos los ejes están dentro de su umbral, no es necesario aplicar torque
        if (ex == 0f && ey == 0f && ez == 0f)
        {
            return;
        }

        // 4. Reconstruimos el Quaternion filtrado y obtenemos su eje y ángulo
        Quaternion filteredGoal = Quaternion.Euler(ex, ey, ez);
        Vector3 rotAxis;
        float rotDegrees;
        filteredGoal.ToAngleAxis(out rotDegrees, out rotAxis);

        if (FreezeX) { rotAxis.x = 0; }
        if (FreezeY) { rotAxis.y = 0; }
        if (FreezeZ) { rotAxis.z = 0; }

        // Si tras congelar los ejes el vector queda inútil, salimos
        if (rotAxis.sqrMagnitude < 0.0001f) return;

        rotAxis.Normalize();

        float rotRadians = rotDegrees * Mathf.Deg2Rad;

        // 5. Aplicamos el torque con la corrección ya filtrada
        _rb.AddTorque((rotAxis * (rotRadians * _uprightJointSpringStrength)) 
        - (_rb.angularVelocity * _uprightJointSpringDamper));
    }

    protected Quaternion ShortestRotation(Quaternion to, Quaternion from)
    {
        // Invertimos el Quaternion "from" para obtener la rotaci�n relativa
        Quaternion inverseFrom = Quaternion.Inverse(from);

        // Multiplicamos "to" por el inverso de "from" para obtener la rotaci�n que lo lleva a "to"
        Quaternion deltaRotation = to * inverseFrom;

        // Aseguramos que tomamos el camino m�s corto
        if (Quaternion.Dot(deltaRotation, Quaternion.identity) < 0f)
        {
            deltaRotation = new Quaternion(-deltaRotation.x, -deltaRotation.y, -deltaRotation.z, -deltaRotation.w);
        }

        return deltaRotation;
    }

}
