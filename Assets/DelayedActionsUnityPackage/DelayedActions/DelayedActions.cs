#if UNITY_EDITOR

#define DebugModeActive
///commenting '#define DebugModeActive' will make DelayedActions behave as if in a build. All debugging info won't be generated (inspector view of the scheduled actions won't be shown).


#endif


/// //////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// Script created by Miguel Lázaro Gil.  
/// 
/// Delayed Actions (DA) is a single-script utility library that allows you to 'wait for seconds' without Coroutines, Invokes or Update timers.
/// DA is simple, you can *Do* and you can *Abort* function calls, not much else. 
/// DA uses C# delegates (only Actions) and anonymous functions (Examples of how to code this way are shown in the sample code scripts).
/// DA doesn't handle concurrency problems (Examples of different race conditions are shown in the sample code scripts).
/// 
/// Inner Workings: DA works with in-game time, during the Update method.
///                 It is affected by time scale.
///                 When DA is used, a GameObject of name "DelayedActions" will appear on the 'DontDestroyOnLoad' scene, it will have children.
///                 Inside those you will be able to see a list of every scheduled action. 
/// 
/// Delayed Actions has the following:
///     Doing Actions (not programmed exactly as shown):
///         **Schedule the execution on the given 'action' after 'delay' seconds, using 'executor' as the Monobehaviour that will execute the action. 
///         **Executor is mandatory. If a gameObject is destroyed, or the scene unloads, actions under said executor will be automatically aborted. 
///         **Locator is optional and entirely user-chosen (default: 0). It is NOT unique by design.
///         **Give the same locator to several actions if you want to be able to abort them together later (see Abort(executor, locator) / Abort(locator) below).
///         **ActionName is optional. Use it for debugging purposes (highly recommended).
///         **Every call to Do returns a globally unique, auto-generated 'id' (long), regardless of what locator (if any) you passed in.
///                  
///         -public long Do(MonoBehaviour executor, double delay, Action action);
///         -public long Do(MonoBehaviour executor, double delay, Action action, int locator);
///         -public long Do(MonoBehaviour executor, double delay, Action action, string actionName);
///         -public long Do(MonoBehaviour executor, double delay, Action action, int locator, string actionName);
///         
///     Aborting Actions:
///         **Aborting an action means it will not be executed. 
///         **Locators are not unique by design, so Abort(locator) / Abort(executor, locator) can cancel several actions at once.
///         **Aborting by Executor means aborting every action under said Executor.
///         **Aborting by Locator means aborting every action with said locator, regardless of their Executor. 
///         **Specifing the executor and locator aborts every action with said locator under said Executor.
///         **Aborting by id aborts exactly one action: the one whose unique id (returned by Do) matches.
///         **Aborting everything aborts the whole group of DelayedActions. See an explanation of groups later.
///                
///         -public void Abort(MonoBehaviour executor);
///         -public void Abort(MonoBehaviour executor, int locator);
///         -public void Abort(int locator); 
///         -public void AbortById(long id);
///         -public void AbortGroup(); 
///         
/// 
///     Quering actions:
///         **You may check whether an action is still pending.
///         **You may check how much time is left for an action to be executed.
///         **If you are going to repeatedly check the same action, it is better to just get the action.
///         
///         -public bool IsScheduled(long id);
///         -public double GetTimeLeft(long id); //Returns negative values if the action is not found.
///         -public DelayedActionInfo GetAction(long id); //Returns null if the action is not found. Lets you access time, cancelation, locator and name.
///     
///     Sequences:
///         **You may create a sequence of actions in an intuitive way using the sequence builder.
///         **Add waiting times, actions and loops/sections in any order any number of times.
///         **Specify the executor in the creation of the sequence, then each action may have its own name.
///         **Specify the locator (optional) when starting the sequence, every action will share the locator. 
///         **Starting a sequences returns the unique auto-generated ids of the scheduled actions (one per step).
///         
///         -public SequenceBuilder Sequence(MonoBehaviour executor);
///             SequenceBuilder:
///                 -public Wait(double seconds);
///                 -public Do(Action action, string actionName);
///                 -public Section();
///                 -public Loop(int totalTimesExecuted=2, double iterationDelay=0);
///                 -public List<long> Start(int locator = 0);
///                 -public List<long> LoopInfinite(double iterationDelay = 0, int locator = 0); //Also starts the sequence
///             *Use example*: DelayedActions.Sequence(this).Wait(2f).Do(SpawnBoss).Wait(3f).Section().Do(SpawnEnemy, "Spawn Enemy").Loop(5, 10f).Start();
///    

