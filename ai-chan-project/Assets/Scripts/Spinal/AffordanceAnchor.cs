using System;
using UnityEngine;

namespace AiChan.Spinal
{
    public enum AffordanceType
    {
        HighFive,
        HandHold,
        Railing,
        Touch,
        Custom
    }

    /// <summary>
    /// Level 1 Affordance target component.
    /// Can be placed on objects, environment surfaces, or player avatar limbs (e.g., High-Five, Hand-Hold, Railing).
    /// Provides task-space target position, approach orientation hints, and affordance semantics for LimbTargetReacher.
    /// </summary>
    public class AffordanceAnchor : MonoBehaviour
    {
        [Header("Affordance Semantics")]
        [Tooltip("Type of physical affordance.")]
        public AffordanceType affordanceType = AffordanceType.Touch;

        [Tooltip("Preferred humanoid hand for reaching this anchor.")]
        public HumanBodyBones preferredHand = HumanBodyBones.RightHand;

        [Header("Anchor Transform & Offsets")]
        [Tooltip("Transform used as anchor origin. If null, uses this GameObject.")]
        public Transform customAnchorTransform;

        [Tooltip("Local position offset relative to anchor transform.")]
        public Vector3 localOffset = Vector3.zero;

        [Tooltip("Approach normal direction in anchor local space.")]
        public Vector3 localApproachDirection = Vector3.up;

        [Header("Reach Configuration")]
        [Tooltip("Max reach speed multiplier (default 1.0 = reaches within 0.6s).")]
        [Range(0.2f, 3.0f)]
        public float reachSpeedMultiplier = 1.0f;

        [Tooltip("Optional custom elbow pole vector / hint direction.")]
        public Vector3 customPoleHint = Vector3.zero;

        [Header("State")]
        public bool isEnabled = true;
        public bool isOccupied = false;
        public LimbTargetReacher currentReacher;

        public Transform TargetTransform => customAnchorTransform != null ? customAnchorTransform : transform;

        public void Occupy(LimbTargetReacher reacher)
        {
            currentReacher = reacher;
            isOccupied = true;
        }

        public void Release()
        {
            currentReacher = null;
            isOccupied = false;
        }

        public Vector3 WorldPosition
        {
            get
            {
                var t = TargetTransform;
                return t != null ? t.TransformPoint(localOffset) : transform.position;
            }
        }

        public Vector3 WorldApproachDirection
        {
            get
            {
                var t = TargetTransform;
                return t != null ? t.TransformDirection(localApproachDirection).normalized : Vector3.up;
            }
        }

        private void OnDrawGizmos()
        {
            if (!isEnabled) return;

            Vector3 pos = WorldPosition;
            switch (affordanceType)
            {
                case AffordanceType.HighFive:
                    Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.8f);
                    break;
                case AffordanceType.HandHold:
                    Gizmos.color = new Color(1.0f, 0.5f, 0.1f, 0.8f);
                    break;
                case AffordanceType.Railing:
                    Gizmos.color = new Color(0.2f, 0.6f, 1.0f, 0.8f);
                    break;
                default:
                    Gizmos.color = new Color(0.9f, 0.2f, 0.7f, 0.8f);
                    break;
            }

            Gizmos.DrawWireSphere(pos, 0.06f);
            Gizmos.DrawLine(pos, pos + WorldApproachDirection * 0.12f);
        }
    }
}
