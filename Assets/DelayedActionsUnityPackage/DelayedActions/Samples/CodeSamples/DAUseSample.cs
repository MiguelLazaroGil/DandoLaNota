using UnityEngine;
namespace MLG.DelayedActionsTool.Samples
{
    public class DAUseSample : MonoBehaviour
    {
        bool endedNormally = false;
        [SerializeField, Range(0, 5)]
        float printDelay = 3f;
        void Start()
        {
            Debug.Log("----------------------------Delayed Actions use code----------------------------");
            Debug.Log("Some actions will be scheduled to be executed in" + printDelay + " and " + (printDelay + 3) + " seconds.");


            DelayedActions.Do(this, printDelay, () =>
            {
                Debug.Log("This action was scheduled in the start");
                Func();
            },
            "Delayed Action In Start anonymous");

            DelayedActions.Do(this, printDelay + 3, PrintMessage, "Delayed Action In Start function");



            DelayedActions.Do(this, printDelay + 3.1415f, () =>
            {
                Debug.Log("----------------------------Delayed Actions use code end (you may clear the console)----------------------------");
                endedNormally = true;
            }, "Delayed Action In Start Aborted");
        }
        private void PrintMessage()
        {
            Debug.Log("This action was scheduled in the start, and it is a function on its own.");

        }
        void Func()
        {
            Debug.Log("You can call other functions inside an anonymous function if you want, like this one.");
        }

        private void OnDisable()
        {
            if (gameObject.scene.isLoaded && !endedNormally)
            {
                Debug.Log("----------------------------Delayed Actions use code end (you may clear the console)----------------------------");
            }
            DelayedActions.Abort(this); //not mandatory. Avoid unwanted actions to be executed after the object is disabled 
        }
    }

}