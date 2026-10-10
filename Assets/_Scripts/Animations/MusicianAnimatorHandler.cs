using System.Collections;
using IK.ProceduralAnimations.Walking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.ProBuilder;

public class MusicianAnimatorHandler : MonoBehaviour
{

    [SerializeField]
    public float blinkTimeInterval = 3.5f;
    [SerializeField]
    public MusicianFaceExpresions.FaceExpresions currentEmotion = MusicianFaceExpresions.FaceExpresions.Iddle;
    [SerializeField]
    public MusicianFaceExpresions faceExpresions;
    [SerializeField]
    public MusicianHandMovement handMovement;
    [SerializeField]
    public ProceduralWalkerPlayer walker;
    private bool IsMoving = false;


    void OnEnable()
    {
        if (faceExpresions == null) return;
        StartCoroutine(Blinking());
        ApplyExpresion();

        if (handMovement == null) return;
        handMovement.PlayHandAnimation(MusicianHandMovement.HandAnimations.Rest);

        if (walker == null) return;
        Stand();

    }

    void OnDisable()
    {
        StopCoroutine(Blinking());
    }

    void Update()
    {
        if (IsMoving)
        {
            Walk();
        }
        else
        {
            Stand();
        }
    }
    private IEnumerator Blinking()
    {
        while (true)
        {
            ApplyExpresion();
            yield return new WaitForSeconds(blinkTimeInterval);
            Blink();
            yield return new WaitForSeconds(0.2f);
        }
    }
    public void ApplyExpresion()
    {
        faceExpresions.ApplyExpression(currentEmotion);
    }
    public void Blink()
    {
        faceExpresions.ApplyExpression(MusicianFaceExpresions.FaceExpresions.Blink);
    }
    public void onMove(InputAction.CallbackContext ctx)
    {
        Vector2 move = ctx.ReadValue<Vector2>();
        IsMoving = move.sqrMagnitude > 0.001f;

    }
    [ContextMenu("Walk")]
    public void Walk()
    {

        walker.IsMoving = true;
        walker.IsGrounded = true;
        handMovement.PlayHandAnimation(MusicianHandMovement.HandAnimations.Walk);
    }
    [ContextMenu("Stand")]
    public void Stand()
    {
        walker.IsMoving = false;
        handMovement.PlayHandAnimation(MusicianHandMovement.HandAnimations.Rest);
    }
    [ContextMenu("Clapp")]
    public void Clapp()
    {
        currentEmotion = MusicianFaceExpresions.FaceExpresions.Happy;
        handMovement.Clap();
    }
    [ContextMenu("ThumbsUp")]
    public void ThumbsUp()
    {
        currentEmotion = MusicianFaceExpresions.FaceExpresions.Happy;
        handMovement.PlayHandAnimation(MusicianHandMovement.HandAnimations.ThumbsUp);
    }
}
