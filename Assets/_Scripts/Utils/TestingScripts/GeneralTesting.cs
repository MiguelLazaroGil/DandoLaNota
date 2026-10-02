using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GeneralTesting : MonoBehaviour
{
    public UnityEvent onStart;
    public UnityEvent on0Pressed;
    public UnityEvent on1Pressed;
    public UnityEvent on2Pressed;
    public UnityEvent on3Pressed;
    public UnityEvent on4Pressed;
    public UnityEvent on5Pressed;
    public UnityEvent on6Pressed;

    private void Start()
    {
        onStart?.Invoke();
    }
    private void Update()
    {
        /*
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            on0Pressed?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            on1Pressed?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            on2Pressed?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            on3Pressed?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            on4Pressed?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            on5Pressed?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            on6Pressed?.Invoke();
        }
        */

    }
    public void Print(string text)
    {
        Debug.Log(text);
    }
    void OnDrawGizmosSelected()
    {
    
    }
}
