using System;
using System.Collections.Generic;
using UnityEngine;

namespace AiChan.Spinal
{
    /// <summary>
    /// Level 1 Affordance-based Task-Space Hand Reaching Controller.
    /// Drives humanoid arms (shoulder and elbow) using physical ConfigurableJoint motors
    /// driven by Two-Bone Analytical IK instead of hardcoded animations.
    /// 
    /// Features:
    /// - Two-Bone IK with shoulder yaw/pitch and elbow hinge physics.
    /// - Realistic motor spring (~800) and damping (~80) on shoulder and elbow.
    /// - Smooth task-space trajectory reaching anchor within 0.6 seconds.
    /// - Balance-compatible: exerts real PhysX reaction torques on grounded active ragdoll spine/legs without kinematically locking torso/hips.
    /// - Protective fall reflex: disengages active reaching and restores relaxed balance pose when torso pitch/roll > 45°.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(ActiveRagdollController))]
    public class LimbTargetReacher : MonoBehaviour
    {
        public enum TargetMode
        {
            None,
            Anchor,
            Transform,
            StaticPosition
        }

        [System.Serializable]
        public class ArmReachSession
        {
            public HumanBodyBones handBone = HumanBodyBones.RightHand;
            public HumanBodyBones upperArmBone = HumanBodyBones.RightUpperArm;
            public HumanBodyBones lowerArmBone = HumanBodyBones.RightLowerArm;

            [Header("Bones")]
            public Transform upperTransform;
            public Transform lowerTransform;
            public Transform handTransform;

            [Header("Targets & State")]
            public TargetMode targetMode = TargetMode.None;
            public AffordanceAnchor currentAnchor;
            public Transform customTargetTransform;
            public Vector3 customWorldTarget;
            public Vector3 customPoleHint;
            public bool isReaching = false;
            public bool isReturning = false;

            [Header("Timers")]
            public float reachTimer = 0f;
            public float reachDuration = 0.6f;
            public float returnTimer = 0f;
            public float returnDuration = 0.5f;

            [Header("Kinematics State")]
            public Vector3 startHandPos;
            public float upperArmLength = 0.22f;
            public float lowerArmLength = 0.20f;
            public Vector3 upperBoneLocal = Vector3.right;
            public Vector3 lowerBoneLocal = Vector3.right;
            public Quaternion relaxedUpperLocalRot = Quaternion.identity;
            public Quaternion relaxedLowerLocalRot = Quaternion.identity;
            public Quaternion reachStartUpperLocalRot = Quaternion.identity;
            public Quaternion reachStartLowerLocalRot = Quaternion.identity;
            public Quaternion currentUpperTargetLocalRot = Quaternion.identity;
            public Quaternion currentLowerTargetLocalRot = Quaternion.identity;

            public Vector3 DefaultPoleHint => handBone == HumanBodyBones.RightHand
                ? new Vector3(0.35f, -0.6f, -0.1f)
                : new Vector3(-0.35f, -0.6f, -0.1f);
        }

        [Header("Motor Physics (ConfigurableJoint SlerpDrive)")]
        [Tooltip("Active reach motor spring (default ~800).")]
        [SerializeField, Range(200f, 2000f)] private float _reachSpring = 800f;

        [Tooltip("Active reach motor damper (default ~80).")]
        [SerializeField, Range(20f, 200f)] private float _reachDamper = 80f;

        [Tooltip("Nominal duration to reach anchor in seconds (default 0.6s).")]
        [SerializeField, Range(0.2f, 2.0f)] private float _nominalReachDuration = 0.6f;

        [Header("Protective Fall Reflex")]
        [Tooltip("Torso pitch/roll angle (degrees) above which active reaching disengages.")]
        [SerializeField, Range(25f, 60f)] private float _fallReflexTiltThreshold = 45.0f;

        [Tooltip("Torso tilt angle below which fall reflex resets.")]
        [SerializeField, Range(15f, 35f)] private float _fallReflexRecoverThreshold = 30.0f;