///     Groups:
///         **DelayedActions as 'DelayedAction.Do(MyFunc, myDelay, this);' will work as a single 'default' group.
///         **If you want to organize your actions in different groups you'll need to use classes as Tags. 
///         **Calling DA with '<MyClass>' will trigger the creation of a new group.
///         **Groups will have their own 'DelayedActions' child object and AbortGroup will work on said group.
///         **Locators are scoped per group (each Tag keeps its own bookkeeping), so the same locator value in two different
///           groups refers to unrelated actions. The unique 'id' returned by Do is drawn from one global counter, but
///           AbortById only searches the group it's called on - always call it via the same DelayedActions<Tag> (or shortcut) you used to schedule the action.
///         **Classes used as tags dont need to be 'real classes': public class MyTag{}; is enough. 
///         **You may create your own 'shortcuts' like class DelayedSounds: DelayedActions<DelayedSounds>{}///         **To use groups do as follows:
///         
///         -DelayedActions<MyTag>.Do(...);
///         -DelayedAction<EnemyAI>.Do(...);        
///         -DelayedActions<MyTag>.Abort(...);
///         -DelayedSounds.Abort(...);
///         
///     Debugging:
///         **In build mode there are no names, no lists and no logs. (More efficient)
///         **In debug mode you can see every scheduled action in the DelayedActions gameobject.
///         **You can disable debugmode, which will make it behave as if in a build by commenting '#define DebugModeActive' (line 3).
///       
///     Note: Disabled components/inactive gameobjects will PAUSE their timers automatically but not abort any action.
///          'Pausing' specific actions is not supported, you can work around it disabling components, turning off gameobjects or aborting and rescheduling actions. Remember this is a simple tool.
/// 
/// /////////////////////////////////////////////////////////////////////////////////////////////////////////////

#region imports

#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Threading;
#endregion

namespace MLG.DelayedActionsTool
{
    /// <summary>
    /// Static-like API class that allows for <b>scheduling</b> and aborting <b>Actions</b> with a delay, using Monobehaviours as executors. 
    /// </summary>
    public class DelayedActions : DelayedActions<DelayedActions> { } ///If you dont like '<' and '>' you can create DelayedActions classes with specific tags like so. This is the default
                                                                     /// <summary>
                                                                     /// Static class that allows for <b>scheduling</b> and aborting <b>Actions</b> with a delay, using Monobehaviours as executors. 
                                                                     /// Uses in-game time to trigger the timers. An obj called "DelayedActions"+ tag's Name will appear on DontDestroyOnLoad scene upon the first use.
                                                                     /// That obj is essential to the functioning of DelayedActions.
                                                                     /// </summary>
    public class DelayedActions<Tag>
    {
        //Name shown in the gameObj regarding this group
        static readonly string objName = "_" + typeof(Tag).Name + "_(DA)";

        //This doesnt seem to cause any problems with fast reloading because monobehaviours and gameobject point to null when destroyed (?)
        static internal DelayedActionsInScene delayedActionsInScene;
        #region Public Methods

        #region Do
        /// <summary>
        /// Delayed action will schedule the execution of the given action after delay seconds. 
        /// The delay might be off as much as one frame.
        /// </summary>
        /// <param name="executor">Monobehaviour that will execute the action</param>
        /// <param name="delay">Seconds of delay</param>
        /// <param name="action">C# action delegate or anonymous func to be executed</param>
        /// <param name="actionName">Optional name of the action to see in the inspector</param>
        /// <returns>The unique id of the action</returns>
        public static long Do(MonoBehaviour executor, double delay, Action action, string actionName)
        {
            return Do(executor, delay, action, 0, actionName);
        }

