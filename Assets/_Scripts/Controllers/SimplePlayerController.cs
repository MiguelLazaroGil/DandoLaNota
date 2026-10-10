using System.Collections;
using System.Collections.Generic;
using Character.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerController : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float speed = 2f;
    public float Speed { get => speed; set => speed = value; }

    [Header("Referencies")]
    [Tooltip("Camera transform to get proper movement")]
    [SerializeField] private Transform cameraTransform;

    private Vector2 inputDir = Vector2.zero;
    private Vector2 _smoothedMovementInput;
    private Vector2 _movementInputSmoothVelocity;

    private void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Si no asignas la cámara en el inspector, intenta buscar la cámara principal automáticamente
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        inputDir = ctx.ReadValue<Vector2>();
    }

    private void Update()
    {
        // Suavizamos el input del usuario
        _smoothedMovementInput = Vector2.SmoothDamp(
            _smoothedMovementInput,
            inputDir,
            ref _movementInputSmoothVelocity,
            0.1f);

        Vector3 moveDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            // 1. Obtenemos las direcciones de la cámara y anulamos el eje Y (para que sea totalmente plano)
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            // 2. Combinamos el input con la orientación de la cámara
            // input.y maneja adelante/atrás (cameraForward)
            // input.x maneja izquierda/derecha (cameraRight)
            moveDirection = (cameraForward * _smoothedMovementInput.y) + (cameraRight * _smoothedMovementInput.x);
        }
        else
        {
            // Fallback por si la cámara no está asignada (movimiento en coordenadas del mundo)
            moveDirection = new Vector3(_smoothedMovementInput.x, 0, _smoothedMovementInput.y);
        }

        // 3. Aplicamos la velocidad manteniendo la inercia vertical (gravedad) del Rigidbody
        rb.linearVelocity = new Vector3(moveDirection.x * speed, rb.linearVelocity.y, moveDirection.z * speed);
    }
}