        [SerializeField] private bool _isFallReflexActive = false;
        [SerializeField] private float _currentTorsoTiltAngle = 0f;

        [Header("Arm Sessions")]
        [SerializeField] private ArmReachSession _rightArm = new ArmReachSession
        {
            handBone = HumanBodyBones.RightHand,
            upperArmBone = HumanBodyBones.RightUpperArm,
            lowerArmBone = HumanBodyBones.RightLowerArm
        };

        [SerializeField] private ArmReachSession _leftArm = new ArmReachSession
        {
            handBone = HumanBodyBones.LeftHand,
            upperArmBone = HumanBodyBones.LeftUpperArm,
            lowerArmBone = HumanBodyBones.LeftLowerArm
        };

        [Header("References")]
        [SerializeField] private ActiveRagdollController _ragdoll;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _chestTransform;
        [SerializeField] private Transform _hipsTransform;

        private bool _isInitialized = false;

        public bool IsRightArmReaching => _rightArm.isReaching;
        public bool IsLeftArmReaching => _leftArm.isReaching;
        public bool IsFallReflexActive => _isFallReflexActive;
        public float CurrentTorsoTiltAngle => _currentTorsoTiltAngle;
        public ArmReachSession RightArm => _rightArm;
        public ArmReachSession LeftArm => _leftArm;
        public float ReachSpring => _reachSpring;
        public float ReachDamper => _reachDamper;

        public event Action<HumanBodyBones, AffordanceAnchor> OnReachStarted;
        public event Action<HumanBodyBones> OnReachCompleted;
        public event Action<HumanBodyBones> OnReachCleared;
        public event Action OnFallReflexTriggered;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_isInitialized) return;

            if (_ragdoll == null)
            {
                _ragdoll = GetComponent<ActiveRagdollController>() ?? GetComponentInParent<ActiveRagdollController>();
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
            }

            if (_animator != null && _animator.isHuman)
            {
                if (_chestTransform == null) _chestTransform = _animator.GetBoneTransform(HumanBodyBones.Chest);
                if (_chestTransform == null) _chestTransform = _animator.GetBoneTransform(HumanBodyBones.Spine);
                if (_hipsTransform == null) _hipsTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);

                InitArmSession(_rightArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
                InitArmSession(_leftArm, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            }

            _isInitialized = true;
        }

        private void InitArmSession(ArmReachSession session, HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone)
        {
            session.upperArmBone = upperBone;
            session.lowerArmBone = lowerBone;
            session.handBone = handBone;

            session.upperTransform = _animator.GetBoneTransform(upperBone);
            session.lowerTransform = _animator.GetBoneTransform(lowerBone);
            session.handTransform = _animator.GetBoneTransform(handBone);

            if (session.upperTransform != null && session.lowerTransform != null && session.handTransform != null)
            {
                session.upperArmLength = Mathf.Max(0.12f, Vector3.Distance(session.upperTransform.position, session.lowerTransform.position));
                session.lowerArmLength = Mathf.Max(0.12f, Vector3.Distance(session.lowerTransform.position, session.handTransform.position));

                session.upperBoneLocal = session.lowerTransform.localPosition.normalized;
                session.lowerBoneLocal = session.handTransform.localPosition.normalized;

                // Cache relaxed idle pose targets
                var upperMuscle = _ragdoll != null ? _ragdoll.Muscles.Find(m => m.bone == upperBone) : null;
                var lowerMuscle = _ragdoll != null ? _ragdoll.Muscles.Find(m => m.bone == lowerBone) : null;

                Quaternion initialUpper = upperMuscle != null && upperMuscle.initialLocalRotation != default
                    ? upperMuscle.initialLocalRotation
                    : session.upperTransform.localRotation;

                Quaternion initialLower = lowerMuscle != null && lowerMuscle.initialLocalRotation != default
                    ? lowerMuscle.initialLocalRotation
                    : session.lowerTransform.localRotation;

                if (handBone == HumanBodyBones.RightHand)
                {
                    session.relaxedUpperLocalRot = initialUpper * Quaternion.Euler(0f, 0f, -70f);
                    session.relaxedLowerLocalRot = initialLower * Quaternion.Euler(0f, -15f, 0f);
                }
                else
                {
                    session.relaxedUpperLocalRot = initialUpper * Quaternion.Euler(0f, 0f, 70f);
                    session.relaxedLowerLocalRot = initialLower * Quaternion.Euler(0f, 15f, 0f);
                }

                // Ensure ConfigurableJoint limits allow natural forward elbow hinge bending
                var lowerJoint = session.lowerTransform.GetComponent<ConfigurableJoint>();
                if (lowerJoint != null)
                {
                    lowerJoint.lowAngularXLimit = new SoftJointLimit { limit = -135f };
                    lowerJoint.highAngularXLimit = new SoftJointLimit { limit = 5f };
                }

                session.currentUpperTargetLocalRot = session.relaxedUpperLocalRot;
                session.currentLowerTargetLocalRot = session.relaxedLowerLocalRot;
            }
        }