        /// <summary>
        /// Delayed action will schedule the execution of the given action after delay seconds. 
        /// The delay might be of as much as one frame.
        /// Set an locator to be able to abort the action later. 
        /// The same locator CAN be given to more than one action. 
        /// </summary>
        /// <param name="executor">Monobehaviour that will execute the action</param>
        /// <param name="delay">Seconds of delay</param>
        /// <param name="action">C# action delegate or anonymous func to be executed</param>
        /// <param name="locator">Optional nonUnique Locator for the action</param>
        /// <param name="actionName">Optional name of the action to see in the inspector</param>
        /// <returns>The unique id of the action</returns>
        public static long Do(MonoBehaviour executor, double delay, Action action, int locator = 0, string actionName = "Action")
        {
            if (executor == null)
            {
                Debug.LogWarning("DelayedActions: Executor can't be null. Action won't be scheduled. DelayedActions");
                return -1;
            }
            if (action == null)
            {
                Debug.LogWarning("DelayedActions: Action can't be null. Action won't be scheduled. DelayedActions");
                return -1;
            }
            DelayedActionInfo info = new DelayedActionInfo
            {
                action = action,
                delay = delay,
                locator = locator,
#if DebugModeActive
                actionName = actionName,
#endif

            };


            return Do(info, executor);
        }
        #endregion

        #region Abort

        /// <summary>
        /// Abort every single action scheduled to be executed by the given monobehaviour.
        /// </summary>
        /// <param name="executor">Which Monobehaviour will cancel all their actions</param>
        public static void Abort(MonoBehaviour executor)
        {
            if (delayedActionsInScene == null)
                return;
            delayedActionsInScene.Abort(executor);
        }
        /// <summary>
        /// Abort the action with the given locator scheduled to be executed by the given monobehaviour. 
        /// If more than one action has the same locator and said executor, <b>all</b> of them will be aborted.
        /// Use this function over Abort(locator) if the executor is known to improve performance.
        /// </summary>
        /// <param name="executor">Which monobehaviour to look into for a given locator</param>
        /// <param name="locator">Locator of the action/s to be aborted</param>
        public static void Abort(MonoBehaviour executor, int locator)
        {
            if (delayedActionsInScene == null)
                return;
            delayedActionsInScene.Abort(executor, locator);
        }
        /// <summary>
        /// Abort every action with the given locator, regardless of their executor. 
        /// Use this function when the executor is unknown or multiple executors have scheduled actions with the same locator.
        /// If the executor is known, use Abort(executor, locator) to imporve performance.
        /// </summary>
        /// <param name="locator">Locator of the action/s to be aborted</param>
        public static void Abort(int locator)
        {
            if (delayedActionsInScene == null)
                return;
            delayedActionsInScene.Abort(locator);
        }
        /// <summary>
        /// Abort the action with the given id. The action's id is given when it is scheduled. 
        /// Must be called on the same group as the action.
        /// </summary>
        /// <param name="id">Id of the action</param>
        public static void AbortById(long id)
        {
            if (delayedActionsInScene == null)
                return;

            delayedActionsInScene.AbortById(id);

        }

        /// <summary>
        /// Abort the whole group. This call also destroys the gameobject that handled the group.
        /// </summary>
        public static void AbortGroup()
        {
            if (delayedActionsInScene == null)
                return;
            delayedActionsInScene.AbortGroup();
        }
        #endregion

        #region Query
        /// <summary>
        /// Check how much time remains for the action to be executed.
        /// </summary>
        /// <param name="id">Id of the action</param>
        /// <returns>Time remaining in seconds. Negative if not found</returns>
        public static double GetTimeLeft(long id)
        {
            if (delayedActionsInScene == null)
                return -1;
            return delayedActionsInScene.GetTimeLeft(id);
        }
        /// <summary>
        /// Checks whether the action with the given id is still pending (not yet executed or aborted).
        /// Must be called on the same group that was used to schedule it.
        /// </summary>
        /// <param name="id">Id of the action</param>
        public static bool IsScheduled(long id)
        {
            if (delayedActionsInScene == null)
                return false;
            return delayedActionsInScene.IsScheduled(id);
        }
        /// <summary>
        /// Gets the obj representing the action with the given id. 
        /// DelayedActionInfo contains the action, the delay, the locator, the name and whether it was cancelled.
        /// Changes to this obj <b>will</b> affect the scheduled action. Use with care.
        /// </summary>
        /// <param name="id">Id of  the action</param>
        /// <returns>The obj of the action. Null if not found</returns>
        public static DelayedActionInfo GetAction(long id)
        {
            if (delayedActionsInScene == null)
                return null;
            return delayedActionsInScene.GetAction(id);
        }
        #endregion
        #region Sequence
        /// <summary>
        /// Begin the construction of a sequence of actions, all executed by the same monobehaviour. 
        /// Append calls to Wait(), Do(), Section() and Loop() to build the sequence. 
        /// Call Start() or LoopInfinite() to schedule the actions.
        /// </summary>
        /// <param name="executor">Executor of the sequence</param>
        public static SequenceBuilder Sequence(MonoBehaviour executor)
        {
            return new SequenceBuilder(executor);
        }
        /// <summary>
        /// Builder class that allows for the construction of a sequence of actions, all executed by the same monobehaviour.
        /// </summary>
        public class SequenceBuilder
        {
            private readonly MonoBehaviour executor;

