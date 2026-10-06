using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class CompleteTaggedDetector : MonoBehaviour, ITargeter
{
    public UnityEvent<GameObject> onTargetChanged;

    [Header("Camera Settings")]
    [SerializeField, Tooltip("If active, the collider detector will move from the center of the camera forwards")]
    private bool moveWithCamera = true;
    [SerializeField, HideIf("moveWithCamera", false), Tooltip("Optional. Default is Camera.main")]
    private GameObject cameraGO;
    [SerializeField, HideIf("moveWithCamera", false), Range(0, 50)]
    private float range = 10f;

    [Header("Filters Configuration")]
    [SerializeField] public TargetFilter filter = new TargetFilter();

    [Header("Params")]
    [SerializeField, Tooltip("Toggle if target properties change while inside the trigger.")]
    private bool checkOnStay = false;
    [SerializeField]
    private bool setLayerToIgnoreRayCast = true;

    [SerializeField, ReadOnly] private List<GameObject> itemsInTrigger = new List<GameObject>();
    [SerializeField, ReadOnly] private GameObject currentTarget;

    private bool locked;

    public bool HasTarget() => currentTarget != null;
    public GameObject GetTarget() => currentTarget;
    public GameObject[] GetAllTargets() => filter.Filter(itemsInTrigger, transform);

    public void SetLocked(bool isLocked)
    {
        locked = isLocked;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (filter.IsValidTarget(other.gameObject, transform))
        {
            if (!itemsInTrigger.Contains(other.gameObject))
            {
                itemsInTrigger.Add(other.gameObject);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!checkOnStay) return;

        bool isValid = filter.IsValidTarget(other.gameObject, transform);
        bool contains = itemsInTrigger.Contains(other.gameObject);

        if (isValid && !contains)
        {
            itemsInTrigger.Add(other.gameObject);
        }
        else if (!isValid && contains)
        {
            itemsInTrigger.Remove(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        itemsInTrigger.Remove(other.gameObject);
    }

    private GameObject GetClosestItem()
    {
        GameObject closestItem = null;
        float closestDistance = Mathf.Infinity;
        Vector3 position = transform.position;

        for (int i = itemsInTrigger.Count - 1; i >= 0; i--)
        {
            GameObject item = itemsInTrigger[i];

            if (item == null)
            {
                itemsInTrigger.RemoveAt(i);
                continue;
            }

            if (!filter.IsValidTarget(item, transform))
            {
                itemsInTrigger.RemoveAt(i);
                continue;
            }

            float distance = Vector3.Distance(position, item.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestItem = item;
            }
        }

        return closestItem;
    }

    private void Update()
    {
        RemoveNullOrDestroyed();
        if (locked) return;

        Move();

        GameObject temp = GetClosestItem();
        if (temp != currentTarget)
        {
            currentTarget = temp;
            onTargetChanged.Invoke(currentTarget);
        }
    }

    private void Move()
    {
        if (!moveWithCamera || cameraGO == null) return;

        Vector3 cameraPosition = cameraGO.transform.position;
        Vector3 cameraForward = cameraGO.transform.forward;
        Ray ray = new Ray(cameraPosition, cameraForward);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            transform.position = hit.point;
        }
        else
        {
            transform.position = cameraPosition + cameraForward * range;
        }
    }

    public void NullTarget()
    {
        currentTarget = null;
        itemsInTrigger.Clear();
    }

    private void RemoveNullOrDestroyed()
    {
        itemsInTrigger.RemoveAll(item => item == null);
    }

    private void Awake()
    {
        if (cameraGO == null && Camera.main != null)
        {
            cameraGO = Camera.main.gameObject;
        }

        if (setLayerToIgnoreRayCast)
        {
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // Layer 2
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
        Transform referenceTransform = cameraGO != null ? cameraGO.transform : transform;
        Gizmos.DrawWireSphere(referenceTransform.position, range);
    }
}