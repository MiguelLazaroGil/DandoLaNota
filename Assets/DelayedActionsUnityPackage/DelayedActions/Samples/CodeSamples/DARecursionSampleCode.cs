using UnityEngine;
namespace MLG.DelayedActionsTool.Samples
{
    internal class DARecursionSampleCode : DACodeScript
    {
        bool recurringActionStarted = false;
        public void recurringActionStart()
        {
            recurringActionStarted = true;
            Debug.Log("----------------------------Delayed Actions recursion code----------------------------");
            DelayedActions.Do(this, floatA, recurringAction, "Recurring action first iteration sent");
        }
        public void recurringAction()
        {
            Debug.Log("This is a recurring action that will execute every " + floatB + " seconds.");

            //Check for stopping behaviour etc.

            DelayedActions.Do(this, floatB, recurringAction, "Recurring action"); //NOTE: this would also work if calling the function inside an anonymous function.
        }
        private void OnDisable() //If you disable the component/gameobject, then the recurring action will stop.
        {
            if (gameObject.scene.isLoaded && recurringActionStarted)
            {
                Debug.Log("The recurring loop has been stopped because the this script (the executor) has been disabled.");
                Debug.Log("----------------------------Recursion sample code end (you may clear the console)----------------------------");
            }
            DelayedActions.Abort(this);
        }

    }
}