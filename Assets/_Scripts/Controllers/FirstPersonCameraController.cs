using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonCameraController : MonoBehaviour
{
    [SerializeField]
    Transform playerTransform;
    [SerializeField] private float playerRotationSpeed = 10f;
    [Tooltip("How far in degrees can you move the head up")]
    public float TopTransformClamp = 60.0f;
    [Tooltip("How far in degrees can you move the head down")]
    public float BottomTransformClamp = -30.0f;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;
    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 90.0f;
    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -90.0f;
    [Tooltip("Rotation speed of the character")]
    public float RotationSpeed = 1.0f;
    [SerializeField] private float cameraSmoothSpeed = 15f;

    [SerializeField]
    private PlayerInput _playerInput;


    // cinemachine
    private float _cinemachineTargetPitch;
    private float _rotationVelocity;
    private const float _threshold = 0.01f;
    private bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
        }
    }
    private void Update()
    {
        CameraRotation();

    }
    private void LateUpdate()
    {
        HandlePlayerRotation();
    }
    private Vector2 look = new Vector2();
    public void SetLookDir(InputAction.CallbackContext ctx)
    {
        look = ctx.ReadValue<Vector2>();
    }
    float _transformPitch;
    private float _cinemachineTargetYaw;
    private void CameraRotation()
    {
        // Si hay input del ratón o gamepad
        if (look.sqrMagnitude >= _threshold)
        {
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            // 1. Acumulamos el movimiento vertical y horizontal de la cámara
            _cinemachineTargetPitch += - look.y * RotationSpeed * deltaTimeMultiplier;

            float rawYawInput = look.x * RotationSpeed * deltaTimeMultiplier;
            _cinemachineTargetYaw += rawYawInput;

            // Limitamos el ángulo vertical (Pitch) de la cámara
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // 2. Aplicamos la rotación a la cámara con una curva suave (Lerp)
            Quaternion targetCamRotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);

            CinemachineCameraTarget.transform.localRotation = Quaternion.Lerp(
                CinemachineCameraTarget.transform.localRotation,
                targetCamRotation,
                Time.deltaTime * cameraSmoothSpeed
            );
        }


    }

    private void HandlePlayerRotation()
    {
        // Extraemos únicamente el eje Y (Yaw) del target de la cámara para que el cuerpo no se incline hacia arriba o abajo
        Quaternion targetBodyRotation = Quaternion.Euler(ClampAngle(_cinemachineTargetPitch,BottomTransformClamp,TopTransformClamp), _cinemachineTargetYaw, 0.0f);

        // Rotamos suavemente el transform del jugador hacia la orientación exacta de la cámara
        playerTransform.rotation = Quaternion.Slerp(
            playerTransform.rotation,
            targetBodyRotation,
            Time.deltaTime * playerRotationSpeed
        );
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

}
