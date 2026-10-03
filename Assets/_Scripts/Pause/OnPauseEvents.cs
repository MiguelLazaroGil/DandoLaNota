using UnityEngine;
using UnityEngine.Events;
public class OnPauseEvents : MonoBehaviour
{
    public UnityEvent onPauseEvents;
    public UnityEvent onResumeEvents;

    public bool ResumeOnStart = true;
    public bool StopTimeOnPause = true;
    public bool ChangeCursorOnResume = true;
    void Start()
    {
        if (ResumeOnStart)
        {
            ResumeGame();
        }
    }
    public void ResumeGame()
    {
        PauseHandler.Resume();
    }
    public void PauseGame()
    {
        PauseHandler.Pause();
    }

    private void DoResumeEvents()
    {

            Time.timeScale = 1f;
        if (ChangeCursorOnResume)
        {

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        onResumeEvents?.Invoke();
    }
    private void DoPauseEvents()
    {
        if (StopTimeOnPause)
        {
            Time.timeScale = 0f;

        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        onPauseEvents?.Invoke();
    }
    private void OnEnable()
    {
        PauseHandler.OnPause.AddListener(DoPauseEvents);
        PauseHandler.OnResume.AddListener(DoResumeEvents);
    }
    private void OnDisable()
    {
        PauseHandler.OnPause.RemoveListener(DoPauseEvents);
        PauseHandler.OnResume.RemoveListener(DoResumeEvents);
    }

}
