using UnityEngine;

public class SimpleMusicoAction : MusicoAccion
{
    public string actionExecuted = "SimpleMusicoAction executed";
    public float utilityValue = 50f;
    public bool isValid = true;
    public override void Execute(MusicoController owner)
    {
       
        Debug.Log(actionExecuted);
    }

    public override float GetRawUtility(MusicoController owner)
    {
        return utilityValue;
    }

    public override bool IsValid(MusicoController owner)
    {
        return isValid;
    }

}
