using System;
using System.Collections.Generic;
using UnityEngine;

namespace AiChan.Spinal
{
    /// <summary>
    /// Active Ragdoll muscle controller.
    /// Drives ConfigurableJoints using angular spring drives (SlerpDrive)
    /// to hold poses, allow physical interaction, and spring back smoothly.
    /// Manages balance and collision-ignore pairs to ensure physical stability.
    /// Converts bone-space delta rotations to Joint Space coordinate frames for proper hinge limits.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ActiveRagdollController : MonoBehaviour
    {
        [System.Serializable]
        public class MuscleJoint
        {
            public HumanBodyBones bone;
            public Transform transform;
            public ConfigurableJoint joint;
            public Quaternion initialLocalRotation;
            public Quaternion targetLocalRotation;
            public Quaternion jointToBone;
            public Quaternion boneToJoint;
            public float spring = 1000f;
            public float damper = 100f;
        }

        [Header("Muscle Stiffness")]
        [Range(50f, 3000f)] public float globalSpring = 1000f;
        [Range(5f, 300f)] public float globalDamper = 100f;

        [Header("Pose & Balance")]
        public bool pinHips = true; // Pin hips to keep avatar standing naturally during interaction
        public bool naturalPoseOnStart = true;

        [Header("Registered Joints")]
        [SerializeField] private List<MuscleJoint> _muscles = new List<MuscleJoint>();

        private Animator _animator;
        private Rigidbody _hipsRb;

        public List<MuscleJoint> Muscles => _muscles;
        public Rigidbody HipsRigidbody => _hipsRb;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            SetupCollisionIgnores();
            InitJointBasisTransforms();
        }

        private void Start()
        {
            if (_hipsRb == null)
            {
                var hipsBone = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                if (hipsBone != null) _hipsRb = hipsBone.GetComponent<Rigidbody>();
            }

            if (_hipsRb != null)
            {
                _hipsRb.isKinematic = pinHips;
            }

            if (naturalPoseOnStart)
            {
                SetNaturalIdlePose();
            }
        }

