using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Mover al músico: mientras está activa, el jugador camina con WASD y gira con el ratón con el cuerpo del músico.
// Se apaga eligiéndola otra vez en la rueda o al salir del músico.
//
// El input lo recoge el sistema del fantasma y lo deja en MusicianMovement (que hace de "buzón").
// Esta acción decide cómo se mueve el cuerpo:
//  - Si el músico tiene un RolyPoly (esqueleto con físicas): se empuja con FUERZAS el Rigidbody del RolyPoly y se le
//    pasa al RolyPoly la dirección a la que mira (UpdateTargetRot). Nunca se toca el transform: las físicas lo pisarían.
//    Al soltar la acción, vuelve caminando al asiento.
//  - Si no lo tiene: movimiento clásico de MusicianMovement (CharacterController / transform).
[CreateAssetMenu(menuName = "Fantasma/Acciones/Mover Músico")]
public class ActionMoveMusician : SabotageAction
{
    [Header("Movimiento con físicas (RolyPoly)")]
    [Tooltip("Aceleración máxima (m/s²) con la que se empuja al músico para alcanzar la velocidad. " +
             "Súbela si le cuesta arrancar o se arrastra. La velocidad es el Move Speed de su MusicianMovement.")]
    [SerializeField] private float maxAcceleration = 40f;
    [Tooltip("Distancia (m) al asiento a la que se da por llegado al volver")]
    [SerializeField] private float seatArriveDistance = 0.15f;
    [Tooltip("Segundos que se queda corrigiendo la orientación al llegar al asiento, antes de devolver las físicas a como estaban")]
    [SerializeField] private float seatSettleTime = 0.8f;
    [Tooltip("Si en estos segundos no llega al asiento, deja de empujarlo (por si algo lo bloquea)")]
    [SerializeField] private float returnTimeout = 20f;
    [Tooltip("Escribe en la consola qué RolyPoly / Rigidbody se ha encontrado y avisa de los problemas típicos")]
    [SerializeField] private bool debugLog = true;

    // Esta acción SIEMPRE tiene que ser continua (si no, nadie la pararía) y solo la puede usar el jugador
    private void OnValidate()
    {
        isContinuous = true;
        playerOnly = true;
    }

    // Estado por músico: el asset es compartido por todos los músicos, así que no puede ir en campos sueltos
    private class DriveState
    {
        public Vector3 seatPosition;
        public float seatYaw;
        public bool rolyWasEnabled;
        public bool wasKinematic;
    }

    private readonly Dictionary<Musician, DriveState> states = new Dictionary<Musician, DriveState>();
    private readonly Dictionary<Musician, Coroutine> routines = new Dictionary<Musician, Coroutine>();

    // ───────────────────────── Activar / desactivar ─────────────────────────

    protected override void Execute(Musician target)
    {
        MusicianMovement movement = target.Movement;
        if (movement == null)
        {
            Debug.LogWarning($"{target.name} no tiene el componente MusicianMovement, no se puede mover.");
            return;
        }

        RolyPoly roly = FindRolyPoly(target);
        Rigidbody rb = roly != null ? roly.GetComponent<Rigidbody>() : null;

        if (roly != null && rb == null)
            Debug.LogWarning($"{target.name}: el RolyPoly '{roly.name}' no tiene Rigidbody en su mismo objeto. " +
                             "Se mueve sin físicas.", target);

        if (rb == null)
        {
            // Sin esqueleto con físicas: movimiento clásico
            movement.SetExternalDrive(false);
            movement.SetControlled(true);
        }
        else
        {
            StartPhysicsDrive(target, movement, roly, rb);
        }

        ApplyNegativeEffect(target); // Si se levanta de la silla, toca peor
    }

    protected override void Stop(Musician target)
    {
        MusicianMovement movement = target.Movement;
        if (movement == null) return;

        // Con físicas, la corrutina nota que ya no está controlado y empieza a volver al asiento
        movement.SetControlled(false);
        StopNegativeEffect(target);
    }

