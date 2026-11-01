using UnityEngine;

/// <summary>
/// Permite que el fantasma mueva físicamente al músico. Lo activa la acción ActionMoveMusician
/// y el input lo manda el sistema del fantasma (este script no lee ningún input por sí mismo).
/// Añádelo a los músicos que quieras que se puedan mover.
///
/// Si el músico tiene un RolyPoly (esqueleto con físicas), la acción ActionMoveMusician se encarga de moverlo con
/// físicas y este script solo hace de "buzón": guarda el input de movimiento y el giro del ratón para que la acción
/// los lea (ExternalDrive = true).
/// Si no tiene físicas, se mueve aquí: con CharacterController (con gravedad) o, si no hay, moviendo el transform.
/// </summary>
public class MusicianMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float gravity = -9.81f;
    [Tooltip("Al terminar la acción, el músico vuelve solo a su asiento")]
    [SerializeField] private bool returnToSeatWhenReleased = true;

    /// <summary>True mientras el fantasma controla el cuerpo del músico.</summary>
    public bool IsControlled { get; private set; }

    /// <summary>True si el movimiento lo hace la acción (esqueleto con físicas) y no este script.</summary>
    public bool ExternalDrive { get; private set; }

    public float MoveSpeed => moveSpeed;
    public bool ReturnToSeatWhenReleased => returnToSeatWhenReleased;

    /// <summary>Último input de movimiento (x = lateral, y = adelante/atrás).</summary>
    public Vector2 MoveInput => moveInput;

    private CharacterController cc;
    private Vector3 seatPosition;
    private Quaternion seatRotation;
    private Vector2 moveInput;
    private float verticalVelocity;
    private bool returning;
    private float pendingYaw;   // giro acumulado del ratón que aún no ha leído la acción (solo con ExternalDrive)

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        seatPosition = transform.position;
        seatRotation = transform.rotation;
    }

    // Lo llama la acción cuando va a mover al músico ella misma (con físicas)
    public void SetExternalDrive(bool external)
    {
        ExternalDrive = external;
        pendingYaw = 0f;
    }

    public void SetControlled(bool controlled)
    {
        IsControlled = controlled;
        moveInput = Vector2.zero;
        pendingYaw = 0f;
        returning = !controlled && returnToSeatWhenReleased && !ExternalDrive;
    }

    // x = lateral, y = adelante/atrás (relativo a hacia dónde mira el cuerpo)
    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    // Gira el cuerpo en el eje vertical (lo usa el sistema del fantasma con el ratón).
    // Con ExternalDrive el giro no se aplica aquí: se acumula para que la acción lo convierta en la dirección objetivo del RolyPoly.
    public void Rotate(float yawDegrees)
    {
        if (ExternalDrive)
            pendingYaw += yawDegrees;
        else
            transform.Rotate(0f, yawDegrees, 0f, Space.World);
    }

    /// <summary>Devuelve el giro acumulado desde la última vez y lo reinicia (lo lee la acción).</summary>
    public float TakeYaw()
    {
        float yaw = pendingYaw;
        pendingYaw = 0f;
        return yaw;
    }

    private void Update()
    {
        if (ExternalDrive) return;   // lo mueve la acción con físicas

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