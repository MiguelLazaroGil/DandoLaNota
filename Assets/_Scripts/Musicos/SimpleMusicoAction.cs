using UnityEngine;

public class SimpleMusicoAction : MusicoAccion
{
    public string actionExecuted = "SimpleMusicoAction executed";
    public float utilityValue = 50f;
    public bool isValid = true;
    public override void Execute(Musician owner)
    {
       
        Debug.Log(actionExecuted);
    }

    public override float GetRawUtility(Musician owner)
    {
        return utilityValue;
    }

    public override bool IsValid(Musician owner)
    {
        return isValid;
    }

}
