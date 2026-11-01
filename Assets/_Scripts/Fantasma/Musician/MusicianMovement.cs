using UnityEngine;

/// <summary>
/// Permite que el fantasma mueva físicamente al músico. Lo activa la acción ActionMoveMusician
/// y el input lo manda el sistema del fantasma (este script no lee ningún input por sí mismo).
/// Añádelo a los músicos que quieras que se puedan mover.
///
/// Si el músico tiene un RolyPoly (esqueleto con físicas), la acción ActionMoveMusician se encarga de moverlo con
/// físicas y este script solo hace de "buzón": guarda el input de movimiento y hacia dónde mira el jugador (LookYaw)
/// para que la acción los lea (ExternalDrive = true).
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

    /// <summary>El Rigidbody que mueve la acción (solo con ExternalDrive). La cámara de posesión lo sigue.</summary>
    public Transform PhysicsBody { get; private set; }

    /// <summary>True mientras el cuerpo anda por su cuenta: lo controla el jugador o vuelve al asiento con físicas.</summary>
    public bool IsBodyDriven => IsControlled || ExternalDrive;

    public float MoveSpeed => moveSpeed;
    public bool ReturnToSeatWhenReleased => returnToSeatWhenReleased;

    /// <summary>Último input de movimiento (x = lateral, y = adelante/atrás).</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>
    /// Hacia dónde QUIERE mirar el jugador (grados, en el mundo): el objetivo de giro del cuerpo. Lo publica la cámara
    /// de posesión cada frame (lo mueve el ratón mientras el cuerpo está libre). El cuerpo lo persigue; la cámara, en
    /// cambio, mira hacia donde mira el cuerpo de verdad (ver TryGetBodyLookYaw). null = nadie lo está publicando.
    /// </summary>
    public float? LookYaw { get; private set; }

    private CharacterController cc;
    private Vector3 seatPosition;
    private Quaternion seatRotation;
    private Vector2 moveInput;
    private float verticalVelocity;
    private bool returning;
    private RolyPoly roly;           // esqueleto con físicas del músico (null si no tiene)
    private bool rolySearched;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        seatPosition = transform.position;
        seatRotation = transform.rotation;
    }

    // Lo llama la acción cuando va a mover al músico ella misma (con físicas)
    // body = el Rigidbody que mueve la acción (la cámara de posesión lo sigue). Solo se usa con external = true.
    public void SetExternalDrive(bool external, Transform body = null)
    {
        ExternalDrive = external;
        PhysicsBody = external ? body : null;
    }

    public void SetControlled(bool controlled)
    {
        IsControlled = controlled;
        moveInput = Vector2.zero;
        returning = !controlled && returnToSeatWhenReleased && !ExternalDrive;
    }

    // x = lateral, y = adelante/atrás (relativo a hacia dónde mira el cuerpo)
    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    // El RolyPoly del músico, o null si no tiene o su objeto no tiene Rigidbody (entonces se mueve sin físicas).
    // Se busca igual que en ActionMoveMusician: en él o en sus hijos, y si no en sus padres.
    private RolyPoly FindRoly()
    {
        if (rolySearched) return roly;
        rolySearched = true;

        roly = GetComponentInChildren<RolyPoly>(true);
        if (roly == null) roly = GetComponentInParent<RolyPoly>(true);
        if (roly != null && roly.GetComponent<Rigidbody>() == null) roly = null;

        return roly;
    }

    /// <summary>
    /// Hacia dónde mira el cuerpo físico del músico (grados, en el mundo), leído de la rotación actual de su RolyPoly.
    /// Devuelve false si el músico no tiene RolyPoly con Rigidbody.
    /// </summary>
    public bool TryGetBodyLookYaw(out float yawDegrees)
    {
        RolyPoly body = FindRoly();
        if (body != null)
        {
            Vector3 look = body.GetLookDirection();
            if (look.sqrMagnitude > 0.0001f)
            {
                yawDegrees = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                return true;
            }
        }

        yawDegrees = 0f;
        return false;
    }

    // Lo llama la cámara de posesión cada frame con hacia dónde quiere mirar el jugador (grados, en el mundo).
    // Sin ExternalDrive el cuerpo gira aquí (en Update); con ExternalDrive la acción lo lee y se lo pasa al RolyPoly.
    public void SetLookYaw(float yawDegrees)
    {
        LookYaw = yawDegrees;
    }

    // Lo llama la cámara de posesión al terminar la posesión
    public void ClearLookYaw()
    {
        LookYaw = null;
    }

    private void Update()
    {
        if (ExternalDrive) return;   // lo mueve la acción con físicas

        if (IsControlled)
        {
            // El cuerpo mira hacia donde mira el jugador
            if (LookYaw.HasValue) transform.rotation = Quaternion.Euler(0f, LookYaw.Value, 0f);

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