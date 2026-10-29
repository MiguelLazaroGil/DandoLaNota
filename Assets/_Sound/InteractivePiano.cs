using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Netcode;
using FMODUnity;
using FMOD.Studio;

[System.Serializable]
public class InstrumentNotes
{
    // C, D, E, F, G, A, B, High C, High D, High E
    [FormerlySerializedAs("notas")]
    public EventReference[] notes = new EventReference[10];
}

public class InteractivePiano : NetworkBehaviour
{
    public enum Instrument
    {
        Trumpet,
        Flute,
        Clarinet,
        Violin,
        Harp,
        KickDrum,
        Cymbals,
        Triangle
    }

    [Header("3D Audio")]
    [SerializeField] private Transform pianoTransform;

    [Header("Active Instrument")]
    [FormerlySerializedAs("instrumentoActual")]
    public Instrument currentInstrument = Instrument.Trumpet;

    [Header("FMOD Instruments")]
    [FormerlySerializedAs("trompeta")]
    public InstrumentNotes trumpet = new();

    [FormerlySerializedAs("flauta")]
    public InstrumentNotes flute = new();

    [FormerlySerializedAs("clarinete")]
    public InstrumentNotes clarinet = new();

    [FormerlySerializedAs("violin")]
    public InstrumentNotes violin = new();

    [FormerlySerializedAs("arpa")]
    public InstrumentNotes harp = new();

    [Header("Percussion")]
    [FormerlySerializedAs("bombo")]
    public EventReference kickDrum;

    [FormerlySerializedAs("platillos")]
    public EventReference cymbals;

    [FormerlySerializedAs("triangulo")]
    public EventReference triangle;

    // --- SINCRONIZACIÓN DE RED ---
    private readonly NetworkVariable<Instrument> netCurrentInstrument = new NetworkVariable<Instrument>(
        Instrument.Trumpet, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private EventInstance[] activeNotes = new EventInstance[10];
    private Instrument previousInstrument;

    private readonly Key[] keys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3,
        Key.Digit4, Key.Digit5, Key.Digit6,
        Key.Digit7, Key.Digit8, Key.Digit9,
        Key.Digit0
    };

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    private void Awake()
    {
        previousInstrument = currentInstrument;
        if (pianoTransform == null) pianoTransform = transform;
    }

    public override void OnNetworkSpawn()
    {
        // Suscribirnos a cambios de instrumento en red
        netCurrentInstrument.OnValueChanged += OnNetworkInstrumentChanged;
        currentInstrument = netCurrentInstrument.Value;
        previousInstrument = currentInstrument;
    }

    public override void OnNetworkDespawn()
    {
        netCurrentInstrument.OnValueChanged -= OnNetworkInstrumentChanged;
    }

    private void OnNetworkInstrumentChanged(Instrument previousValue, Instrument newValue)
    {
        ReleaseAllNotes();
        currentInstrument = newValue;
        previousInstrument = newValue;
    }

    private void Update()
    {
        // Detectar cambios de instrumento locales
        if (previousInstrument != currentInstrument)
        {
            ReleaseAllNotes();
            previousInstrument = currentInstrument;
        }

        // EN MULTIJUGADOR: Solo el jugador que posee a este músico/instrumento lee el teclado
        if (IsNetworkActive && !IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int i = 0; i < keys.Length; i++)
        {
            if (keyboard[keys[i]].wasPressedThisFrame)
                RequestPlayNote(i);

            if (keyboard[keys[i]].wasReleasedThisFrame)
                RequestReleaseNote(i);
        }
    }

    // ========================================================================
    // SOLICITUDES DE ACCIÓN (Soporta Offline y Online)
    // ========================================================================

    private void RequestPlayNote(int index)
    {
        if (IsNetworkActive)
        {
            PlayNoteServerRpc(index);
        }
        else
        {
            PlayNoteLocal(index);
        }
    }

