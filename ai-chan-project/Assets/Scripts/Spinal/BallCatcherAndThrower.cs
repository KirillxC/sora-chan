using System;
using UnityEngine;
using AiChan.Core;

namespace AiChan.Spinal
{
    /// <summary>
    /// Coordinates ball tracking, trajectory prediction, procedural look-at,
    /// and two-handed IK intercept reach to catch incoming balls.
    /// Also handles user ball throwing for interactive testing.
    /// </summary>
    public class BallCatcherAndThrower : MonoBehaviour
    {
        [Header("References")]
        public BallisticPredictor predictor;
        public Rigidbody ballRb;
        public Animator animator;
        public Transform chestTransform;
        public Transform headTransform;

        [Header("Catch Reach Points")]
        public Transform leftHandTarget;
        public Transform rightHandTarget;

        [Header("State")]
        public bool isTrackingBall = false;
        public bool isHoldingBall = false;
        public float ballCaughtDistance = 0.25f;

        [Header("IK Weights")]
        [Range(0f, 1f)] public float lookAtWeight = 0.8f;
        [Range(0f, 1f)] public float handsWeight = 0f;

        private Vector3 _currentInterceptTarget;
        private float _timeRemaining;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (predictor == null) predictor = GetComponent<BallisticPredictor>();
            if (predictor == null) predictor = gameObject.AddComponent<BallisticPredictor>();

            if (animator != null && animator.isHuman)
            {
                chestTransform = animator.GetBoneTransform(HumanBodyBones.Chest);
                headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            if (predictor != null && chestTransform != null)
            {
                predictor.chestTransform = chestTransform;
            }
        }

        private void Update()
        {
            if (ballRb == null)
            {
                var ballGo = GameObject.Find("InteractiveBall");
                if (ballGo != null) ballRb = ballGo.GetComponent<Rigidbody>();
            }

            if (ballRb == null) return;

            // 1. If ball is held by hands, pin it between hands
            if (isHoldingBall)
            {
                Vector3 holdPos = chestTransform != null ? chestTransform.position + chestTransform.forward * 0.35f : transform.position + Vector3.up * 1.2f + transform.forward * 0.35f;
                ballRb.transform.position = holdPos;
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                handsWeight = Mathf.MoveTowards(handsWeight, 0.7f, Time.deltaTime * 3f);
                return;
            }

            // 2. Predict trajectory when ball is flying
            if (ballRb.linearVelocity.sqrMagnitude > 0.5f)
            {
                Vector3 g = Physics.gravity;
                Vector3 intercept;
                float timeToImpact;

                if (predictor.PredictTrajectory(ballRb.position, ballRb.linearVelocity, g, out intercept, out timeToImpact))
                {
                    isTrackingBall = true;
                    _currentInterceptTarget = intercept;
                    _timeRemaining = timeToImpact;

                    // Blend in hands reach IK as ball approaches
                    float urgency = Mathf.Clamp01(1f - (_timeRemaining / 1.5f));
                    handsWeight = Mathf.MoveTowards(handsWeight, urgency, Time.deltaTime * 6f);
                }
                else
                {
                    // Ball flying elsewhere
                    isTrackingBall = false;
                    handsWeight = Mathf.MoveTowards(handsWeight, 0f, Time.deltaTime * 2f);
                }
            }
            else
            {
                isTrackingBall = false;
                handsWeight = Mathf.MoveTowards(handsWeight, 0f, Time.deltaTime * 2f);
            }

            // 3. Check for catch distance
            if (!isHoldingBall && isTrackingBall)
            {
                Vector3 catchCenter = _currentInterceptTarget;
                float distToBall = Vector3.Distance(ballRb.position, catchCenter);
                if (distToBall <= ballCaughtDistance || (_timeRemaining <= 0.05f && distToBall <= 0.5f))
                {
                    CatchBall();
                }
            }
        }

        public void CatchBall()
        {
            isHoldingBall = true;
            isTrackingBall = false;
            ballRb.isKinematic = true;
            Debug.Log("[BallCatcher] Ball caught successfully!");
        }

        public void ThrowBallBack(Vector3 targetPosition, float flightDuration = 0.8f)
        {
            if (!isHoldingBall) return;
            isHoldingBall = false;
            ballRb.isKinematic = false;

            Vector3 v0 = BallisticPredictor.CalculateThrowVelocity(ballRb.position, targetPosition, flightDuration, Physics.gravity);
            ballRb.linearVelocity = v0;
            handsWeight = 0f;
            Debug.Log($"[BallCatcher] Threw ball back with velocity {v0}");
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            // 1. Look-at IK: follow the ball
            if (ballRb != null)
            {
                animator.SetLookAtWeight(lookAtWeight, 0.4f, 0.8f, 0.5f, 0.5f);
                animator.SetLookAtPosition(ballRb.position);
            }

            // 2. Hand reach IK: move hands toward intercept or ball hold position
            if (handsWeight > 0.01f)
            {
                Vector3 target = isHoldingBall && chestTransform != null 
                    ? chestTransform.position + chestTransform.forward * 0.35f 
                    : _currentInterceptTarget;

                Vector3 leftOffset = -transform.right * 0.12f;
                Vector3 rightOffset = transform.right * 0.12f;

                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handsWeight);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, target + leftOffset);

                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handsWeight);
                animator.SetIKPosition(AvatarIKGoal.RightHand, target + rightOffset);
            }
            else
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            }
        }
    }
}
