using UnityEngine;
namespace MLG.DelayedActionsTool.Samples
{
    public class DATagsSample : MonoBehaviour
    {
        bool endedNormally = true;
        class MyTag { }
        class DelayedSounds : DelayedActions<DelayedSounds> { }
        class Enemies { }
        class DelayedEnemies : DelayedActions<Enemies> { }
        public void Function()
        {
            endedNormally = false;

            Debug.Log("----------------------------Delayed Actions tags code----------------------------");
            Debug.Log("For this lesson look at the DelayedActions GO in the hierarchy, you will see that there are different groups for the different tags, and the actions scheduled with those tags will be listed under them.");
            DelayedActions.Do(this, 2f, DoNothing, "Default tag example");

            DelayedActions<Rigidbody>.Do(this, 20f, DoNothing, "Using rigidBody tag");
            DelayedActions<Rigidbody>.Do(this, 100f, DoNothing, "Using rigidBody tag again");

            DelayedActions<string>.Do(this, 25f, DoNothing, "Using string tag");
            DelayedActions<DATagsSample>.Do(this, 30f, DoNothing, "Using DATagsSample tag");
            DelayedActions<MyTag>.Do(this, 240f, DoNothing, "Using MyTag tag");

            DelayedSounds.Do(this, 100f, DoNothing, "Using DelayedSounds tag");
            DelayedEnemies.Do(this, 50f, DoNothing, "Using DelayedEnemies tag");
            DelayedActions.Do(this, 240f, () =>
            {
                Debug.Log("----------------------------Delayed Actions tags code end (you may clear the console)----------------------------");
                endedNormally = true;
            }, "Delayed Action In Start Aborted");
        }
        public void DoNothing()
        {
            Debug.Log("Doing nothing A.");
        }
        public void DoNothingB()
        {
            Debug.Log("Doing nothing B.");
        }
        void OnDisable()
        {
            if (gameObject.scene.isLoaded && !endedNormally)
            {
                Debug.Log("----------------------------Delayed Actions tags code end (you may clear the console)----------------------------");
            }


            //NOTE: this will also destory the gameobject, this is done so that the tutorial is more clear. To learn how to abort actions continue to the following step.
            DelayedActions.AbortGroup();
            DelayedActions<Rigidbody>.AbortGroup();
            DelayedActions<string>.AbortGroup();
            DelayedActions<DATagsSample>.AbortGroup();
            DelayedActions<MyTag>.AbortGroup();
            DelayedSounds.AbortGroup();
            DelayedEnemies.AbortGroup();



        }
    }

}