    private void RequestReleaseNote(int index)
    {
        if (IsNetworkActive)
        {
            ReleaseNoteServerRpc(index);
        }
        else
        {
            ReleaseNoteLocal(index);
        }
    }

    public void ChangeInstrument(Instrument newInstrument)
    {
        if (IsNetworkActive)
        {
            ChangeInstrumentServerRpc(newInstrument);
        }
        else
        {
            ReleaseAllNotes();
            currentInstrument = newInstrument;
            previousInstrument = newInstrument;
        }
    }

    // ========================================================================
    // RPCs DE RED
    // ========================================================================

    [Rpc(SendTo.Server)]
    private void PlayNoteServerRpc(int index)
    {
        PlayNoteClientRpc(index);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayNoteClientRpc(int index)
    {
        PlayNoteLocal(index);
    }

    [Rpc(SendTo.Server)]
    private void ReleaseNoteServerRpc(int index)
    {
        ReleaseNoteClientRpc(index);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReleaseNoteClientRpc(int index)
    {
        ReleaseNoteLocal(index);
    }

    [Rpc(SendTo.Server)]
    private void ChangeInstrumentServerRpc(Instrument newInstrument)
    {
        netCurrentInstrument.Value = newInstrument;
    }

    // ========================================================================
    // LÓGICA LOCAL DE FMOD (Se ejecuta en cada máquina al recibir el evento)
    // ========================================================================

    private void PlayNoteLocal(int index)
    {
        Vector3 soundPosition = pianoTransform != null ? pianoTransform.position : transform.position;

        // Percusión
        if (IsPercussion())
        {
            EventReference sound = GetPercussionEvent();

            if (!sound.IsNull)
                RuntimeManager.PlayOneShot(sound, soundPosition);

            return;
        }

        EventReference[] notes = GetInstrumentNotes();

        if (notes == null || index >= notes.Length || notes[index].IsNull)
            return;

        // Arpa (sonido sin release sostenido)
        if (currentInstrument == Instrument.Harp)
        {
            RuntimeManager.PlayOneShot(notes[index], soundPosition);
            return;
        }

        // Notas sostenidas
        EventInstance instance = RuntimeManager.CreateInstance(notes[index]);

        if (pianoTransform != null)
        {
            RuntimeManager.AttachInstanceToGameObject(instance, pianoTransform.gameObject);
        }

        instance.setParameterByName("Release", 0f);
        instance.start();

        activeNotes[index] = instance;
    }

    private void ReleaseNoteLocal(int index)
    {
        if (index < 0 || index >= activeNotes.Length) return;

        EventInstance instance = activeNotes[index];

        if (!instance.isValid()) return;

        instance.setParameterByName("Release", 1f);
        instance.release();

        activeNotes[index] = default;
    }

    private void ReleaseAllNotes()
    {
        for (int i = 0; i < activeNotes.Length; i++)
            ReleaseNoteLocal(i);
    }

    private EventReference[] GetInstrumentNotes()
    {
        switch (currentInstrument)
        {
            case Instrument.Trumpet: return trumpet.notes;
            case Instrument.Flute: return flute.notes;
            case Instrument.Clarinet: return clarinet.notes;
            case Instrument.Violin: return violin.notes;
            case Instrument.Harp: return harp.notes;
            default: return null;
        }
    }

    private bool IsPercussion()
    {
        return currentInstrument == Instrument.KickDrum ||
               currentInstrument == Instrument.Cymbals ||
               currentInstrument == Instrument.Triangle;
    }

    private EventReference GetPercussionEvent()
    {
        switch (currentInstrument)
        {
            case Instrument.KickDrum: return kickDrum;
            case Instrument.Cymbals: return cymbals;
            case Instrument.Triangle: return triangle;
            default: return default;
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < activeNotes.Length; i++)
        {
            if (!activeNotes[i].isValid()) continue;

            activeNotes[i].stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            activeNotes[i].release();
            activeNotes[i] = default;
        }
    }
}
