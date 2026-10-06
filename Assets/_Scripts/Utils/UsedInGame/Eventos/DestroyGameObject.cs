using UnityEngine;

public class DestroyGameObject : MonoBehaviour
{
    public float delay = 0f; // Time in seconds before the GameObject is destroyed
    public bool destroyOnStart = false; // Whether to destroy the GameObject immediately on start
    private void Start()
    {
        if (destroyOnStart)
        {
            Destroy(gameObject, delay);
        }
    }
    public void DestroyObject()
    {
        Destroy(gameObject, delay);
    }
}

