using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character.Controls;
using UnityEngine.Events;
public class GroundDetectorRaycast : MonoBehaviour, IGroundDetector
{

    [SerializeField] float Height = 1.5f;
    [SerializeField] bool useMultipleRays=true;
    [SerializeField] LayerMask IgnoreMask;
    [SerializeField]
    [ShowIf("useMultipleRays")]
    int nRays = 4;
    [SerializeField]
    [ShowIf("useMultipleRays")]
    float radius = 2f;
    private bool isGrounded;
    public bool IsGrounded { get => isGrounded; set => throw new System.NotImplementedException(); }
    [SerializeField]
    private Vector3 rayVector = Vector3.down;//whats supoused to point "down"

    private RaycastHit hit;
    private List<RaycastHit> hits=new List<RaycastHit>();
    bool[] isGroundedMultiple;
    public UnityEvent onLanding=new UnityEvent();


    // Start is called before the first frame update
    void Start()
    {
        if (useMultipleRays)
        {
            isGroundedMultiple=new bool[nRays];
            for (int i = 0; i < nRays; i++)
            {
                hits.Add(new RaycastHit());
                isGroundedMultiple[i] = false;
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        DetectGround();
    }

    public bool DetectGround()
    {
        if (!useMultipleRays)
        {
            if(Physics.Raycast(transform.position, rayVector, out hit, Height,~IgnoreMask)){
                bool wasGrounded = isGrounded;
                isGrounded = true;
                if (!wasGrounded && isGrounded)//tambien aqui se tiene que disparar el aterrizaje
                {
                    onLanding.Invoke();
                }
                Debug.Log(hit.distance);
            }
            else
            {
                isGrounded= false;
            }
        }
        else
        {
            Vector3 center = transform.position;
            
            for (int i = 0; i < nRays; i++)
            {
                float angulo = i * Mathf.PI * 2f / nRays;
                float x = Mathf.Cos(angulo) * radius;
                float z = Mathf.Sin(angulo) * radius;

                float lenght = hit.distance;
                Vector3 offset = new Vector3(x, 0, z);
                Vector3 punto = new Vector3(lenght * rayVector.x, lenght * rayVector.y, lenght * rayVector.z) + center + offset;
                RaycastHit raycastHit = hits[i];
                if(Physics.Raycast(punto, rayVector, out raycastHit, Height,~IgnoreMask)) {

                    isGroundedMultiple[i] = true;

                }
                else
                {
                    isGroundedMultiple[i] = false;
                }
                hits[i] = raycastHit;
               
            }
            isGrounded = IsGroundedMultiple();
            }
        return isGrounded;
    }
    bool IsGroundedMultiple()
    {
        bool wasGrounded=isGrounded;
        bool boolReturn = false;
        foreach(bool b in isGroundedMultiple)
        {
            boolReturn |= b;
        }
       
        if (!wasGrounded && boolReturn)//En el anterior frame no estaba en el suelo pero en este si, ha aterrizado joder
                                       {
            
            onLanding.Invoke(); }
        return boolReturn;
    }
#if UNITY_EDITOR
    void DrawCirclePoints()
    {
        Vector3 center=transform.position;
        Gizmos.color = Color.green;

        for (int i = 0; i < nRays; i++)
        {
            float angulo = i * Mathf.PI * 2f / nRays;
            float x = Mathf.Cos(angulo) * radius;
            float z = Mathf.Sin(angulo) * radius;

            float lenght = Height;
            Vector3 offset = new Vector3(x, 0, z);
            Vector3 punto = new Vector3(lenght*rayVector.x, lenght*rayVector.y,lenght*rayVector.z)+center+offset;
            Gizmos.DrawSphere(punto, 0.1f);

            Gizmos.DrawRay(center+offset,Height* rayVector );
        }
    }
    void DrawPoint()
    {
        Gizmos.color = Color.green;
        Vector3 origin = transform.position;
        Vector3 direction = rayVector;
        float length = hit.distance;
        Vector3 endPoint = origin + direction * length;
        Gizmos.DrawRay(origin, direction * length);
        Gizmos.DrawSphere(endPoint, 0.3f);
    }
    void OnDrawGizmos()
    {
      
        
        if (useMultipleRays)
            DrawCirclePoints();
        else DrawPoint();
    }
#endif
}
