using UnityEngine;

namespace MLG.DelayedActionsTool.Samples
{
    public class DAGivingOrdersSampleCode : MonoBehaviour
    {
        bool endedNormally = true;
        [SerializeField]
        DARecieveOrdersSampleCode[] orderRecievers;
        [SerializeField]
        DARecieveOrdersSampleCode singleReciever;
        public void Func()
        {
            endedNormally = false;
            Debug.Log("----------------------------Delayed Actions giving orders code----------------------------");
            Debug.Log("This lesson is shown in-game with some rigidbodies moving.");
            Debug.Log("Objects will now move with independet orders (a DA for each object)");
            EveryBodyRight(1f);
            EveryBodyLeft(8.5f);
            EveryBodyStop(16f);



            DelayedActions.Sequence(this)
                .Wait(17.0f)
                .Do(
                () =>
                {
                    Debug.Log("Objects will now move with a shared order (one DA given by this script)");

                })
                .Do(EveryBodyRight)
                .Wait(5f)
                .Do(EveryBodyLeft)
                .Wait(5f)
                .Do(EveryBodyStop)
                .Wait(1f)
                .Do(() =>
                {
                    Debug.Log("----------------------------Giving Orders code end (you may clear the console)----------------------------");
                    endedNormally = true;
                }, "Sequence end")
                .Start();

        }

        void EveryBodyRight(float delay)
        {
            for (int i = 0; i < orderRecievers.Length; i++)
            {
                var receiver = orderRecievers[i];
                DelayedActions.Do(receiver, delay, receiver.MoveRight, "Moving right");
            }

        }
        void EveryBodyLeft(float delay)
        {

            for (int i = 0; i < orderRecievers.Length; i++)
            {
                var receiver = orderRecievers[i];
                DelayedActions.Do(receiver, delay, receiver.MoveLeft, "Moving left");
            }

        }
        void EveryBodyStop(float delay)
        {
            for (int i = 0; i < orderRecievers.Length; i++)
            {
                var receiver = orderRecievers[i];
                DelayedActions.Do(receiver, delay, receiver.Stop, "Stopping");
            }

        }
        void EveryBodyRight()
        {
            for (int i = 0; i < orderRecievers.Length; i++)
            {
                var receiver = orderRecievers[i];
                receiver.MoveRight();
            }

        }
        void EveryBodyLeft()
        {
            for (int i = 0; i < orderRecievers.Length; i++)
            {
                var receiver = orderRecievers[i];
                receiver.MoveLeft();
            }

        }
        void EveryBodyStop()
        {
            for (int i = 0; i < orderRecievers.Length; i++)
            {
                var receiver = orderRecievers[i];
                receiver.Stop();
            }

        }
        private void OnDisable()
        {
            if (gameObject.scene.isLoaded && !endedNormally)
            {
                Debug.Log("----------------------------Delayed Actions giving orders code end (you may clear the console)----------------------------");
            }
            DelayedActions.Abort(this);
            foreach (var reciever in orderRecievers)
            {
                DelayedActions.Abort(reciever);
            }
        }

    }
}