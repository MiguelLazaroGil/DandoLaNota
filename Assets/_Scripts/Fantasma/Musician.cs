using System.Collections.Generic;
using UnityEngine;

public class Musician : MonoBehaviour
{
    public enum PlayingState { PlayingWell, PlayingBadly, Sabotaging }
    public PlayingState currentState = PlayingState.PlayingWell;

    [Header("Configuración de Posesión")]
    public Transform cameraFirstPersonPoint;
    public Transform sheetMusicTransform;
    public AudioSource musicianAudio;
    public List<SabotageAction> availableActions = new List<SabotageAction>();

    public GhostEntity PossessingGhost { get; set; }

    //todo: asignar bien available
    public bool IsAvailable = true;
    public bool IsPossessed => PossessingGhost != null;

    public void SetPlayingState(PlayingState newState) => currentState = newState;

    public void PlaySound(AudioClip clip)
    {
        if (musicianAudio && clip) musicianAudio.PlayOneShot(clip);
    }

    public void SwapToFakeInstrument(GameObject fakePrefab) { /* Lógica visual */ }
    public void ShowPhoneUI(bool show) { /* Lógica visual */ }

    // Llamar a este método cuando la batuta del Director golpea a este músico
    public void OnHitByConductor()
    {
        if (IsPossessed)
        {
            PossessingGhost.GetHitByConductor();
        }
        else
        {
            Debug.Log("¡El Director golpeó a un músico inocente! Penalización para el director.");
        }
    }
}