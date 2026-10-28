using System.Collections.Generic;
using UnityEngine;

public class Musician : MonoBehaviour
{
    public enum PlayingState { PlayingWell, PlayingBadly, Sabotaging }
    public PlayingState currentState = PlayingState.PlayingWell;
    public MusicianQuality quality;

    [Header("Configuración de Posesión")]
    public Transform cameraFirstPersonPoint;
    public Transform sheetMusicTransform;
    public AudioSource musicianAudio;
    public List<SabotageAction> availableActions = new List<SabotageAction>();

    public GhostEntity PossessingGhost { get; set; }

    //todo: asignar bien available
    public bool IsAvailable = true;
    public bool IsPossessed => PossessingGhost != null;

    // Punto donde se coloca la cámara al poseer (si no has asignado uno, usa el propio transform)
    public Transform CameraPoint => cameraFirstPersonPoint != null ? cameraFirstPersonPoint : transform;

    // Componente opcional que permite moverlo (null si el músico no lo tiene)
    public MusicianMovement Movement { get; private set; }

    private void Awake()
    {
        Movement = GetComponent<MusicianMovement>();
    }

    // Devuelve la acción de sabotaje en esa posición, o null si no existe
    public SabotageAction GetAction(int index)
    {
        if (index < 0 || index >= availableActions.Count) return null;
        return availableActions[index];
    }

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