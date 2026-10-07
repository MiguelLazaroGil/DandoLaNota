using UnityEngine;

public interface ITargeter 
{
    public GameObject GetTarget();
    public GameObject[] GetAllTargets();
    public bool HasTarget();
}
