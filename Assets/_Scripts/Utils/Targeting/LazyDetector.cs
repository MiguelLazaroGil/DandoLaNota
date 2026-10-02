
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class LazyDetector : MonoBehaviour, ITargeter
{
    [SerializeField, ReadOnly] GameObject lastTargetFetched = null;
    private enum DetectionShape
    {
        Sphere,
        Box,
    }
    [Header("Physics")]
    [SerializeField] float detectionRange = 2f;
    [SerializeField] DetectionShape shape = DetectionShape.Sphere;

    [Header("Settings")]
    [SerializeField] bool ignoreSelf = true;
    [SerializeField] bool ignoreChildren = true;
    [SerializeField] bool ignoreParents = true;
    [SerializeField] bool ignoreSpecificGameObjects = false;
    [ShowIf("ignoreSpecificGameObjects")]
    public GameObject[] specificGameObjectsToIgnore = new GameObject[0];
    [Header("Filters")]
    [Space]

    [SerializeField] bool selectOnlySpecificGameObjects = false;
    [ShowIf("selectOnlySpecificGameObjects")]
    public GameObject[] specificGameObjectsToSelect = new GameObject[0];
    [Space]
    [SerializeField] bool filterByTag = false;
    [SerializeField, HideIf("filterByTag", false)] string targetTag = "";
    [SerializeField, HideIf("filterByTag", false)] bool useOtherTags = false;
    [SerializeField, HideIf("useOtherTags", false), ShowIf("useOtherTags")] string[] otherTags = new string[0];
    [Space]
    [SerializeField] bool filterByLayer = true;
    [SerializeField, HideIf("filterByLayer", false)] LayerMask targetLayerMask = 1;



    public virtual GameObject[] GetAllTargets()
    {

        GameObject[] targets = GetPhysicsTargets();
        targets= ApplyIgnores(targets);
        if (filterByTag)
        {
            targets = ApplyTagFilters(targets);
        }
        if(filterByLayer)
        {
            targets = ApplyLayerFilters(targets,targetLayerMask);
        }
        if (selectOnlySpecificGameObjects)
        {
            targets = ApplySpecificSelection(targets);
        }
        return targets;
    }

    public GameObject GetTarget()
    {
        return lastTargetFetched= GetClosestItem(GetAllTargets());
    }
    /// <summary>
    /// En LazyDetector, no usais HasTarget y luego GetTarget porque va a hacer dos detecciones fisicas. Usad solo GetTarget y manejad el posible null
    /// </summary>
    /// <returns></returns>
    public bool HasTarget()
    {
        return GetAllTargets().Length > 0;
    }
    private GameObject[] GetPhysicsTargets()
    {
        Collider[] hits = new Collider[0];
        if (shape == DetectionShape.Sphere)
        {
            hits = Physics.OverlapSphere(transform.position, detectionRange, targetLayerMask);
        }
        else if (shape == DetectionShape.Box)
        {
            hits = Physics.OverlapBox(transform.position, Vector3.one * detectionRange * 0.5f, Quaternion.identity, targetLayerMask);
        }
        GameObject[] gameObjects = hits.Select(c => c.gameObject).ToArray();


        return gameObjects;
    }
    private GameObject[] ApplyIgnores(GameObject[] targets)
    {
        List<GameObject> ignoreTargets = new List<GameObject>();
        if (ignoreSpecificGameObjects)
        {
            ignoreTargets.AddRange(specificGameObjectsToIgnore);
        }
        if (ignoreSelf)
        {
            ignoreTargets.Add(this.gameObject);
        }
        if (ignoreChildren)
        {
            foreach (Transform child in transform)
            {
                ignoreTargets.Add(child.gameObject);
            }
        }
        if (ignoreParents)
        {
            Transform currentParent = transform.parent;
            while (currentParent != null)
            {
                ignoreTargets.Add(currentParent.gameObject);
                currentParent = currentParent.parent;
            }
        }
        targets = targets.Where(t => !ignoreTargets.Contains(t)).ToArray();
        return targets;
    }
    private GameObject[] ApplyTagFilters(GameObject[] targets)
    {
        List<GameObject> filtered = new List<GameObject>();
        foreach (var target in targets)
        {

            if (target.CompareTag(targetTag))
            {
                filtered.Add(target);
                break;
            }
            else if (useOtherTags)
            {
                if (otherTags.Contains(target.tag))
                {
                    filtered.Add(target);
                    break;

                }
            }

        }
        return filtered.ToArray();
    }
    private GameObject[] ApplyLayerFilters(GameObject[] targets, LayerMask targetLayerMask)
    {
     

        List<GameObject> filtered = new List<GameObject>();
        foreach (var target in targets)
        {
            if (((1 << target.layer) & targetLayerMask) != 0)
            {
                filtered.Add(target);
            }
        }
        return filtered.ToArray();
    }
    private GameObject[] ApplySpecificSelection(GameObject[] targets)
    {
        List<GameObject> filtered = new List<GameObject>();
        foreach (var target in targets)
        {
            if (specificGameObjectsToSelect.Contains(target))
            {
                filtered.Add(target);
            }
        }
        return filtered.ToArray();
    }

    private GameObject GetClosestItem(GameObject[] targets)
    {
        GameObject closestItem = null;
        float closestDistance = Mathf.Infinity;

 
        Vector3 cubePosition = transform.position;

   
        foreach (GameObject item in targets)
        {
           
    
            float distanceToCube = Vector3.Distance(cubePosition, item.transform.position);

         
            if (distanceToCube < closestDistance)
            {
                closestDistance = distanceToCube;
                closestItem = item;
            }
        }


        return closestItem; 
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (shape == DetectionShape.Sphere)
        {
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
        else if (shape == DetectionShape.Box)
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one * detectionRange);
        }
    }



}
