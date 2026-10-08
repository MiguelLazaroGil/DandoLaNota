
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using FMODUnity;
using FMOD.Studio;

[System.Serializable]
public class InstrumentNotes
{
    // C, D, E, F, G, A, B, High C, High D, High E
    [FormerlySerializedAs("notas")]
    public EventReference[] notes = new EventReference[10];
}

public class InteractivePiano : MonoBehaviour
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

    private EventInstance[] activeNotes = new EventInstance[10];
    private Instrument previousInstrument;

    private readonly Key[] keys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3,
        Key.Digit4, Key.Digit5, Key.Digit6,
        Key.Digit7, Key.Digit8, Key.Digit9,
        Key.Digit0
    };

    private void Awake()
    {
        previousInstrument = currentInstrument;
    }

    private void Update()
    {
        // Detect instrument changes
        if (previousInstrument != currentInstrument)
        {
            ReleaseAllNotes();
            previousInstrument = currentInstrument;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int i = 0; i < keys.Length; i++)
        {
            if (keyboard[keys[i]].wasPressedThisFrame)
                PlayNote(i);

            if (keyboard[keys[i]].wasReleasedThisFrame)
                ReleaseNote(i);
        }
    }

    private void PlayNote(int index)
    {
        // Percussion instruments play their full sound
        if (IsPercussion())
        {
            EventReference sound = GetPercussionEvent();

            if (!sound.IsNull)
                RuntimeManager.PlayOneShot(sound);

            return;
        }

        EventReference[] notes = GetInstrumentNotes();

        if (notes == null || index >= notes.Length)
            return;

        if (notes[index].IsNull)
            return;

        // Harp notes play without early release
        if (currentInstrument == Instrument.Harp)
        {
            RuntimeManager.PlayOneShot(notes[index]);
            return;
        }

        EventInstance instance =
            RuntimeManager.CreateInstance(notes[index]);

        instance.setParameterByName("Release", 0f);
        instance.start();

        activeNotes[index] = instance;
    }

    private void ReleaseNote(int index)
    {
        EventInstance instance = activeNotes[index];

        if (!instance.isValid()) return;

        // Trigger the FMOD release transition
        instance.setParameterByName("Release", 1f);

        // Allow FMOD to finish playback naturally
        instance.release();

        activeNotes[index] = default;
    }

    private void ReleaseAllNotes()
    {
        for (int i = 0; i < activeNotes.Length; i++)
            ReleaseNote(i);
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

    public void ChangeInstrument(Instrument newInstrument)
    {
        ReleaseAllNotes();

        currentInstrument = newInstrument;
        previousInstrument = newInstrument;
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
