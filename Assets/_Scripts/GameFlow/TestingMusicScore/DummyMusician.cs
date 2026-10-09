using UnityEngine;

public class DummyMusician : MonoBehaviour, IMusician
{
    [Header("--- TEST MÚSICO ---")]
    [SerializeField, Range(0f, 1f)] private float individualQuality = 1.0f;

    public float IndividualQuality => individualQuality;

}