        public void Step(float dt)
        {
            EnsureInitialized();
            CheckTorsoBalance();
            UpdateArmSession(_rightArm, dt);
            UpdateArmSession(_leftArm, dt);
        }

        private void FixedUpdate()
        {
            Step(Time.fixedDeltaTime);
        }

        private void CheckTorsoBalance()
        {
            Vector3 torsoUp = Vector3.up;
            if (_chestTransform != null && _hipsTransform != null)
            {
                torsoUp = (_chestTransform.position - _hipsTransform.position).normalized;
            }
            else if (_chestTransform != null)
            {
                torsoUp = _chestTransform.up;
            }
            else
            {
                torsoUp = transform.up;
            }

            _currentTorsoTiltAngle = Vector3.Angle(Vector3.up, torsoUp);

            if (_currentTorsoTiltAngle > _fallReflexTiltThreshold)
            {
                if (!_isFallReflexActive)
                {
                    _isFallReflexActive = true;
                    OnFallReflexTriggered?.Invoke();
                    DisengageAllReaches(immediate: false);
                }
                else if (_currentTorsoTiltAngle > 55.0f && (_rightArm.isReaching || _rightArm.isReturning || _leftArm.isReaching || _leftArm.isReturning))
                {
                    DisengageAllReaches(immediate: true);
                }
            }
            else if (_currentTorsoTiltAngle < _fallReflexRecoverThreshold)
            {
                _isFallReflexActive = false;
            }
        }

