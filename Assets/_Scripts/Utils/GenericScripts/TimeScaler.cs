using UnityEngine;

public class TimeScaler : MonoBehaviour
{
    [SerializeField]
    [Range(0, 1)]
    float InitialTimeScale = 1f;
    public void Awake()
    {
        Time.timeScale = InitialTimeScale;
    }
}
