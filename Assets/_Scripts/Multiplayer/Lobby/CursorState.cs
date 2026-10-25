using UnityEngine;

public class CursorState : MonoBehaviour
{
    [SerializeField] private CursorLockMode mode = CursorLockMode.None;
    [SerializeField] private bool visible = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = mode;
        Cursor.visible = visible;
    }

    // Update is called once per frame
    void Update()
    {

        Cursor.lockState = mode;
        Cursor.visible = visible;
    }
}