            private readonly List<Step> steps = new();

            private double accumulatedDelay;
            // Stores indices of steps that belong to the current repeat block
            private struct SectionInfo
            {
                public int StartIndex;
                public double StartDelay;
            }

            private readonly Stack<SectionInfo> sectionStack = new();

            internal SequenceBuilder(MonoBehaviour executor)
            {
                this.executor = executor;
            }

            /// <summary>
            /// Adds time before the next action.
            /// </summary>
            /// <param name="seconds">Seconds to wait</param>
            public SequenceBuilder Wait(double seconds)
            {
                accumulatedDelay += seconds;
                return this;
            }

            /// <summary>
            /// Schedules an action at the current point in the sequence.
            /// </summary>
            public SequenceBuilder Do(Action action, string actionName = "Action")
            {
                steps.Add(new Step
                {
                    Delay = accumulatedDelay,
                    Action = action
#if DebugModeActive
                    ,
                    ActionName = actionName
#endif
                });

                return this;
            }
            /// <summary>
            /// Marks the start of a new section.
            /// Nested sections will be <b>consumed</b> by the closest Loop() calls, allowing for <b>nested loops</b>.
            /// </summary>
            public SequenceBuilder Section()
            {
                sectionStack.Push(new SectionInfo
                {
                    StartDelay = accumulatedDelay,
                    StartIndex = steps.Count
                });
                return this;
            }
            /// <summary>
            /// Every thing in the section will be repeated. Consumes the sections, nested loops are supported through the use of multiple sections.
            /// Loop(1) is redundant, it won't do anything extra. 
            /// Use Loop(2) to repeat at least once.
            /// Loop(x <= 0 ) will prevent the execution of the section, this can be used to conditionally <b>skip</b> a section of the sequence.
            /// Note: Loop() <b>unrolls</b> the loop into totalTimesExecuted * sectionLength steps.
            /// </summary>
            /// <param name="iterationDelay">Seconds of delay between the end of one iteration and the start of the next</param>
            /// <param name="totalTimesExecuted">Total times the section will be executed. Must be 2 or more to repeat at least once</param>
            public SequenceBuilder Loop(int totalTimesExecuted = 2, double iterationDelay = 0)
            {
                SectionInfo section = ConsumeSectionInfo();
                int sectionStartIndex = section.StartIndex;
                double sectionBaseDelay = section.StartDelay;

                if (totalTimesExecuted == 1)
                {
                    Debug.LogWarning($"Looping {totalTimesExecuted} totalTimesExecuted won't do anything. Use Loop(2) to repeat at least once. -DelayedActions");
                    return this;

                }
                if (totalTimesExecuted < 1)
                {
                    //Remove the steps of the section
                    for (int i = steps.Count - 1; i >= sectionStartIndex; i--)
                    {
                        steps.RemoveAt(i);
                    }

                    return this;

                }

                var block = steps.GetRange(sectionStartIndex, steps.Count - sectionStartIndex);
                double sectionDuration = accumulatedDelay - sectionBaseDelay;
                double baseOffset = sectionDuration + iterationDelay;


                for (int i = 1; i < totalTimesExecuted; i++)
                {
                    foreach (var step in block)
                    {
                        steps.Add(new Step
                        {
                            Delay = step.Delay + baseOffset * i,
                            Action = step.Action,
#if DebugModeActive
                            ActionName = step.ActionName
#endif
                        });
                    }
                }
                accumulatedDelay += baseOffset * (totalTimesExecuted - 1);


                return this;
            }

