using UnityEngine;

/// <summary>
/// Orquestador del fantasma: decide qué hacer cada frame (modo libre / modo poseído) y reacciona a los eventos
/// de GhostEntity. El trabajo concreto lo hacen las piezas que van en este mismo objeto:
/// GhostInputHandler, GhostTargeting, PossessionCamera, PossessionVolumeEffect, GhostActionWheel y GhostPresence.
/// </summary>
[RequireComponent(typeof(GhostEntity))]
[RequireComponent(typeof(GhostInputHandler), typeof(GhostTargeting), typeof(PossessionCamera))]
[RequireComponent(typeof(PossessionVolumeEffect), typeof(GhostActionWheel), typeof(GhostPresence))]
public class GhostController1 : MonoBehaviour, IGhostSystemPart
{
    private GhostEntity ghost;
    private GhostInputHandler input;
    private GhostTargeting targeting;
    private PossessionCamera possessionCamera;
    private PossessionVolumeEffect possessionEffect;
    private GhostActionWheel wheel;
    private GhostPresence presence;

    // Se mantiene público por compatibilidad con otros scripts que lo lean
    public Musician TargetedMusician => targeting != null ? targeting.TargetedMusician : null;

    private void Awake()
    {
        ghost = GetComponent<GhostEntity>();
        input = GetComponent<GhostInputHandler>();
        targeting = GetComponent<GhostTargeting>();
        possessionCamera = GetComponent<PossessionCamera>();
        possessionEffect = GetComponent<PossessionVolumeEffect>();
        wheel = GetComponent<GhostActionWheel>();
        presence = GetComponent<GhostPresence>();

        // Cámara de posesión: se prepara aquí (y no en su propio Awake) para controlar el orden y poder desactivarnos si falla
        if (!possessionCamera.Initialize())
        {
            enabled = false;
            return;
        }

        // Detector de interacción: el asignado o, si no, el que cuelgue de este objeto
        targeting.Initialize();
    }

    private void OnEnable()
    {
        ghost.MusicianPossessed += HandlePossessed;
        ghost.MusicianLeft += HandleLeft;
    }

    private void OnDisable()
    {
        ghost.MusicianPossessed -= HandlePossessed;
        ghost.MusicianLeft -= HandleLeft;
    }

    private void Update()
    {
        if (ghost.CurrentMusician == null) UpdateFreeMode();
        else UpdatePossessedMode();
    }

    // ───────────────────────── Modo libre (sin poseer) ─────────────────────────

    private void UpdateFreeMode()
    {
        targeting.UpdateTarget();

        if (targeting.TargetedMusician != null && input.PossessExitPressed)
        {
            if (!ghost.TryPossess(targeting.TargetedMusician))
                Debug.Log("No se pudo poseer: ¿poca energía, aturdido o músico ocupado?");
        }
    }

    // ───────────────────────── Modo poseído ─────────────────────────

    private void UpdatePossessedMode()
    {
        // Abrir la rueda (no se puede mientras estás saliendo)
        if (!ghost.IsExiting && !wheel.IsOpen && input.WheelPressed)
            wheel.Open();

        // Si la acción "Mover músico" está activa, WASD mueve el cuerpo
        UpdateMovement();

        // Con la rueda abierta el ratón sirve para apuntar: no se mira ni se sale
        if (wheel.IsOpen)
        {
            wheel.Tick(input.Look, input.WheelHeld);
            return;
        }

        // E mientras posees = intentar salir (GhostEntity se encarga del tiempo de casteo)
        if (input.PossessExitPressed)
        {
            ghost.RequestVoluntaryExit();
            return;
        }

        possessionCamera.UpdateLook(input.Look, ghost.CurrentMusician.Movement);
    }

    private void UpdateMovement()
    {
        MusicianMovement movement = ghost.CurrentMusician.Movement;
        if (movement == null || !movement.IsControlled) return;

        movement.SetMoveInput(input.Move);
    }

    // ───────────────────────── Eventos de GhostEntity ─────────────────────────

    private void HandlePossessed(Musician musician)
    {
        //Desactivar componentes propios(camara,player input en componentactivator.deactivate...)
        //Activar componentes de musician(en componentactivator.activate... encontrar en Musician.GetcomponentInChildren<ComponentActivator>() )

        // Cámara del músico en sus ojos y la del fantasma apagada
        possessionCamera.BeginPossession(musician);
        possessionEffect.Show(ghost.IsLocallyControlled);

        // La rueda muestra las acciones de este músico
        wheel.LoadActions(musician);

        targeting.ClearTarget();
        targeting.SetInteractionActive(false);
        presence.SetPresent(false);
    }

    // Se llama tanto al salir voluntariamente como cuando el director te golpea
    private void HandleLeft(Musician musician)
    {
        wheel.Close();

        // El fantasma reaparece donde estaba el músico (puede haberse movido), para que la cámara no pegue un salto
        presence.MoveTo(musician.transform.position);

        // Devolvemos la vista a la cámara del fantasma y guardamos la del músico en el fantasma
        possessionCamera.EndPossession();
        possessionEffect.Show(false);

        targeting.SetInteractionActive(true);
        presence.SetPresent(true);
    }
}