        private void UpdateArmSession(ArmReachSession session, float dt)
        {
            if (session.upperTransform == null || session.lowerTransform == null || session.handTransform == null) return;

            Transform chestT = _chestTransform != null ? _chestTransform : transform;

            if (session.isReaching)
            {
                // Disengage if target anchor was disabled, destroyed, or inactive
                if (session.targetMode == TargetMode.Anchor)
                {
                    if (session.currentAnchor == null || !session.currentAnchor.isEnabled || !session.currentAnchor.enabled || !session.currentAnchor.gameObject.activeInHierarchy)
                    {
                        ClearReach(session.handBone);
                        return;
                    }
                }
                else if (session.targetMode == TargetMode.Transform)
                {
                    if (session.customTargetTransform == null || !session.customTargetTransform.gameObject.activeInHierarchy)
                    {
                        ClearReach(session.handBone);
                        return;
                    }
                }

                // Determine target position
                Vector3 targetWorld = session.currentAnchor != null
                    ? session.currentAnchor.WorldPosition
                    : (session.customTargetTransform != null ? session.customTargetTransform.position : session.customWorldTarget);

                // If target moved outside reachable workspace / behind avatar coronal plane, disengage safely
                if (!IsInReachWorkspace(session.handBone, targetWorld))
                {
                    ClearReach(session.handBone);
                    return;
                }

                // Advance reach timer
                session.reachTimer += dt;
                float progress = Mathf.Clamp01(session.reachTimer / Mathf.Max(0.01f, session.reachDuration));
                float smoothT = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 ikTarget = Vector3.Lerp(session.startHandPos, targetWorld, smoothT);

                if (progress >= 1f && (session.reachTimer - dt < session.reachDuration))
                {
                    OnReachCompleted?.Invoke(session.handBone);
                }

                // Determine pole hint vector
                Vector3 poleTargetWorld = GetPoleTargetWorld(session);

                // Solve Two-Bone IK
                var upperMuscle = _ragdoll != null ? _ragdoll.Muscles.Find(m => m.bone == session.upperArmBone) : null;
                var lowerMuscle = _ragdoll != null ? _ragdoll.Muscles.Find(m => m.bone == session.lowerArmBone) : null;

                Quaternion initialUpper = upperMuscle != null && upperMuscle.initialLocalRotation != default ? upperMuscle.initialLocalRotation : session.upperTransform.localRotation;
                Quaternion initialLower = lowerMuscle != null && lowerMuscle.initialLocalRotation != default ? lowerMuscle.initialLocalRotation : session.lowerTransform.localRotation;

                Transform parentT = session.upperTransform.parent != null ? session.upperTransform.parent : chestT;
                if (SolveTwoBoneIK(session.upperTransform, session.lowerTransform, session.handTransform, parentT,
                                  ikTarget, poleTargetWorld, session.upperArmLength, session.lowerArmLength,
                                  initialUpper, initialLower, session.upperBoneLocal, session.lowerBoneLocal,
                                  session.handBone,
                                  out Quaternion targetUpper, out Quaternion targetLower))
                {
                    session.currentUpperTargetLocalRot = targetUpper;
                    session.currentLowerTargetLocalRot = targetLower;

                    if (_ragdoll != null)
                    {
                        _ragdoll.SetMuscleTarget(session.upperArmBone, targetUpper);
                        _ragdoll.SetMuscleTarget(session.lowerArmBone, targetLower);
                    }
                }
            }
            else if (session.isReturning)
            {
                session.returnTimer += dt;
                float progress = Mathf.Clamp01(session.returnTimer / Mathf.Max(0.01f, session.returnDuration));
                float smoothT = Mathf.SmoothStep(0f, 1f, progress);

                Quaternion upper = Quaternion.Slerp(session.reachStartUpperLocalRot, session.relaxedUpperLocalRot, smoothT);
                Quaternion lower = Quaternion.Slerp(session.reachStartLowerLocalRot, session.relaxedLowerLocalRot, smoothT);

                session.currentUpperTargetLocalRot = upper;
                session.currentLowerTargetLocalRot = lower;

                if (_ragdoll != null)
                {
                    _ragdoll.SetMuscleTarget(session.upperArmBone, upper);
                    _ragdoll.SetMuscleTarget(session.lowerArmBone, lower);
                }

                if (progress >= 1f)
                {
                    session.isReturning = false;
                    RestoreJointDrive(session.upperArmBone);
                    RestoreJointDrive(session.lowerArmBone);
                }
            }
        }

        private Vector3 GetPoleTargetWorld(ArmReachSession session)
        {
            Vector3 poleOffsetLocal;
            if (session.currentAnchor != null && session.currentAnchor.customPoleHint != Vector3.zero)
            {
                poleOffsetLocal = session.currentAnchor.customPoleHint;
                poleOffsetLocal.x = session.handBone == HumanBodyBones.RightHand ? Mathf.Abs(poleOffsetLocal.x) : -Mathf.Abs(poleOffsetLocal.x);
            }
            else if (session.customPoleHint != Vector3.zero)
            {
                poleOffsetLocal = session.customPoleHint;
                poleOffsetLocal.x = session.handBone == HumanBodyBones.RightHand ? Mathf.Abs(poleOffsetLocal.x) : -Mathf.Abs(poleOffsetLocal.x);
            }
            else
            {
                poleOffsetLocal = session.DefaultPoleHint;
            }
            return session.upperTransform.position + transform.TransformDirection(poleOffsetLocal);
        }

