using UnityEngine;

public class HandPoseStorer : MonoBehaviour
{
    [SerializeField]
    Transform leftHandTransform;
    public Transform LeftHand => leftHandTransform;
    [SerializeField]
    Transform righHandTransform;
    public Transform RighHand => righHandTransform;

}
