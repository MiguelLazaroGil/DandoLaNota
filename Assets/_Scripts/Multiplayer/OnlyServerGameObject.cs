using Unity.Netcode;
using UnityEngine;

public class OnlyServerGameObject : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(!IsServer && !IsHost)
        {
            this.gameObject.SetActive(false);
        }
    }
    private void OnEnable()
    {
        if (!IsServer && !IsHost)
        {
            this.gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
