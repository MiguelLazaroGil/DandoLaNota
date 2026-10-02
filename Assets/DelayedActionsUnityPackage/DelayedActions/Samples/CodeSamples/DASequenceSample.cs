using UnityEngine;
namespace MLG.DelayedActionsTool.Samples
{
    public class DASequenceSample : MonoBehaviour
    {
        public int numberRepetitionsSimpleLoop = 3;
        public void SimpleSequence()
        {
            DelayedActions.Sequence(this).LoopInfinite(33f, 10);
            DelayedActions.Sequence(this)
                .Wait(3f)
                .Do(PrintA, "Printing A")
                .Wait(2f)
                .Do(PrintB, "Printing B")
                .Wait(5f)
                .Do(PrintB, "Printing A and B").Do(PrintA, "Printing A and B") //These actions will be executed in the same frame, order is not expected or guaranteed.
                .Start();
        }
        public void ComplexLoop()
        {
            int id = 25;
            DelayedActions.Sequence(this) // -A-A-A--BB-AA-BB-AA
                .Wait(1f)
                .Do(PrintA, "Print INITIAL A loop")
                .Loop(3)
                .Wait(1f)
                .Section()
                    .Section()
                    .Wait(0.25f)
                    .Do(PrintB, "Print B internal loop")
                    .Do(PrintB, "Print B2 internal loop")
                    .Loop(2)
                    .Section()
                    .Wait(0.25f)
                    .Do(PrintA, "Print A internal loop")
                    .Loop(2)
                .Loop(3)
                .Start(id);
        }
        public void SimpleLoop()
        {
            DelayedActions.Sequence(this)
                .Do(PrintA, "Printing A")
                .Wait(2f)
                .Do(PrintB, "Printing B")
                .Loop(numberRepetitionsSimpleLoop, 2f).Start();
        }
        public void SimplestInfiniteLoop()
        {
            DelayedActions.Sequence(this)
                .Do(PrintA, "Printing A infinitely")
                .LoopInfinite(1f);
        }
        public void InfiniteLoop()
        {
            DelayedActions.Sequence(this)
                .Do(PrintA, "Printing A twice").Do(PrintA, "Printing A twice")
                .Wait(1f)
                .Section()
                    .Do(PrintA, "Printing A infinitely")
                    .Wait(2f)
                    .Do(PrintB, "Printing B infinitely")
                    .Wait(2f)
                .LoopInfinite();
        }

        void PrintA()
        {
            Debug.Log("A");

        }
        void PrintB()
        {
            Debug.Log("B");
        }
        private void OnDisable()
        {
            DelayedActions.Abort(this); //not mandatory but good for performance and to avoid unwanted actions to be executed after the object is disabled
        }
    }

}