        /// <summary>
        /// Analytical Two-Bone IK solver tailored for Humanoid ConfigurableJoints.
        /// Computes exact target local rotations for upper and lower arm bones without locking spine or balance joints.
        /// </summary>
        public static bool SolveTwoBoneIK(
            Transform upper, Transform lower, Transform hand, Transform chest,
            Vector3 targetWorld, Vector3 poleTargetWorld,
            float L1, float L2,
            Quaternion initialUpperLocal, Quaternion initialLowerLocal,
            Vector3 upperBoneLocal, Vector3 lowerBoneLocal,
            HumanBodyBones handBone,
            out Quaternion targetUpperLocal, out Quaternion targetLowerLocal)
        {
            targetUpperLocal = initialUpperLocal;
            targetLowerLocal = initialLowerLocal;

            if (upper == null || lower == null || hand == null || chest == null) return false;

            Vector3 P0 = upper.position;
            Vector3 Dvec = targetWorld - P0;
            float d = Dvec.magnitude;

            // Clamp reach distance to avoid mechanical hyperextension or folding
            float dClamped = Mathf.Clamp(d, Mathf.Abs(L1 - L2) + 0.01f, L1 + L2 - 0.005f);
            Vector3 uTarget = d > 0.0001f ? Dvec / d : upper.forward;

            // Law of Cosines for shoulder angle
            float cosAlpha = (L1 * L1 + dClamped * dClamped - L2 * L2) / (2f * L1 * dClamped);
            cosAlpha = Mathf.Clamp(cosAlpha, -1f, 1f);
            float sinAlpha = Mathf.Sqrt(Mathf.Max(0f, 1f - cosAlpha * cosAlpha));

            // Elbow bend plane normal
            Vector3 pVec = poleTargetWorld - P0;
            Vector3 nBend = Vector3.Cross(uTarget, pVec);
            if (nBend.sqrMagnitude < 0.0001f) nBend = Vector3.Cross(uTarget, chest.forward);
            if (nBend.sqrMagnitude < 0.0001f) nBend = Vector3.Cross(uTarget, chest.up);
            nBend = nBend.normalized;

            Vector3 vBend = Vector3.Cross(nBend, uTarget).normalized;

            // Desired 3D positions of elbow and hand
            Vector3 P1Star = P0 + uTarget * (L1 * cosAlpha) + vBend * (L1 * sinAlpha);
            Vector3 P2Star = P0 + uTarget * dClamped;

            Vector3 VUpperStar = (P1Star - P0).normalized;
            Vector3 VLowerStar = (P2Star - P1Star).normalized;

            // Compute Base world orientation from reference T-pose
            Transform parent = upper.parent != null ? upper.parent : chest;
            Quaternion QParent = parent.rotation;
            Quaternion QUpperBase = QParent * initialUpperLocal;
            Vector3 VUpperBase = (QUpperBase * upperBoneLocal).normalized;

            Quaternion QLowerBase = QUpperBase * initialLowerLocal;
            Vector3 VLowerBase = (QLowerBase * lowerBoneLocal).normalized;

            Vector3 nBase = Vector3.Cross(VUpperBase, VLowerBase);
            if (nBase.sqrMagnitude < 0.0001f)
            {
                Vector3 defaultHinge = handBone == HumanBodyBones.RightHand ? Vector3.up : -Vector3.up;
                nBase = QLowerBase * defaultHinge;
            }
            nBase = nBase.normalized;

            // Rotate Upper Arm
            Quaternion R1 = Quaternion.FromToRotation(VUpperBase, VUpperStar);
            Vector3 n1 = R1 * nBase;

            Vector3 n1Proj = Vector3.ProjectOnPlane(n1, VUpperStar).normalized;
            Vector3 nBendProj = Vector3.ProjectOnPlane(nBend, VUpperStar).normalized;

            Quaternion R2 = Quaternion.identity;
            if (n1Proj.sqrMagnitude > 0.001f && nBendProj.sqrMagnitude > 0.001f)
            {
                R2 = Quaternion.FromToRotation(n1Proj, nBendProj);
            }

            Quaternion QUpperStar = R2 * R1 * QUpperBase;
            targetUpperLocal = Quaternion.Inverse(QParent) * QUpperStar;

            // Rotate Lower Arm
            Quaternion QLowerTemp = QUpperStar * initialLowerLocal;
            Vector3 VLowerTemp = (QLowerTemp * lowerBoneLocal).normalized;

            Quaternion RB = Quaternion.FromToRotation(VLowerTemp, VLowerStar);
            Quaternion QLowerStar = RB * QLowerTemp;
            targetLowerLocal = Quaternion.Inverse(QUpperStar) * QLowerStar;

            return true;
        }