            /// <summary>
            /// Schedules all actions using DelayedActions.
            /// The same group as the creation of the sequence will be used.
            /// </summary>
            /// <param name="locator">Locator for all actions in the sequence</param>
            /// <returns>List of all the actions' Ids</returns>
            public List<long> Start(int locator = 0)
            {

                List<long> ids = new();
                foreach (var step in steps)
                {
                    ids.Add(
                    DelayedActions<Tag>.Do(
                            executor,
                            step.Delay,
                            step.Action,
                            locator
#if DebugModeActive
                            , step.ActionName
#endif
                            ));
                }

                return ids;
            }
            /// <summary>
            /// Loops the last section of the sequence infinitely.
            /// Non repeated commands are executed once and then discarded. 
            /// LoopInfinite consumes the builder and should not be reused.
            /// </summary>
            /// <param name="locator">Locator for all actions in the sequence, including repeating it</param>
            /// <returns>List of all the assigned Ids, will be filled with 'locator' if one was given</returns>
            public List<long> LoopInfinite(int locator)
            {
                return LoopInfinite(0, locator);
            }
            /// <summary>
            /// Loops infinitely the las section of the sequence.
            /// Non repeated commands are executed once and then discarded.
            /// LoopInfinite consumes the builder and should not be reused.
            /// </summary>
            /// <param name="locator">Locator for all actions in the sequence, including repeating it</param>
            /// <param name="iterationDelay">Seconds of delay between the end of one iteration and the start of the next</param>
            /// <returns>List of  the assigned Ids for the first iteration. Following iterations will <b>not</b> preserve Ids</returns>
            public List<long> LoopInfinite(double iterationDelay = 0, int locator = 0)
            {
                SectionInfo section = ConsumeSectionInfo();
                if (section.StartIndex >= steps.Count)
                {
                    //Empty section
                    return new List<long>();
                }

                List<long> ids = new();

                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];

                    long tempID = DelayedActions<Tag>.Do(
                            executor,
                            step.Delay,
                            step.Action,
                            locator
#if DebugModeActive
                            , step.ActionName
#endif
                            );

                    if (i >= section.StartIndex)
                    {
                        steps[i].Delay -= section.StartDelay;
                        ids.Add(tempID);
                    }

                }

                //remove the non looping part of the sequence
                accumulatedDelay -= section.StartDelay;
                steps.RemoveRange(0, section.StartIndex);

                if (steps.Count == 0)
                {
                    Debug.LogWarning(
                        "LoopInfinite called on an empty section.");
                    return ids;
                }

                long loopingActionID = 0;
                loopingActionID = DelayedActions<Tag>.Do(executor, accumulatedDelay + section.StartDelay + iterationDelay,
                    () =>
                     {
                         LoopInfiniteBase(iterationDelay, locator);
                     }, locator, "Sequence Loop");

                return ids;
            }

            private void LoopInfiniteBase(double iterationDelay, int locator) //Looping from the start of the the sequence
            {
                for (int i = 0; i < steps.Count; i++)
                {

                    DelayedActions<Tag>.Do(
                        executor,
                        steps[i].Delay,
                        steps[i].Action,
                        locator
#if DebugModeActive
                        , steps[i].ActionName
#endif
                        );
                }
                DelayedActions<Tag>.Do(executor, accumulatedDelay + iterationDelay,
                               () =>
                               {
                                   LoopInfiniteBase(iterationDelay, locator);
                               }, locator, "Sequence Loop");

                return;
            }
            private SectionInfo ConsumeSectionInfo()
            {
                if (sectionStack.Count > 0)
                    return sectionStack.Pop();

                // Implicit root section
                return new SectionInfo { StartIndex = 0, StartDelay = 0 };

            }


