using UnityEngine;
using UnityEngine.UI;

namespace MLG.DelayedActionsTool.Samples
{
    public class DAQueringSample : MonoBehaviour
    {
        long idOfTheAction;
        public Image image;
        DelayedActionInfo action;
        public void Func()
        {
            idOfTheAction= DelayedActions.Do(this, 30, DoNothing, "Doing nothing");
            action = DelayedActions.GetAction(idOfTheAction);

        }
        private void Update()
        {
            if (action!=null)
            {
               
                double timeLeft = action.delay;

                if(timeLeft < 10 && timeLeft >9.5 )
                {
                    action.actionName = "Doing nothing but with a new name";
                }
                Color color = Color.Lerp(Color.red, Color.green, (float)(timeLeft / 30));
                image.color = color;
            }
        }
        void DoNothing()
        {
            Debug.Log("Doing nothing.");
        }
        public void PrintTimeAndWhereItIsUp()
        {
            double timeLeft=DelayedActions.GetTimeLeft(idOfTheAction);
            bool isScheduled = DelayedActions.IsScheduled(idOfTheAction);

            Debug.Log($"Time left for the action to be executed: {timeLeft} seconds. Is it scheduled? {isScheduled}");
        }

        private void OnDisable()
        {
 
            DelayedActions.Abort(this); //not mandatory. Avoid unwanted actions to be executed after the object is disabled 

        }
    }
}