    // El RolyPoly del músico: en él o en sus hijos, y si no en sus padres.
    // (No se busca desde la raíz de la escena: se cogería el de otro músico.)
    private static RolyPoly FindRolyPoly(Musician target)
    {
        RolyPoly roly = target.GetComponentInChildren<RolyPoly>(true);
        if (roly == null) roly = target.GetComponentInParent<RolyPoly>(true);
        return roly;
    }

    private void StartPhysicsDrive(Musician target, MusicianMovement movement, RolyPoly roly, Rigidbody rb)
    {
        // Se guarda cómo estaban las físicas ANTES de tocar nada (solo la primera vez), para devolverlas al terminar
        DriveState state = GetState(target, roly, rb);

        // Si había una corrutina de una activación anterior (p. ej. volviendo al asiento), se cancela
        if (routines.TryGetValue(target, out Coroutine old) && old != null)
            target.StopCoroutine(old);

        // Para moverse con físicas el RolyPoly tiene que estar activo y el Rigidbody no puede ser kinemático
        roly.enabled = true;
        rb.isKinematic = false;

        movement.SetExternalDrive(true, rb.transform);   // MusicianMovement deja de mover el transform y solo recoge el input; la cámara sigue a este cuerpo
        movement.SetControlled(true);

        Report(target, roly, rb, state);

        routines[target] = target.StartCoroutine(DriveRoutine(target, movement, roly, rb, state));
    }

    private DriveState GetState(Musician target, RolyPoly roly, Rigidbody rb)
    {
        if (!states.TryGetValue(target, out DriveState state))
        {
            // Hacia dónde mira el cuerpo en su asiento, leído de sus físicas
            float seatYaw;
            if (target.Movement == null || !target.Movement.TryGetBodyLookYaw(out seatYaw))
                seatYaw = FlatYaw(target.CameraPoint, target.transform);

            state = new DriveState
            {
                seatPosition = rb.position,
                seatYaw = seatYaw,
                rolyWasEnabled = roly.enabled,
                wasKinematic = rb.isKinematic
            };
            states[target] = state;
        }
        return state;
    }

    // ───────────────────────── Movimiento con físicas ─────────────────────────

