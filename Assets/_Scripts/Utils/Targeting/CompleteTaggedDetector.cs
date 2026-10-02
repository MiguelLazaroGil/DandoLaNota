using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
[RequireComponent(typeof(Collider))]
public class CompleteTaggedDetector : MonoBehaviour, ITargeter
{
    public UnityEvent<GameObject> onTargetChanged;
    [Header("Camera settings")]
    [SerializeField, Tooltip("If active, the collider detector will move from the center of the camera forwards")]
    private bool moveWithCamera = true;
    [SerializeField, HideIf("moveWithCamera", false), Tooltip("Optinal. Default is Camera.main")]
    private GameObject cameraGO;
    [SerializeField, HideIf("moveWithCamera", false), Tooltip("Max distance from the camera the detector will reach,\nthis does not take into account the detector's size"), Range(0, 50)]
    private float range;
    [Space]
    [Header("Tags")]
    [SerializeField]
    private bool multipleTags;
    [HideIf("multipleTags", true)]
    public String singleTag;
    [SerializeField, ShowIf("multipleTags"), Tooltip("Every tag which will be detected. *Detection is done through using unity triggers system*")]
    public List<String> checkedTags;

    [Header("Params")]
    [SerializeField, Tooltip("Toggle if you change obj tags in game while the would/wouldn't be selected obj is in the detector's collider. Very rarely.")]
    bool checkOnStay;
    [SerializeField]
    bool setLayerToIgnoreRayCast = true;
    [SerializeField, ReadOnly] private List<GameObject> itemsInTrigger = new List<GameObject>();

    [SerializeField, ReadOnly] private GameObject currentTarget;

    private bool locked;

    public bool HasTarget()
    {
        return currentTarget != null;
    }

    /// <summary>
    /// Gets the closest target to the center of the detector.
    /// </summary>
    /// <returns>Might be null</returns>
    public GameObject GetTarget()
    {
        return currentTarget;
    }
    /// <summary>
    /// Gets all detected objs currently in the detector's collider.
    /// </summary>
    /// <returns>Might return an empty array</returns>
    public GameObject[] GetAllTargets()
    {
        return itemsInTrigger.ToArray();
    }
    /// <summary>
    /// Lock state makes it so that it doesnt move with respect to the camera and doesnt update the current target. *The detector will update alltargets regardless.
    /// </summary>
    /// <param name="locked"></param>
    public void SetLocked(bool locked)
    {
        this.locked = locked;
    }
    private bool CheckTag(string tag)
    {
        if (multipleTags)
        {
            return checkedTags.Contains(tag);
        }
        else
        {
            return singleTag == tag;
        }
    }
    private void OnTriggerEnter(Collider other)
    {

        if (CheckTag(other.tag))
        {
            itemsInTrigger.Add(other.gameObject); // A�adir el item a la lista
        }
    }

    private void OnTriggerStay(Collider other)
    {

        if (!checkOnStay) return;
        if (CheckTag(other.tag))
        {
            if (!itemsInTrigger.Contains(other.gameObject))
            {
                itemsInTrigger.Add(other.gameObject); // A�adir el item a la lista
            }

        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (CheckTag(other.tag))
        {
            itemsInTrigger.Remove(other.gameObject); // Remover el item de la lista
        }
    }

    // M�todo para obtener el item m�s cercano
    private GameObject GetClosestItem()
    {
        GameObject closestItem = null;
        float closestDistance = Mathf.Infinity;

        // Posici�n del cubo invisible
        Vector3 cubePosition = transform.position;

        // Recorrer los items que est�n en colisi�n
        foreach (GameObject item in itemsInTrigger)
        {
            if (item == null) continue;
            if (!CheckTag(item.tag))
            {
                //El objeto sigue en la listapero ya no es interactuable, se le echa
                itemsInTrigger.Remove(item);
                return GetClosestItem(); //Se repite con la lista buena
            }
            // Calcular la distancia al cubo
            float distanceToCube = Vector3.Distance(cubePosition, item.transform.position);

            // Si la distancia es menor a la m�s cercana registrada, actualizar la referencia
            if (distanceToCube < closestDistance)
            {
                closestDistance = distanceToCube;
                closestItem = item;
            }
        }


        return closestItem; // Retornar el item m�s cercano o null si no hay ninguno
    }

    private void Update()
    {
        RemoveNUllOrDestroyed();
        if (locked) return;
        Move();
        var temp = GetClosestItem();
        if (temp != currentTarget)
        {
            currentTarget = temp;
            onTargetChanged.Invoke(currentTarget);
        }


    }
    private void Move()
    {
        if (!moveWithCamera) { return; }
        // Posici�n de la c�mara
        Vector3 cameraPosition = cameraGO.transform.position;

        // Direcci�n hacia donde la c�mara est� mirando
        Vector3 cameraForward = cameraGO.transform.forward;

        // Raycast para detectar obst�culos
        Ray ray = new Ray(cameraPosition, cameraForward);
        RaycastHit hit;

        // Si el Raycast golpea algo a menos de maxDistance, colocamos el cubo en ese punto
        if (Physics.Raycast(ray, out hit, range))
        {
            // Mover el cubo a la posici�n de impacto
            transform.position = hit.point;
        }
        else
        {
            // Si no golpea nada, mover el cubo a la distancia m�xima
            transform.position = cameraPosition + cameraForward * range;
        }

    }
    public void NullTarget()
    {
        currentTarget = null;
        itemsInTrigger.Clear();
    }
    private void RemoveNUllOrDestroyed()
    {
        List<GameObject> toRemove = new List<GameObject>();
        foreach (GameObject go in itemsInTrigger)
        {
            if (go == null)
            {
                toRemove.Add(go);
            }
        }
        foreach (GameObject go in toRemove)
        {
            itemsInTrigger.Remove(go);
        }
    }
    private void Awake()
    {

        if (cameraGO == null)
        {
            cameraGO = Camera.main.gameObject;
        }
        if (setLayerToIgnoreRayCast)
        {
            this.gameObject.layer = 2;
        }


    }

    private void OnDrawGizmos()
    {

        Color c = new Color(0, 0, 0, 0.55f);
        foreach (string t in checkedTags)
        {
            if (t.Length == 0) continue;
            c += new Color((t[0] % 3) / 2, ((1 + t[0]) % 3) / 2, ((2 + t[0]) % 3) / 2, 0.0f);
            if (t.Length == 1) continue;

            c += new Color((t[1] % 3) / 2, ((1 + t[1]) % 3) / 2, ((2 + t[1]) % 3) / 2, 0.0f);
        }
        Gizmos.color = c;
        if (this.cameraGO != null)
        {
            if (cameraGO)
            {
                Gizmos.DrawWireSphere(this.cameraGO.transform.position, range);

            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, range);
        }

    }
    public class Interactable : MonoBehaviour
    {
        public void Interact()
        {

        }
    }
}
