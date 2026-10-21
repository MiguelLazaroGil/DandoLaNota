using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class HandGrabber : MonoBehaviour
{
    [SerializeField]
    TwoBoneIKConstraint leftHandGrabber;
    [SerializeField]
    TwoBoneIKConstraint rightHandGrabber;

    public void onGrab(Grabbable grabbable)
    {
        var handPoser = grabbable.GetComponent<HandPoseStorer>();
        if (handPoser)
        {

            onGrab(handPoser.LeftHand, handPoser.RighHand);
        }
    }
    public void onGrab(Transform leftHand, Transform rightHand)
    {

        var tempDataL = leftHandGrabber.data;
        tempDataL.target = leftHand;
        tempDataL.targetPositionWeight = 1f;
        tempDataL.targetRotationWeight = 1f;
        leftHandGrabber.data = tempDataL;
        leftHandGrabber.weight = 1f;


        var tempDataR = rightHandGrabber.data;
        tempDataR.target = rightHand;
        tempDataR.targetPositionWeight = 1f;
        tempDataR.targetRotationWeight = 1f;
        rightHandGrabber.data = tempDataR;
        rightHandGrabber.weight = 1f;
        //
        ResetConstraints();
    }
    public void ResetConstraints()
    {
        var rigBuilder = GetComponentInParent<UnityEngine.Animations.Rigging.RigBuilder>();
        if (rigBuilder != null)
        {
            rigBuilder.Build();
        }
        leftHandGrabber.enabled = false;
        leftHandGrabber.enabled = true;

        rightHandGrabber.enabled = false;
        rightHandGrabber.enabled = true;
    }
    [ContextMenu("Drop")]
    public void Drop()
    {
        if (leftHandGrabber.data.target)
        {
            var tempDataL = leftHandGrabber.data;
            tempDataL.target = null;
            tempDataL.targetPositionWeight = 1f;
            tempDataL.targetRotationWeight = 1f;
            leftHandGrabber.data = tempDataL;
            leftHandGrabber.weight = 1f;
        }
        if (rightHandGrabber.data.target)
        {
            var tempDataR = rightHandGrabber.data;
            tempDataR.targetPositionWeight = 1f;
            tempDataR.targetRotationWeight = 1f;
            tempDataR.target = null;
            rightHandGrabber.data = tempDataR;
            rightHandGrabber.weight = 1f;
        }
        ResetConstraints();
    }
}
