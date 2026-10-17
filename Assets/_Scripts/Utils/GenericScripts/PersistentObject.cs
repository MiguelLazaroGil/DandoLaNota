using UnityEngine;

public class PersistentObject : MonoBehaviour
{
    private static PersistentObject instance;

    private void Awake()
    {
        // 1. Comprobamos si ya existe otra instancia de este objeto
        if (instance == null)
        {
            // Si es el primero, nos convertimos en la instancia oficial
            instance = this;
            
            // Hacemos que este GameObject no se destruya al cambiar de escena
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Si ya existía uno previo, destruimos este duplicado inmediatamente
            Destroy(gameObject);
        }
    }
}