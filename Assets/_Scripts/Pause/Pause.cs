using UnityEngine;
using UnityEngine.Events;

public class PauseHandler
{
    public static UnityEvent OnPause = new UnityEvent();
    public static UnityEvent OnResume = new UnityEvent();
    public static bool gameIsPaused = false;
    public static void Pause()
    {
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        gameIsPaused = true;
        OnPause?.Invoke();
    }
    public static void Resume()
    {
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        gameIsPaused = false;
        OnResume?.Invoke();
    }
    public static void TogglePaused()
    {
        if (gameIsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }
    public static void ClearEvents()
    {
        OnResume = new UnityEvent();
        OnPause = new UnityEvent();
    }
}
