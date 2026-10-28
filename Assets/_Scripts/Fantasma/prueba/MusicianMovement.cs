using UnityEngine;

/// <summary>
/// Permite que el fantasma mueva físicamente al músico. Lo activa la acción ActionMoveMusician
/// y el input lo manda GhostController (este script no lee ningún input por sí mismo).
/// Añádelo a los músicos que quieras que se puedan mover.
/// Si el músico tiene un CharacterController se usa (con gravedad); si no, se mueve el transform.
/// </summary>
public class MusicianMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float gravity = -9.81f;
    [Tooltip("Al terminar la acción, el músico vuelve solo a su asiento")]
    [SerializeField] private bool returnToSeatWhenReleased = true;

    /// <summary>True mientras el fantasma controla el cuerpo del músico.</summary>
    public bool IsControlled { get; private set; }

    private CharacterController cc;
    private Vector3 seatPosition;
    private Quaternion seatRotation;
    private Vector2 moveInput;
    private float verticalVelocity;
    private bool returning;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        seatPosition = transform.position;
        seatRotation = transform.rotation;
    }

    public void SetControlled(bool controlled)
    {
        IsControlled = controlled;
        moveInput = Vector2.zero;
        returning = !controlled && returnToSeatWhenReleased;
    }

    // x = lateral, y = adelante/atrás (relativo a hacia dónde mira el cuerpo)
    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    // Gira el cuerpo en el eje vertical (lo usa GhostController con el ratón)
    public void Rotate(float yawDegrees)
    {
        transform.Rotate(0f, yawDegrees, 0f, Space.World);
    }

    private void Update()
    {
        if (IsControlled)
        {
            Vector3 direction = transform.TransformDirection(new Vector3(moveInput.x, 0f, moveInput.y));
            Move(direction * moveSpeed);
        }
        else if (returning)
        {
            ReturnToSeatStep();
        }
    }

    private void Move(Vector3 velocity)
    {
        if (cc != null)
        {
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
            verticalVelocity += gravity * Time.deltaTime;
            velocity.y = verticalVelocity;
            cc.Move(velocity * Time.deltaTime);
        }
        else
        {
            transform.position += velocity * Time.deltaTime;
        }
    }

    private void ReturnToSeatStep()
    {
        Vector3 toSeat = seatPosition - transform.position;
        toSeat.y = 0f;

        // Si en este frame llegamos al asiento, nos colocamos exactamente en él
        if (toSeat.magnitude <= moveSpeed * Time.deltaTime)
        {
            SnapToSeat();
            returning = false;
            return;
        }

        Vector3 direction = toSeat.normalized;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
        Move(direction * moveSpeed);
    }

    private void SnapToSeat()
    {
        // Un CharacterController activo pisa los cambios de transform.position
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(seatPosition, seatRotation);
        if (cc != null) cc.enabled = true;
    }
}