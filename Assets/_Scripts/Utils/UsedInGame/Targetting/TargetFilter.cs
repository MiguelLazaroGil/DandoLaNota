using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TargetFilter
{
    [Header("Ignores")]
    [SerializeField] public bool ignoreSelf = true;
    [SerializeField] public bool ignoreChildren = true;
    [SerializeField] public bool ignoreParents = true;
    [SerializeField] public bool ignoreSpecificGameObjects = false;
    [ShowIf("ignoreSpecificGameObjects")]
    [SerializeField] public GameObject[] specificGameObjectsToIgnore = new GameObject[0];

    [Header("Inclusions")]
    [SerializeField] public bool selectOnlySpecificGameObjects = false;
    [ShowIf("selectOnlySpecificGameObjects")]
    [SerializeField] public GameObject[] specificGameObjectsToSelect = new GameObject[0];

    [Header("Tags")]
    [SerializeField] public bool filterByTag = false;
    [SerializeField, ShowIf("filterByTag")] public string targetTag = "";
    [SerializeField, ShowIf("filterByTag")] public bool useOtherTags = false;
    [SerializeField, ShowIf("useOtherTags")] public string[] otherTags = new string[0];

    [Header("Layers")]
    [SerializeField] public bool filterByLayer = true;
    [SerializeField, ShowIf("filterByLayer")] public LayerMask targetLayerMask = ~0;

    /// <summary>
    /// Devuelve el LayerMask efectivo para acelerar las consultas de físicas (Overlap/Raycast).
    /// </summary>
    public LayerMask PhysicsLayerMask => filterByLayer ? targetLayerMask : (LayerMask)~0;

    /// <summary>
    /// Comprueba si un GameObject cumple todas las condiciones configuradas.
    /// </summary>
    public bool IsValidTarget(GameObject target, Transform owner = null)
    {
        if (target == null) return false;

        // 1. Selección exclusiva por objetos específicos
        if (selectOnlySpecificGameObjects)
        {
            if (specificGameObjectsToSelect == null || !specificGameObjectsToSelect.Contains(target))
                return false;
        }

        // 2. Comprobaciones de ignorado por relación jerárquica u objeto
        if (owner != null)
        {
            if (ignoreSelf && target == owner.gameObject)
                return false;

            if (ignoreChildren && target.transform.IsChildOf(owner))
                return false;

            if (ignoreParents && owner.IsChildOf(target.transform))
                return false;
        }

        if (ignoreSpecificGameObjects && specificGameObjectsToIgnore != null)
        {
            if (specificGameObjectsToIgnore.Contains(target))
                return false;
        }

        // 3. Filtro por Layer
        if (filterByLayer)
        {
            if (((1 << target.layer) & targetLayerMask) == 0)
                return false;
        }

        // 4. Filtro por Tag
        if (filterByTag)
        {
            bool matchesMainTag = !string.IsNullOrEmpty(targetTag) && target.CompareTag(targetTag);
            bool matchesOtherTag = false;

            if (!matchesMainTag && useOtherTags && otherTags != null)
            {
                for (int i = 0; i < otherTags.Length; i++)
                {
                    if (!string.IsNullOrEmpty(otherTags[i]) && target.CompareTag(otherTags[i]))
                    {
                        matchesOtherTag = true;
                        break;
                    }
                }
            }

            if (!matchesMainTag && !matchesOtherTag)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Filtra una colección de GameObjects devolviendo solo los válidos.
    /// </summary>
    public GameObject[] Filter(IEnumerable<GameObject> candidates, Transform owner = null)
    {
        if (candidates == null) return Array.Empty<GameObject>();
        return candidates.Where(c => IsValidTarget(c, owner)).ToArray();
    }
}