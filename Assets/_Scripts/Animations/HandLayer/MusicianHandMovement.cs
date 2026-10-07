using System;
using System.Collections.Generic;
using UnityEngine;

public class MusicianHandMovement : MonoBehaviour
{
    public enum HandAnimations { Rest, Walk, Clap,ThumbsUp, SayHello, PlayViolin }
    public Animator HandAnimator;
    public List<AnimationClip> handAnimationClips;
    private AnimationClip currentClip;
    public void Start()
    {

    }
    public void PlayHandAnimation(HandAnimations handAnimation)
    {
        StopClip();
        currentClip = handAnimationClips[(int)handAnimation];
        HandAnimator.Play(currentClip.name);
        HandAnimator.speed = 1f;
    }
    internal void Clap()
    {
        PlayHandAnimation(HandAnimations.Clap);

    }
    public void StopClip()
    {
        if (currentClip == null) return;
        HandAnimator.speed = 0f;


    }
}