    private IEnumerator DriveRoutine(Musician target, MusicianMovement movement, RolyPoly roly, Rigidbody rb, DriveState state)
    {
        float speed = movement.MoveSpeed;

        // Solo se usa si no se puede leer hacia dónde mira el cuerpo (no debería pasar con un RolyPoly con Rigidbody)
        float fallbackYaw = FlatYaw(target.CameraPoint, target.transform);

        // 1) El jugador lo controla
        while (movement.IsControlled)
        {
            // Hacia dónde mira el cuerpo AHORA, leído de sus físicas: es lo que ve la cámara y la referencia de WASD
            float bodyYaw = movement.TryGetBodyLookYaw(out float lookedYaw) ? lookedYaw : fallbackYaw;

            // Hacia dónde QUIERE mirar el jugador (lo mueve el ratón y lo publica PossessionCamera). El cuerpo lo
            // persigue gracias al torque del RolyPoly. Si nadie lo publica, se queda mirando hacia donde ya miraba.
            float targetYaw = movement.LookYaw ?? bodyYaw;

            Vector2 input = movement.MoveInput;
            Vector3 direction = Quaternion.Euler(0f, bodyYaw, 0f) * new Vector3(input.x, 0f, input.y);

            roly.UpdateTargetRot(YawToDirection(targetYaw));
            DrivePlanarVelocity(rb, direction * speed);

            yield return new WaitForFixedUpdate();
        }

        // 2) Soltado: vuelve caminando al asiento (si está activado en su MusicianMovement)
        if (movement.ReturnToSeatWhenReleased)
        {
            bool arrived = false;
            float elapsed = 0f;

            while (elapsed < returnTimeout)
            {
                Vector3 toSeat = state.seatPosition - rb.position;
                toSeat.y = 0f;

                if (toSeat.magnitude <= seatArriveDistance)
                {
                    arrived = true;
                    break;
                }

                // Mira hacia donde camina
                Vector3 direction = toSeat.normalized;
                roly.UpdateTargetRot(direction);
                DrivePlanarVelocity(rb, direction * speed);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (arrived)
            {
                // 3) Llegada: pequeña corrección horizontal (sin tocar la altura), quieto y mirando como al principio
                Vector3 p = rb.position;
                rb.position = new Vector3(state.seatPosition.x, p.y, state.seatPosition.z);

                Vector3 v = GetVelocity(rb);
                SetVelocity(rb, new Vector3(0f, v.y, 0f));

                Vector3 seatDirection = YawToDirection(state.seatYaw);
                float settle = 0f;
                while (settle < seatSettleTime)
                {
                    roly.UpdateTargetRot(seatDirection);
                    DrivePlanarVelocity(rb, Vector3.zero);

                    settle += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }

                // Las físicas vuelven a como estaban antes de empezar la acción
                rb.isKinematic = state.wasKinematic;
                roly.enabled = state.rolyWasEnabled;
            }
            else if (debugLog)
            {
                Debug.LogWarning($"[{actionName}] {target.name} no ha llegado al asiento en {returnTimeout:0} s " +
                                 "(¿algo lo bloquea?). Se deja donde está.", target);
            }
        }

        movement.SetExternalDrive(false);
        routines.Remove(target);
    }

    // Empuja al cuerpo (con fuerza, no con teletransporte) hasta que su velocidad horizontal sea "target".
    // La vertical no se toca: de la gravedad se encargan las físicas.
    private void DrivePlanarVelocity(Rigidbody rb, Vector3 target)
    {
        Vector3 v = GetVelocity(rb);
        Vector3 planar = new Vector3(v.x, 0f, v.z);

        Vector3 acceleration = (target - planar) / Time.fixedDeltaTime;
        acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);

        rb.AddForce(acceleration, ForceMode.Acceleration);
    }

    // Hacia dónde mira un transform (grados en el plano horizontal)
    private static float FlatYaw(Transform t, Transform fallback)
    {
        Vector3 forward = t.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = fallback.forward;
            forward.y = 0f;
        }
        if (forward.sqrMagnitude < 0.0001f) return 0f;

        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private static Vector3 YawToDirection(float yawDegrees)
    {
        return Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
    }

    // Unity 6 renombró Rigidbody.velocity a linearVelocity
    private static Vector3 GetVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocity(Rigidbody rb, Vector3 value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
        rb.velocity = value;
#endif
    }

    // ───────────────────────── Diagnóstico ─────────────────────────

    private void Report(Musician target, RolyPoly roly, Rigidbody rb, DriveState state)
    {
        if (!debugLog) return;

        Transform look = target.CameraPoint;

        Debug.Log($"[{actionName}] {target.name}: RolyPoly en '{roly.name}', Rigidbody de masa {rb.mass:0.##}. " +
                  $"Al empezar: kinemático = {state.wasKinematic}, RolyPoly activo = {state.rolyWasEnabled}. " +
                  $"Camera First Person Point = '{look.name}'.", target);

        if (!look.IsChildOf(rb.transform))
            Debug.Log($"[{actionName}] {target.name}: el Camera First Person Point '{look.name}' no cuelga del cuerpo " +
                      $"físico '{rb.name}'. No pasa nada: la cámara de posesión sigue al cuerpo físico igualmente.", target);

        if ((rb.constraints & (RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ)) != 0)
            Debug.LogWarning($"[{actionName}] {target.name}: el Rigidbody '{rb.name}' tiene la posición X/Z congelada " +
                             $"({rb.constraints}), no podrá caminar.", target);

        if (target.GetComponent<CharacterController>() != null)
            Debug.LogWarning($"[{actionName}] {target.name}: tiene un CharacterController además de físicas. " +
                             "Quítaselo, estorba al movimiento.", target);
    }
}