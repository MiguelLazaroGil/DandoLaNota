using UnityEngine;

public class MenuUtils : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Quit()
    {

        Debug.Log("Application is quitting");
        Application.Quit();
    }
    private void OnApplicationQuit()
    {
    }
}