            private class Step
            {
                public double Delay;
                public Action Action;
#if DebugModeActive
                public string ActionName;
#endif
            }
        }

        #endregion

        #endregion

        #region InternalWorking
        private static long Do(DelayedActionInfo info, MonoBehaviour executor)
        {
            CheckDelayedActionsInScene();
            delayedActionsInScene.AddAction(info, executor);
            return info.id;

        }
        private static void CheckDelayedActionsInScene()
        {

            if (delayedActionsInScene == null)
            {
                GameObject go = new GameObject(objName);
                delayedActionsInScene = go.AddComponent<DelayedActionsInScene>();
            }

        }

        #endregion
    }

    #region Essential Internal Classes

    internal class DelayedActionsInScene : MonoBehaviour
    {
        #region Fields
        private static Transform DAParent;

        private Dictionary<MonoBehaviour, List<DelayedActionInfo>> delayedActions = new();
        private bool abortWholeGroup;
#if DebugModeActive

        public IEnumerable<(MonoBehaviour target, List<DelayedActionInfo> actions)> DebugView
        {
            get
            {
                foreach (var kv in delayedActions)
                    yield return (kv.Key, kv.Value);
            }
        }

#endif
        #endregion

        #region Internal Methods
        public void AddAction(DelayedActionInfo info, MonoBehaviour target)
        {
            if (delayedActions.ContainsKey(target))
            {
                delayedActions[target].Add(info);

            }
            else
            {
                delayedActions.Add(target, new List<DelayedActionInfo> { info });
            }
            abortWholeGroup = false;
        }
        #region Aborts
        public void Abort(MonoBehaviour target)
        {

            if (delayedActions.TryGetValue(target, out var list))
            {
                foreach (var info in list)
                {
                    info.cancelled = true;
                }
            }

        }
        public void Abort(MonoBehaviour target, int locator)
        {
            delayedActions.TryGetValue(target, out List<DelayedActionInfo> list);
            list?.ForEach(
                (info) =>
                {
                    if (info.locator == locator)
                    {
                        info.cancelled = true;
                    }
                }
                );

        }
        public void Abort(int locator)
        {
            var keys = delayedActions.Keys.ToList();

            foreach (var key in keys)
            {
                Abort(key, locator);
            }
        }
        public void AbortById(long id)
        {

            foreach (var pair in delayedActions)
            {
                foreach (var info in pair.Value)
                {
                    if (info.id == id)
                    {

                        info.cancelled = true;
                        return;
                    }
                }
            }
        }
        public void AbortGroup()
        {
            abortWholeGroup = true;
            foreach (var pair in delayedActions)
            {
                foreach (var info in pair.Value)
                {
                    info.cancelled = true;
                }
            }
        }
        #endregion
        #region Query
        public bool IsScheduled(long id)
        {
            foreach (var pair in delayedActions)
            {
                foreach (var info in pair.Value)
                {
                    if (info.id == id)
                        return !info.cancelled;
                }
            }
            return false;
        }

        public double GetTimeLeft(long id)
        {
            foreach (var pair in delayedActions)
            {
                foreach (var info in pair.Value)
                {
                    if (info.id == id)
                        return info.delay;
                }
            }
            return -1;
        }
        public DelayedActionInfo GetAction(long id)
        {
            foreach (var pair in delayedActions)
            {
                foreach (var info in pair.Value)
                {
                    if (info.id == id)
                        return info;
                }
            }
            return null;
        }
        #endregion
        #endregion

        #region Unity Internal Working
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (DAParent != null)
            {
                transform.SetParent(DAParent.transform);
            }
            else
            {
                var go = new GameObject("DelayedActions");
                DontDestroyOnLoad(go);
                transform.SetParent(go.transform);
                DAParent = go.transform;
            }
        }

        void Update()
        {
            var keys = new List<MonoBehaviour>(delayedActions.Keys);
            foreach (var executor in keys)
            {
                if (executor == null) //Destroyed gameobjects
                {
                    delayedActions.Remove(executor);
                    continue;
                }
                List<DelayedActionInfo> list = delayedActions[executor];
                int originalCount = list.Count;
                int writeIndex = 0;
                List<DelayedActionInfo> toExecute = null;

                //Save the ready to execute actions in a temp list. Hold the rest in the original list.
                //This makes it so that deleting is faster later.
                for (int i = 0; i < originalCount; i++)
                {
                    var actionInfo = list[i];

                    if (executor.isActiveAndEnabled) //disabled components dont tick seconds
                    {
                        actionInfo.delay -= Time.deltaTime;
                    }

                    if (actionInfo.delay <= 0 || actionInfo.cancelled)
                    {
                        if (!actionInfo.cancelled)
                        {
                            (toExecute ??= new List<DelayedActionInfo>()).Add(actionInfo);
                        }
                    }
                    else
                    {
                        list[writeIndex++] = actionInfo;
                    }
                }

                //Execute.
                if (toExecute != null)
                {
                    foreach (var actionInfo in toExecute)
                    {
                        if(!actionInfo.cancelled)
                            ExecuteAction(actionInfo, executor);
                    }
                }

                //Remove the slots left by the executed actions. This is faster than removing one by one.
                list.RemoveRange(writeIndex, originalCount - writeIndex);
                if (list.Count == 0) // No more actions for this gameobject
                    delayedActions.Remove(executor);

            }

            if (abortWholeGroup && delayedActions.Count == 0) //Only destroy the go if the user wanted to and there is no more executors.
            {
                Destroy(gameObject);
            }

        }
        void ExecuteAction(DelayedActionInfo actionInfo, MonoBehaviour executor)
        {

            try
            {
                actionInfo.action?.Invoke();

            }
            catch (System.Exception e)
            {
#if DebugModeActive
                Debug.LogError($"DelayedActions. Error executing action '{actionInfo.actionName}' (locator: {actionInfo.locator}) on target {executor.name} ({executor.GetType().Name}). DelayedActions");
#endif
                Debug.LogException(e);

            }
        }