        #region Public Reaching API

        /// <summary>
        /// Checks whether a target world position is within the reachable forward/lateral workspace of an arm.
        /// Rejects targets located behind the coronal plane of the body (behind back) to prevent wrenching the ragdoll spine.
        /// </summary>
        public bool IsInReachWorkspace(HumanBodyBones hand, Vector3 targetWorld)
        {
            EnsureInitialized();
            var session = hand == HumanBodyBones.LeftHand ? _leftArm : _rightArm;
            if (session == null || session.upperTransform == null) return false;

            Vector3 shoulderPos = session.upperTransform.position;
            Vector3 toTarget = targetWorld - shoulderPos;
            if (toTarget.sqrMagnitude < 0.0001f) return true;

            // Disallow targets behind the torso coronal plane
            float forwardDot = Vector3.Dot(toTarget.normalized, transform.forward);
            if (forwardDot < -0.15f)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reaches for the specified affordance anchor using designated hand within 0.6 seconds.
        /// </summary>
        public bool ReachTarget(HumanBodyBones hand, AffordanceAnchor anchor)
        {
            if (anchor == null || !anchor.isEnabled || !anchor.enabled || !anchor.gameObject.activeInHierarchy) return false;
            if (_isFallReflexActive) return false;
            if (!IsInReachWorkspace(hand, anchor.WorldPosition)) return false;

            EnsureInitialized();
            var session = hand == HumanBodyBones.LeftHand ? _leftArm : _rightArm;
            if (session.upperTransform == null) return false;

            // If already actively reaching this exact anchor, do not reset progress
            if (session.isReaching && session.currentAnchor == anchor)
            {
                return true;
            }

            // Release previous anchor if switching
            if (session.currentAnchor != null && session.currentAnchor != anchor)
            {
                session.currentAnchor.Release();
            }

            // Occupy anchor
            anchor.Occupy(this);
            session.targetMode = TargetMode.Anchor;
            session.currentAnchor = anchor;
            session.customTargetTransform = null;

            StartReachSession(session, anchor.WorldPosition, _nominalReachDuration / Mathf.Max(0.2f, anchor.reachSpeedMultiplier));
            OnReachStarted?.Invoke(hand, anchor);
            return true;
        }

        /// <summary>
        /// Overload: reaches anchor using anchor's preferred hand.
        /// </summary>
        public bool ReachTarget(AffordanceAnchor anchor)
        {
            if (anchor == null) return false;
            return ReachTarget(anchor.preferredHand, anchor);
        }

        /// <summary>
        /// Reaches towards a custom Transform target.
        /// </summary>
        public bool ReachTarget(HumanBodyBones hand, Transform targetTransform, float duration = -1f)
        {
            if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy || _isFallReflexActive) return false;
            if (!IsInReachWorkspace(hand, targetTransform.position)) return false;

            EnsureInitialized();
            var session = hand == HumanBodyBones.LeftHand ? _leftArm : _rightArm;
            if (session.upperTransform == null) return false;

            // If already reaching towards this exact transform, continue tracking without timer reset
            if (session.isReaching && session.customTargetTransform == targetTransform)
            {
                return true;
            }

            if (session.currentAnchor != null)
            {
                session.currentAnchor.Release();
                session.currentAnchor = null;
            }

            float d = duration > 0f ? duration : _nominalReachDuration;
            session.targetMode = TargetMode.Transform;
            session.customTargetTransform = targetTransform;
            StartReachSession(session, targetTransform.position, d);
            OnReachStarted?.Invoke(hand, null);
            return true;
        }

