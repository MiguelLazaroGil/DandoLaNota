using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    void Start()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

    }
    public void UpdateTargetRot(Vector3 lookDirection)
    {
        _uprightJointTargetRot = Quaternion.LookRotation(lookDirection) * offsetRotation;
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
        Vector3 rotAxis;
        float rotDegrees;
        toGoal.ToAngleAxis(out rotDegrees, out rotAxis);
        if (FreezeX) { rotAxis.x = 0; }
        if (FreezeY) { rotAxis.y = 0; }
        if (FreezeZ) { rotAxis.z = 0; }
        rotAxis.Normalize();

        float rotRadians = rotDegrees * Mathf.Deg2Rad;

        _rb.AddTorque((rotAxis * (rotRadians * _uprightJointSpringStrength)) - (_rb.angularVelocity * _uprightJointSpringDamper));

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
