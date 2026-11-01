using UnityEngine;

public class SettingsApplierDirector : MonoBehaviour
{
    [SerializeField]
    GVLoadSystem.Core.GVEntryReference gVEntry;
    [SerializeField]
    FirstPersonCameraController firstPersonCameraController;
    private void Start()
    {
        applySetting();
    }
    private void OnEnable()
    {
        PauseHandler.OnResume.AddListener(applySetting);
    }
    private void OnDisable()
    {

        PauseHandler.OnResume.RemoveListener(applySetting);
    }
    private void applySetting()
    {
        ApplyMouseSensibility(gVEntry.Get<float>());
    }
    public void ApplyMouseSensibility(float ApplyMouseSensibility)
    {
        Mathf.Clamp01(ApplyMouseSensibility);
        firstPersonCameraController.RotationSpeed = ApplyMouseSensibility;
    }
}
