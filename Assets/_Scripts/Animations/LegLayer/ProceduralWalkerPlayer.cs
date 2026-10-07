using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace IK.ProceduralAnimations.Walking
{
    public class ProceduralWalkerPlayer : MonoBehaviour
    {

        public LayerMask IgnoreMask;
        [SerializeField]
        public bool IsMoving = false;
        [SerializeField]
        public bool IsGrounded = false;
        [SerializeField] Transform bodyReference;
        [SerializeField] Transform leftFootTarget;
        [SerializeField] Transform rightFootTarget;
        [SerializeField] AnimationCurve horizontalCurve;
        [SerializeField] AnimationCurve verticalCurve;
        [SerializeField] float speed = 0.5f;
        [SerializeField] float stepSize = .5f;
        [SerializeField] float stepHeight = 0.5f;
        [SerializeField] float raycastDistance = 2.0f;
        private Vector3 leftTargetOffset;
        private Vector3 rightTargetOffset;


        float moveSpeed = 0f;
        float restPoseTimer = 0f;
        float movingPoseTimer = 0f;

        void Start()
        {


            if (leftFootTarget != null)
                leftTargetOffset = leftFootTarget.localPosition;
            if (rightFootTarget != null)
                rightTargetOffset = rightFootTarget.localPosition;
            if (horizontalCurve != null)
            {
                horizontalCurve.postWrapMode = WrapMode.PingPong;
                horizontalCurve.preWrapMode = WrapMode.PingPong;
            }
            if (verticalCurve != null)
            {
                verticalCurve.postWrapMode = WrapMode.PingPong;
                verticalCurve.preWrapMode = WrapMode.PingPong;
            }

            if (leftFootTarget != null)
                leftTargetOffset = bodyReference.InverseTransformPoint(leftFootTarget.position);
            if (rightFootTarget != null)
                rightTargetOffset = bodyReference.InverseTransformPoint(rightFootTarget.position);

        }

        // Update is called once per frame
        void FixedUpdate()
        {

            if (IsMoving && IsGrounded)//MOVING
            {

                moveSpeed = speed;
                restPoseTimer = 0f;
                movingPoseTimer += Time.fixedDeltaTime;
                float timeVal = movingPoseTimer * moveSpeed;

                // --- Left Leg (Fase 0.0) ---
                UpdateLegPosition(
                      leftFootTarget,
                      leftTargetOffset,
                      timeVal
                      );

                // --- Right Leg (Different pase 0.5 = Half cycle) ---
                UpdateLegPosition(
                       rightFootTarget,
                       rightTargetOffset,
                       timeVal + 1f
                       );

            }

            else
            {

                movingPoseTimer = 0f;
                if (restPoseTimer < speed)
                {
                    // Volver suavemente al offset original
                    Vector3 leftRestWorldPos = bodyReference.TransformPoint(leftTargetOffset);
                    Vector3 rightRestWorldPos = bodyReference.TransformPoint(rightTargetOffset);

                    leftFootTarget.position = Vector3.Lerp(leftFootTarget.position, leftRestWorldPos, restPoseTimer / speed);
                    rightFootTarget.position = Vector3.Lerp(rightFootTarget.position, rightRestWorldPos, restPoseTimer / speed);

                    restPoseTimer += Time.fixedDeltaTime;
                }

                SnapToGround(leftFootTarget);
                SnapToGround(rightFootTarget);
            }
        }
        private void UpdateLegPosition(Transform footTarget, Vector3 baseOffset, float time)
        {
            // Normalizar tiempo dentro del ciclo de duracion 2.0 (0 a 1 = Swing, 1 a 2 = Stance)
            float normalizedTime = Mathf.Repeat(time, 2.0f);

            // 1. Posición horizontal centrada (-0.5 a +0.5)
            float horizontalEval = horizontalCurve.Evaluate(normalizedTime) - 0.5f;

            // 2. Elevación vertical: ÚNICAMENTE activa durante la fase de vuelo (0.0 a 1.0)
            float verticalLift = 0f;
            if (normalizedTime < 1.0f)
            {
                // Eleva el pie en el aire y garantiza llegar a 0 al aterrizar en 1.0
                verticalLift = Mathf.Max(0f, verticalCurve.Evaluate(normalizedTime));
            }
            else
            {
                // En fase de apoyo (1.0 a 2.0) el pie está 100% pegado al suelo
                verticalLift = 0f;
            }

            // 3. Offset local respecto al cuerpo
            Vector3 localStepOffset = baseOffset + Vector3.forward * (horizontalEval * stepSize);

            // 4. Transformar a coordenadas del mundo y proyectar al suelo con Raycast
            Vector3 targetWorldPos = bodyReference.TransformPoint(localStepOffset);
            Vector3 upDir = bodyReference.up;
            Vector3 rayOrigin = targetWorldPos + upDir * 1.0f;
            Vector3 groundPoint = targetWorldPos;

            if (Physics.Raycast(rayOrigin, -upDir, out RaycastHit hit, raycastDistance, ~IgnoreMask))
            {
                groundPoint = hit.point;
            }

            // 5. Posición final = Punto exacto del terreno + Altura de elevación
            footTarget.position = groundPoint + upDir * (verticalLift * stepHeight);
        }

        private void SnapToGround(Transform footTarget)
        {
            Vector3 upDir = bodyReference.up;
            Vector3 rayOrigin = footTarget.position + upDir * 0.5f;

            if (Physics.Raycast(rayOrigin, -upDir, out RaycastHit hit, raycastDistance, ~IgnoreMask))
            {
                footTarget.position = hit.point;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (leftFootTarget == null || rightFootTarget == null) return;


            if (Physics.Raycast(leftFootTarget.position, Vector3.down, out RaycastHit Lhit, Mathf.Infinity, ~IgnoreMask))
            {
                Gizmos.DrawSphere(Lhit.point, 0.5f);
            }
            if (Physics.Raycast(rightFootTarget.position, Vector3.down, out RaycastHit Rhit, Mathf.Infinity, ~IgnoreMask))
            {
                Gizmos.DrawSphere(Rhit.point, 0.5f);
            }
        }
#endif
    }
}
