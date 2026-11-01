using UnityEngine;

public class SettingsApplierDirector : MonoBehaviour
{
    [SerializeField]
    FirstPersonCameraController firstPersonCameraController;

    public void ApplyMouseSensibility(float ApplyMouseSensibility)
    {
        Mathf.Clamp01(ApplyMouseSensibility);
        firstPersonCameraController.RotationSpeed = ApplyMouseSensibility;
    }
}
