using UnityEngine;
namespace MLG.DelayedActionsTool.Samples
{
    [RequireComponent(typeof(Rigidbody))]
    public class DARecieveOrdersSampleCode : MonoBehaviour
    {
        Rigidbody rb;
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }
        public void MoveRight()
        {
            rb.linearVelocity = Vector3.right * 0.8f;
        }
        public void Stop()
        {
            rb.linearVelocity = Vector3.zero;
        }
        public void MoveLeft()
        {
            rb.linearVelocity = Vector3.left * 0.8f;
        }
 
    }
}