using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MLG.DelayedActionsTool.Samples
{
    public class DAAbortsSampleCode : MonoBehaviour
    {
        private class A { }
        public void ScheduleActions()
        {
            Debug.Log("----------------------------Delayed Actions aborts code----------------------------");
            Debug.Log("For this lesson look at the DelayedActions GO in the hierarchy, " +
                "you will see some actions with diferent locator and executors, " +
                "now press the rest of the buttons to see for yourself how actions are aborted."
                );
            Debug.Log("You can press the Schedule Actions button again");
            DelayedActions.AbortGroup();
            DelayedActions<A>.AbortGroup();
            DelayedActions.Do(this, 5000f, DoNothing, 5);

            DelayedActions.Do(this, 100f, DoNothing, 5);

            DelayedActions.Do(this, 500f, DoNothing, 5);
            DelayedActions.Do(this, 500f, DoNothing, 4);

            DelayedActions<A>.Do(this, 500f, DoNothing);
            DelayedActions<A>.Do(this, 500f, DoNothing, 5);

            DATagsSample dATagsSample = gameObject.GetComponentInChildren<DATagsSample>();
            DelayedActions.Do(dATagsSample, 500f, dATagsSample.DoNothingB, 5);


        }
        public void AbortDelayed()
        {
            DelayedActions.AbortGroup();
        }
        public void AbortThisID(int actionId)
        {
            DelayedActions.Abort(this, actionId);
        }
        public void AbortActionId(int actionId)
        {
            DelayedActions.Abort(actionId);
        }
        public void AbortA()
        {
            DelayedActions<A>.AbortGroup();
        }
        public void AbortThis()
        {
            DelayedActions.Abort(this);
        }
        public void DoNothing()
        {

        }
        private void OnDisable()
        {

            DelayedActions.AbortGroup();
            DelayedActions<A>.AbortGroup();
        }
    }
}