        public void InitJointBasisTransforms()
        {
            foreach (var m in _muscles)
            {
                if (m.joint == null) continue;
                Vector3 right = m.joint.axis;
                Vector3 up = m.joint.secondaryAxis;
                Vector3 forward = Vector3.Cross(right, up);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    m.jointToBone = Quaternion.LookRotation(forward, up);
                    m.boneToJoint = Quaternion.Inverse(m.jointToBone);
                }
                else
                {
                    m.jointToBone = Quaternion.identity;
                    m.boneToJoint = Quaternion.identity;
                }
            }
        }

        public void SetupCollisionIgnores()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator == null) return;

            HumanBodyBones[][] adjacentPairs = new[]
            {
                // Spine & Torso
                new[] { HumanBodyBones.Hips, HumanBodyBones.Spine },
                new[] { HumanBodyBones.Spine, HumanBodyBones.Chest },
                new[] { HumanBodyBones.Chest, HumanBodyBones.Head },

                // Arms
                new[] { HumanBodyBones.Chest, HumanBodyBones.LeftUpperArm },
                new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm },
                new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },

                new[] { HumanBodyBones.Chest, HumanBodyBones.RightUpperArm },
                new[] { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
                new[] { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },

                // Legs
                new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg },
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg },
                new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },

                new[] { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg },
                new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
                new[] { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },

                // Inter-leg / thigh collision prevention
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg },
                new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg },
                new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot },

                // Torso & Upper Legs <-> Hands & Forearms (prevents arm clipping/springing against torso when dropped)
                new[] { HumanBodyBones.Hips, HumanBodyBones.LeftHand },
                new[] { HumanBodyBones.Hips, HumanBodyBones.RightHand },
                new[] { HumanBodyBones.Hips, HumanBodyBones.LeftLowerArm },
                new[] { HumanBodyBones.Hips, HumanBodyBones.RightLowerArm },
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftHand },
                new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightHand },
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerArm },
                new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerArm },
                new[] { HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm },
                new[] { HumanBodyBones.Spine, HumanBodyBones.RightUpperArm },
                new[] { HumanBodyBones.Spine, HumanBodyBones.LeftLowerArm },
                new[] { HumanBodyBones.Spine, HumanBodyBones.RightLowerArm }
            };

            foreach (var pair in adjacentPairs)
            {
                var t1 = _animator.GetBoneTransform(pair[0]);
                var t2 = _animator.GetBoneTransform(pair[1]);
                if (t1 == null || t2 == null) continue;

                var c1s = t1.GetComponents<Collider>();
                var c2s = t2.GetComponents<Collider>();
                foreach (var c1 in c1s)
                {
                    foreach (var c2 in c2s)
                    {
                        Physics.IgnoreCollision(c1, c2, true);
                    }
                }
            }
        }

        public void RegisterJoint(HumanBodyBones bone, Transform t, ConfigurableJoint joint)
        {
            Vector3 right = joint.axis;
            Vector3 up = joint.secondaryAxis;
            Vector3 forward = Vector3.Cross(right, up);
            Quaternion jToB = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, up) : Quaternion.identity;
            Quaternion bToJ = Quaternion.Inverse(jToB);

            var muscle = new MuscleJoint
            {
                bone = bone,
                transform = t,
                joint = joint,
                initialLocalRotation = t.localRotation,
                targetLocalRotation = t.localRotation,
                jointToBone = jToB,
                boneToJoint = bToJ,
                spring = globalSpring,
                damper = globalDamper
            };
            _muscles.Add(muscle);
        }

        public void ClearJoints()
        {
            _muscles.Clear();
        }

        public void SetHipsKinematic(bool kinematic)
        {
            pinHips = kinematic;
            if (_hipsRb == null && _animator != null)
            {
                var hipsBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hipsBone != null) _hipsRb = hipsBone.GetComponent<Rigidbody>();
            }

            if (_hipsRb != null)
            {
                _hipsRb.isKinematic = kinematic;
            }
        }

        /// <summary>
        /// Transitions avatar from T-pose to a cute, natural relaxed standing anime pose.
        /// </summary>
        public void SetNaturalIdlePose()
        {
            foreach (var m in _muscles)
            {
                switch (m.bone)
                {
                    case HumanBodyBones.LeftUpperArm:
                        // Lower left arm down to side, slightly forward
                        m.targetLocalRotation = m.initialLocalRotation * Quaternion.Euler(0f, 0f, 70f);
                        break;

                    case HumanBodyBones.RightUpperArm:
                        // Lower right arm down to side, slightly forward
                        m.targetLocalRotation = m.initialLocalRotation * Quaternion.Euler(0f, 0f, -70f);
                        break;

                    case HumanBodyBones.LeftLowerArm:
                        // Slight natural elbow bend inwards
                        m.targetLocalRotation = m.initialLocalRotation * Quaternion.Euler(0f, 15f, 0f);
                        break;

                    case HumanBodyBones.RightLowerArm:
                        // Slight natural elbow bend inwards
                        m.targetLocalRotation = m.initialLocalRotation * Quaternion.Euler(0f, -15f, 0f);
                        break;

                    case HumanBodyBones.Head:
                        // Natural head posture
                        m.targetLocalRotation = m.initialLocalRotation * Quaternion.Euler(2f, 0f, 0f);
                        break;

                    case HumanBodyBones.Spine:
                    case HumanBodyBones.Chest:
                        m.targetLocalRotation = m.initialLocalRotation;
                        break;

                    default:
                        m.targetLocalRotation = m.initialLocalRotation;
                        break;
                }
            }
            ApplySprings();
        }

        /// <summary>
        /// Resets pose back to reference T-Pose.
        /// </summary>
        public void SetTPose()
        {
            foreach (var m in _muscles)
            {
                m.targetLocalRotation = m.initialLocalRotation;
            }
            ApplySprings();
        }

        public void SetMuscleTarget(HumanBodyBones bone, Quaternion targetLocal)
        {
            var m = _muscles.Find(x => x.bone == bone);
            if (m != null)
            {
                m.targetLocalRotation = targetLocal;
            }
        }

        public void ApplySprings()
        {
            foreach (var m in _muscles)
            {
                if (m.joint == null) continue;

                var drive = new JointDrive
                {
                    positionSpring = globalSpring,
                    positionDamper = globalDamper,
                    maximumForce = float.MaxValue
                };
                m.joint.slerpDrive = drive;
            }
        }

        public void StepMuscles()
        {
            // Update joint targets converted to joint coordinate frame
            for (int i = 0; i < _muscles.Count; i++)
            {
                var m = _muscles[i];
                if (m.joint == null) continue;

                if (m.initialLocalRotation.x == 0 && m.initialLocalRotation.y == 0 && m.initialLocalRotation.z == 0 && m.initialLocalRotation.w == 0)
                {
                    m.initialLocalRotation = m.transform != null ? m.transform.localRotation : Quaternion.identity;
                }

                if (m.targetLocalRotation.x == 0 && m.targetLocalRotation.y == 0 && m.targetLocalRotation.z == 0 && m.targetLocalRotation.w == 0)
                {
                    m.targetLocalRotation = m.initialLocalRotation;
                }

                Quaternion deltaBone = Quaternion.Inverse(m.targetLocalRotation) * m.initialLocalRotation;

                if (m.jointToBone == default || (m.jointToBone.x == 0 && m.jointToBone.y == 0 && m.jointToBone.z == 0 && m.jointToBone.w == 0))
                {
                    Vector3 right = m.joint.axis;
                    Vector3 up = m.joint.secondaryAxis;
                    Vector3 forward = Vector3.Cross(right, up);
                    if (forward.sqrMagnitude > 0.0001f)
                    {
                        m.jointToBone = Quaternion.LookRotation(forward, up);
                        m.boneToJoint = Quaternion.Inverse(m.jointToBone);
                    }
                    else
                    {
                        m.jointToBone = Quaternion.identity;
                        m.boneToJoint = Quaternion.identity;
                    }
                }

                m.joint.targetRotation = m.boneToJoint * deltaBone * m.jointToBone;
            }
        }

        private void FixedUpdate()
        {
            StepMuscles();
        }
    }
}
