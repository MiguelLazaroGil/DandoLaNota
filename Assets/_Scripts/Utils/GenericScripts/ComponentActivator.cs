using System;
using System.Collections.Generic;
using GVUtils.Attributes;
using UnityEngine;

public class ComponentActivator : MonoBehaviour
{
    internal enum InitialState { ACTIVATE, DEACTIVATE, NONE }
    [SerializeField]
    List<MonoBehaviour> monoBehaviours;
    [SerializeField]
    InitialState initialState = InitialState.DEACTIVATE;
    public void Awake()
    {
        switch (initialState)
        {
            case InitialState.ACTIVATE:
                ActivateMonoBehaviours();
                break;
            case InitialState.DEACTIVATE:
                DeActivateMonoBehaviours();
                break;

        }
    }

    public void ActivateMonoBehaviours()
    {
        foreach (var mono in monoBehaviours)
        {
            mono.enabled = true;
        }
    }
    public void DeActivateMonoBehaviours()
    {
        foreach (var mono in monoBehaviours)
        {
            mono.enabled = false;
        }
    }
}
