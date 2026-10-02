using UnityEngine;
using UnityEngine.Events;

public class EventPlayer : MonoBehaviour
{

    [SerializeField]
    private UnityEvent events;
    [SerializeField]
    bool playOnStart;
    [SerializeField]
    bool playOnEnable;
    private void Start()
    {
        if (playOnStart)
        {
            events?.Invoke();
        }
    }
    private void OnEnable()
    {
        if (playOnEnable)
        {
            events?.Invoke();
        }
    }
    public  void PlayEvents() {
        if (events != null)
        {
            Debug.Log("Eventos ejecutados: " + gameObject.name);
        }
        events?.Invoke();
    }


}
