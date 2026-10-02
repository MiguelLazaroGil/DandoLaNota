using UnityEngine;

namespace MLG.DelayedActionsTool.Samples
{

    internal class DACommonUseSampleCode : DACodeScript
    {

        bool endedNormally = true;

        public void DelayedForLoop()
        {
            DelayedActions.Abort(this); //I dont want multiple loops going at once
            endedNormally = false;

            Debug.Log("----------------------------Common issues sample code----------------------------");
            Debug.Log("4 actions will be scheduled 5 second from eachother and from now. Modify printedInt to see it changed.");
            int num = intA;
            var currentTime = Time.realtimeSinceStartup;




            for (int i = 0; i < 5; i++)
            {
                int temp = i;
                DelayedActions.Do(this, (i + 1) * 5, () =>
                {
                    Debug.Log("----");
                    Debug.Log("The value of i inside my Iteration is " + temp + " . But the value of i right now is:" + i);
                    Debug.Log("The value of 'printedInt' when I was scheduled was " + num + " . But the value of 'printedInt' is:" + intA);
                    Debug.Log("The time I was scheduled was: " + currentTime + " . But the current time is:" + Time.realtimeSinceStartup);
                    Debug.Log("----");


                }, "Delayed loop");
            }

            DelayedActions.Do(this, 26.0, () =>
            {
                Debug.Log("----------------------------Common issues sample code end (you may clear the console)----------------------------");
                endedNormally = true;
            }, "Delayed loop end");
        }
        private void OnDisable()
        {
            if (gameObject.scene.isLoaded && !endedNormally)
            {
                Debug.Log("The loop has been stopped because the this script (the executor) has been disabled.");
                Debug.Log("----------------------------Common issues sample code end (you may clear the console)----------------------------");
            }
            DelayedActions.Abort(this); //not mandatory. Avoid unwanted actions to be executed after the object is disabled 

        }
    }

}