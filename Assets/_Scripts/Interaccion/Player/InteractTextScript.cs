using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InteractTextScript : MonoBehaviour
{
    [SerializeField]
    CompleteTaggedDetector detector;
    [SerializeField]
    private TextMeshProUGUI textInteract;
   
 
    private void Start()
    {  
        textInteract.text = "";
        detector.onTargetChanged.AddListener(UpdateTarget);
    }
    public void UpdateTarget(GameObject go)
    {

        string tempText = "";
        if (go)
        {
            InteractTextComponent interact = go.GetComponent<InteractTextComponent>();
            if (interact != null)
            {
                tempText = interact.interactText;
            }
        }
        textInteract.text = tempText;

    }
    private void OnDisable()
    {

        detector?.onTargetChanged.RemoveListener(UpdateTarget);
    }
}

