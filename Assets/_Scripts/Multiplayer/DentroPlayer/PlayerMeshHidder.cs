using System.Collections.Generic;
using UnityEngine;

public class PlayerMeshHidder : MonoBehaviour
{

    [SerializeField] private List<GameObject> hiddenMeshes = new();
    public void ShowMeshes()
    {
        foreach (var mesh in hiddenMeshes)
        {
            mesh.layer = 10;
        }
    }
    public void HideMeshes()
    {
        foreach (var mesh in hiddenMeshes)
        {
            mesh.layer = 9;
        }
    }
}
