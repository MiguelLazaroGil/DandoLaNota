using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HighlighterScript : MonoBehaviour
{
    [SerializeField]
    CompleteTaggedDetector detector;
    [Header("Layers")]
    [SerializeField]
    private int defaultLayer = 0;
    
    [SerializeField]
    private int highlightLayer = 0;
    private int lastLayer;
    [Header("Params")]
    [SerializeField, Tooltip("Highlighting all wont return all of the gameObjects to previous layer")]
    private bool returnToPreviousLayer;
    [SerializeField, Tooltip("If true will change the highlighted's children's layers too.")]
    private bool recursiveLayerChange;

    private GameObject lastTarget;
 
    private void UpdateTarget(GameObject go)
    {
        GameObject newTarget = go;
        
        if(lastTarget != null)
        {
           changeLayer(lastTarget, lastLayer);

        }
        if(newTarget != null)
        {
            if (returnToPreviousLayer)
            {
                lastLayer = newTarget.layer;
            }
            changeLayer(newTarget, highlightLayer);
        }

        lastTarget = newTarget;
        

    }
    private void changeLayer(GameObject go, int layer)
    {
        go.gameObject.layer = layer;
        if (recursiveLayerChange)
        {

            foreach (Transform hijoTarget in go.transform)
            {
                hijoTarget.gameObject.layer = layer;
            }
        }
    }
    private void Start()
    {
        lastTarget = null;
        detector.onTargetChanged.AddListener(UpdateTarget);
        lastLayer = defaultLayer;
    }
    private void OnDisable()
    {
        detector?.onTargetChanged.RemoveListener(UpdateTarget);
    }
}
