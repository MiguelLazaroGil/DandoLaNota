using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class HandGrabber : MonoBehaviour
{
    [SerializeField]
    TwoBoneIKConstraint leftHandGrabber;
    [SerializeField]
    TwoBoneIKConstraint rightHandGrabber;

    public void onGrab(Transform grabbable)
    {
        
    }
    public void onGrab(Transform leftHand, Transform rightHand)
    {
        Drop();
        var tempDataL = leftHandGrabber.data;
        tempDataL.target = leftHand;
        leftHandGrabber.data = tempDataL;
        var tempDataR = rightHandGrabber.data;
        tempDataR.target = rightHand;
        rightHandGrabber.data = tempDataR;
    }
    [ContextMenu("Drop")]
    public void Drop()
    {
        if (leftHandGrabber.data.target)
        {
            var tempDataL = leftHandGrabber.data;
            tempDataL.target = null;
            leftHandGrabber.data = tempDataL;
        }
        if (rightHandGrabber.data.target)
        {
            var tempDataR = rightHandGrabber.data;
            tempDataR.target = null;
            rightHandGrabber.data = tempDataR;
        }
    }
}