        /// <summary>
        /// Reaches towards a static world position.
        /// </summary>
        public bool ReachTarget(HumanBodyBones hand, Vector3 worldPosition, Vector3 poleHint = default, float duration = -1f)
        {
            if (_isFallReflexActive) return false;
            if (!IsInReachWorkspace(hand, worldPosition)) return false;

            EnsureInitialized();
            var session = hand == HumanBodyBones.LeftHand ? _leftArm : _rightArm;
            if (session.upperTransform == null) return false;

            // If already reaching towards essentially the same target position, don't restart
            if (session.isReaching && session.currentAnchor == null && session.customTargetTransform == null)
            {
                if (Vector3.Distance(session.customWorldTarget, worldPosition) < 0.02f)
                {
                    return true;
                }
            }

            if (session.currentAnchor != null)
            {
                session.currentAnchor.Release();
                session.currentAnchor = null;
            }

            float d = duration > 0f ? duration : _nominalReachDuration;
            session.targetMode = TargetMode.StaticPosition;
            session.customTargetTransform = null;
            session.customWorldTarget = worldPosition;
            session.customPoleHint = poleHint;
            StartReachSession(session, worldPosition, d);
            OnReachStarted?.Invoke(hand, null);
            return true;
        }

        private void StartReachSession(ArmReachSession session, Vector3 targetWorld, float duration)
        {
            Vector3 currentHandPos = session.handTransform != null ? session.handTransform.position : transform.position;

            // If already reaching, dynamically adapt duration to remaining distance so retargeting is responsive and doesn't freeze
            if (session.isReaching)
            {
                float targetDist = Vector3.Distance(currentHandPos, targetWorld);
                float armLength = session.upperArmLength + session.lowerArmLength;
                float nominalSpeed = armLength / Mathf.Max(0.1f, _nominalReachDuration);
                session.reachDuration = Mathf.Clamp(targetDist / Mathf.Max(0.1f, nominalSpeed), 0.15f, duration);
            }
            else
            {
                session.reachDuration = Mathf.Max(0.1f, duration);
            }

            session.startHandPos = currentHandPos;
            session.reachTimer = 0f;
            session.isReaching = true;
            session.isReturning = false;

            // Apply realistic motor spring & damping (~800, ~80)
            SetJointDrive(session.upperArmBone, _reachSpring, _reachDamper);
            SetJointDrive(session.lowerArmBone, _reachSpring, _reachDamper);
        }

        /// <summary>
        /// Clears active reaching on hand and smoothly returns arm to relaxed balance pose.
        /// </summary>
        public void ClearReach(HumanBodyBones hand)
        {
            var session = hand == HumanBodyBones.LeftHand ? _leftArm : _rightArm;
            if (session == null || (!session.isReaching && !session.isReturning)) return;

            if (session.currentAnchor != null)
            {
                session.currentAnchor.Release();
                session.currentAnchor = null;
            }
            session.customTargetTransform = null;
            session.targetMode = TargetMode.None;

            if (session.isReaching)
            {
                session.reachStartUpperLocalRot = session.currentUpperTargetLocalRot;
                session.reachStartLowerLocalRot = session.currentLowerTargetLocalRot;
                session.returnTimer = 0f;
                session.returnDuration = 0.45f;
                session.isReturning = true;
            }

            session.isReaching = false;
            OnReachCleared?.Invoke(hand);
        }