#if DebugModeActive
        public void DebugActions()
        {
            Debug.Log("Debbugging DelayedActions: " + gameObject.name);
            foreach (var pair in delayedActions)
            {
                Debug.Log($"\tTarget: {pair.Key.name} ({pair.Key.GetType().Name})", pair.Key);

                foreach (var actionInfo in pair.Value)
                {
                    Debug.Log(
                                $"\t\t -> Action: {actionInfo.actionName}, " +
                                $"Delay: {actionInfo.delay:F2}s, " +
                                $"Locator: {actionInfo.locator}, " +
                                $"ID: {actionInfo.id}"
                                );
                }
            }
        }
#endif

        #endregion
    }
    /// <summary>
    /// Class that represents a scheduled action. Contains the action, the delay, the locator, the name and whether it was cancelled.
    /// Changes to the fields of the object WILL affect the scheduled action. Use with care.
    /// </summary>
    [Serializable]
    public class DelayedActionInfo
    {
        public Action action;

#if DebugModeActive
        public string actionName;
#endif
        public double delay;
        public int locator;
        public bool cancelled = false;

        public readonly long id;
        private static long idCounter = 0;
        internal DelayedActionInfo()
        {


            Interlocked.Increment(ref idCounter);
            id = idCounter;

        }


    }
    #endregion

    #region Editor Debugging

#if DebugModeActive

    [CustomEditor(typeof(DelayedActionsInScene))]
    internal class DelayedActionsInSceneEditor : Editor
    {
        MonoScript DAscript = null;
        private bool debugListExpanded = true;
        private readonly Dictionary<UnityEngine.Object, bool> executorFoldouts = new();

        //For the dual inspector
        private DelayedActionInfo editingAction;

        private string editingControlName = null;

        public override void OnInspectorGUI()
        {
            //Show script reference
            {

                GUI.enabled = false;
                { //Show script correctly
                    if (DAscript == null)
                    {
                        string[] guids = AssetDatabase.FindAssets("DelayedActions t:MonoScript");

                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            DAscript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                        }
                    }
                }
                EditorGUILayout.ObjectField("DelayedActionsScript", DAscript, typeof(MonoScript), false);
                GUI.enabled = true;
            }
            //Inspector
            {

                var runtime = (DelayedActionsInScene)target;



                // Foldout
                debugListExpanded = EditorGUILayout.Foldout(debugListExpanded, $"Executors List", true);

                if (debugListExpanded)
                {
                    EditorGUI.indentLevel++;

                    // Outer list box
                    var listBoxStyle = new GUIStyle("box") { padding = new RectOffset(1, 1, 1, 1) };
                    EditorGUILayout.BeginVertical(listBoxStyle);


                    foreach (var pair in runtime.DebugView)
                    {

                        if (!executorFoldouts.ContainsKey(pair.target))
                            executorFoldouts[pair.target] = true;

                        EditorGUILayout.BeginHorizontal();
                        executorFoldouts[pair.target] = EditorGUILayout.Foldout(
                            executorFoldouts[pair.target],
                            $"{pair.target.name} ({pair.actions.Count} actions)",
                            true
                        );

                        bool isUnolded = executorFoldouts[pair.target];
                        if (isUnolded)
                        {
                            GUILayout.Space(5);
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.ObjectField("", pair.target, typeof(MonoScript), false);

                            if (GUILayout.Button("✕", GUILayout.Width(20)))
                            {
                                runtime.Abort(pair.target);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        if (isUnolded)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                            GUILayout.Space(2);

                            Rect lineRect = EditorGUILayout.GetControlRect(false, 4f);

                            lineRect.x += 5f;
                            lineRect.width -= 10f;
                            EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f, 0.4f));

                            foreach (var action in pair.actions)
                            {

                                DrawAction(action);

                                lineRect = EditorGUILayout.GetControlRect(false, 4f);
                                lineRect.x += 5f;
                                lineRect.width -= 10f;
                                EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f, 0.4f));
                            }
                            EditorGUILayout.EndVertical();
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.Space(3);

                    }


                    EditorGUILayout.EndVertical();


                    EditorGUI.indentLevel--;

                }
                EditorGUILayout.Space();
            }
            //Column headers
            {

                Rect headerRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                float w = headerRect.width;
                float x = headerRect.x;
                float y = headerRect.y;
                float h = headerRect.height;

                float fifthWidth = w / 5;
                var actionRect = new Rect(x+20, y, (fifthWidth * 2) - 5, h);
                var delayRect = new Rect(x + (fifthWidth * 2) + 5, y, (fifthWidth * 2) - 5, h);
                var locatorRect = new Rect(x + (fifthWidth * 4) + 5, y, fifthWidth - 5, h);
                EditorGUI.indentLevel++;
                
                var headerStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };

                EditorGUI.LabelField(actionRect, "Action Name", headerStyle);
                EditorGUI.LabelField(delayRect, "Delay", headerStyle);
                EditorGUI.LabelField(locatorRect, "Locator", headerStyle);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "Double-click any field to edit.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(2);


            // Print actions button
            {
                if (GUILayout.Button("🔁 Print Debug Actions"))
                {
                    DelayedActionsInScene script = (DelayedActionsInScene)target;
                    script.DebugActions();
                }

            }
        }
        void DrawAction(DelayedActionInfo action)
        {
            EditorGUILayout.BeginHorizontal();

            Color oldColor = GUI.color;

            if (action.cancelled)
                GUI.color = Color.gray;

            Rect rowRect = EditorGUILayout.GetControlRect(false);
            float w = rowRect.width;
            float spacing = 4f;

            float fifthWidth = (w-(3*spacing)) / 5;
            float actionWidth = (fifthWidth * 2) - spacing;
            float delayWidth = (fifthWidth * 2) - spacing;
            float buttonWidth = 22f;

            float locatorWidth = fifthWidth - spacing - buttonWidth;



            Rect actionRect = new Rect(
                rowRect.x,
                rowRect.y,
                actionWidth,
                rowRect.height
            );

            Rect delayRect = new Rect(
                actionRect.xMax + spacing,
                rowRect.y,
                delayWidth,
                rowRect.height
            );

            Rect locatorRect = new Rect(
                delayRect.xMax + spacing,
                rowRect.y,
                locatorWidth,
                rowRect.height
            );

            Rect buttonRect = new Rect(
                locatorRect.xMax + spacing,
                rowRect.y,
                buttonWidth,
                rowRect.height
            );

            //Draw fields

            {
                DrawEditableField(actionRect, action, Field.Name);
                
                DrawEditableField(delayRect, action, Field.Delay);

                DrawEditableField(locatorRect,action, Field.Locator);
            }
           


            if (GUI.Button(buttonRect, "✕", EditorStyles.miniButton))
            {
                action.cancelled = true;
                Repaint();
            }
            GUI.color = oldColor;
            EditorGUILayout.EndHorizontal();

        }
        private enum Field
        {
            Name,
            Delay,
            Locator
        }
        private void DrawEditableField(Rect rect, DelayedActionInfo owner, Field field)
        {
            string controlName = field.ToString();
            bool editing = editingAction == owner && editingControlName == controlName;

            if (editing)
            {
                GUI.SetNextControlName(controlName);

                switch (field)
                {
                    case Field.Name:
                        owner.actionName = EditorGUI.TextField(rect, owner.actionName);
                        break;
                    case Field.Delay:
                        owner.delay = EditorGUI.DoubleField(rect, owner.delay);
                        break;
                    case Field.Locator:
                        owner.locator = EditorGUI.IntField(rect, owner.locator);
                        break;
                }

                // Exit edit mode when the field loses focus.

                if (Event.current.type == EventType.MouseDown && !rect.Contains(Event.current.mousePosition))
                {
                    editingAction = null;
                    editingControlName = null;
                }
            }
            else
            {
                string displayText = "";
                switch (field)
                {
                    case Field.Name:
                        displayText = owner.actionName;
                        break;
                    case Field.Delay:
                        displayText = owner.delay.ToString("F2");
                        break;
                    case Field.Locator:
                        displayText = owner.locator.ToString();
                        break;
                }

                EditorGUI.LabelField(rect, displayText);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    editingAction = owner;
                    editingControlName = controlName;

                    Repaint();
                    Event.current.Use();
                }
            }
        }
        public override bool RequiresConstantRepaint()
        {
            return true;
        }
    }
#endif
    #endregion
}