        /// <summary>
        /// Clears all reaching arms.
        /// </summary>
        public void ClearAllReaches()
        {
            ClearReach(HumanBodyBones.RightHand);
            ClearReach(HumanBodyBones.LeftHand);
        }

        /// <summary>
        /// Disengages active reaches on both arms (used by protective fall reflex or emergency balance).
        /// </summary>
        public void DisengageAllReaches(bool immediate = false)
        {
            DisengageArm(_rightArm, immediate);
            DisengageArm(_leftArm, immediate);
        }

        private void DisengageArm(ArmReachSession session, bool immediate)
        {
            if (session == null) return;

            if (session.currentAnchor != null)
            {
                session.currentAnchor.Release();
                session.currentAnchor = null;
            }
            session.customTargetTransform = null;
            session.targetMode = TargetMode.None;

            if (immediate)
            {
                session.isReaching = false;
                session.isReturning = false;
                session.currentUpperTargetLocalRot = session.relaxedUpperLocalRot;
                session.currentLowerTargetLocalRot = session.relaxedLowerLocalRot;

                if (_ragdoll != null)
                {
                    _ragdoll.SetMuscleTarget(session.upperArmBone, session.relaxedUpperLocalRot);
                    _ragdoll.SetMuscleTarget(session.lowerArmBone, session.relaxedLowerLocalRot);
                }
                RestoreJointDrive(session.upperArmBone);
                RestoreJointDrive(session.lowerArmBone);
            }
            else if (session.isReaching)
            {
                session.reachStartUpperLocalRot = session.currentUpperTargetLocalRot;
                session.reachStartLowerLocalRot = session.currentLowerTargetLocalRot;
                session.returnTimer = 0f;
                session.returnDuration = 0.35f;
                session.isReturning = true;
                session.isReaching = false;
            }

            OnReachCleared?.Invoke(session.handBone);
        }

        #endregion

        #region Joint Drive Helpers

        private void SetJointDrive(HumanBodyBones bone, float spring, float damper)
        {
            if (_ragdoll == null) return;
            var muscle = _ragdoll.Muscles.Find(m => m.bone == bone);
            if (muscle != null)
            {
                muscle.spring = spring;
                muscle.damper = damper;
                if (muscle.joint != null)
                {
                    muscle.joint.slerpDrive = new JointDrive
                    {
                        positionSpring = spring,
                        positionDamper = damper,
                        maximumForce = float.MaxValue
                    };
                }
            }
        }

        private void RestoreJointDrive(HumanBodyBones bone)
        {
            if (_ragdoll == null) return;
            float defaultSpring = _ragdoll.globalSpring > 0f ? _ragdoll.globalSpring : 1000f;
            float defaultDamper = _ragdoll.globalDamper > 0f ? _ragdoll.globalDamper : 100f;
            SetJointDrive(bone, defaultSpring, defaultDamper);
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            DrawArmGizmos(_rightArm);
            DrawArmGizmos(_leftArm);
        }

        private void DrawArmGizmos(ArmReachSession session)
        {
            if (session == null || session.upperTransform == null) return;

            Gizmos.color = session.isReaching ? Color.green : (session.isReturning ? Color.yellow : Color.gray);
            Vector3 p0 = session.upperTransform.position;
            Vector3 p1 = session.lowerTransform != null ? session.lowerTransform.position : p0;
            Vector3 p2 = session.handTransform != null ? session.handTransform.position : p1;

            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawWireSphere(p2, 0.04f);

            if (session.isReaching)
            {
                Vector3 target = session.currentAnchor != null ? session.currentAnchor.WorldPosition : session.customWorldTarget;
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(target, 0.05f);
                Gizmos.DrawLine(p2, target);
            }
        }
    }
}
