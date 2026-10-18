using System;
using System.Collections.Generic;
using UnityEngine;

namespace AiChan.Spinal
{
    /// <summary>
    /// Runtime Active Ragdoll auto-rigger.
    /// Operates at runtime (Play Mode) or in editor on any Humanoid / VRM model.
    /// Configures ConfigurableJoints, anatomical angular limits, muscle springs,
    /// rigidbodies, colliders, and collision ignore matrices without manual rigging.
    /// </summary>
    public static class RuntimeRagdollRigger
    {
        public struct BoneJointDef
        {
            public HumanBodyBones bone;
            public HumanBodyBones parentBone;
            public float mass;
            public float radius;
            public Vector3 primaryAxis;
            public Vector3 secondaryAxis;
            public float lowAngularXLimit;
            public float highAngularXLimit;
            public float angularYLimit;
            public float angularZLimit;
        }

        public static readonly BoneJointDef[] JointDefinitions = new[]
        {
            // Spine & Torso
            new BoneJointDef
            {
                bone = HumanBodyBones.Spine, parentBone = HumanBodyBones.Hips, mass = 10f, radius = 0.11f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -20f, highAngularXLimit = 25f, angularYLimit = 20f, angularZLimit = 20f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.Chest, parentBone = HumanBodyBones.Spine, mass = 12f, radius = 0.12f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -20f, highAngularXLimit = 25f, angularYLimit = 20f, angularZLimit = 20f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.Head, parentBone = HumanBodyBones.Chest, mass = 4f, radius = 0.11f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -30f, highAngularXLimit = 40f, angularYLimit = 45f, angularZLimit = 30f
            },

            // Left Arm
            new BoneJointDef
            {
                bone = HumanBodyBones.LeftUpperArm, parentBone = HumanBodyBones.Chest, mass = 2f, radius = 0.05f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -85f, highAngularXLimit = 85f, angularYLimit = 75f, angularZLimit = 75f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.LeftLowerArm, parentBone = HumanBodyBones.LeftUpperArm, mass = 1.5f, radius = 0.04f,
                primaryAxis = new Vector3(0, 1, 0), secondaryAxis = new Vector3(1, 0, 0),
                lowAngularXLimit = 0f, highAngularXLimit = 135f, angularYLimit = 5f, angularZLimit = 5f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.LeftHand, parentBone = HumanBodyBones.LeftLowerArm, mass = 0.6f, radius = 0.04f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -45f, highAngularXLimit = 45f, angularYLimit = 30f, angularZLimit = 45f
            },

            // Right Arm
            new BoneJointDef
            {
                bone = HumanBodyBones.RightUpperArm, parentBone = HumanBodyBones.Chest, mass = 2f, radius = 0.05f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -85f, highAngularXLimit = 85f, angularYLimit = 75f, angularZLimit = 75f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.RightLowerArm, parentBone = HumanBodyBones.RightUpperArm, mass = 1.5f, radius = 0.04f,
                primaryAxis = new Vector3(0, -1, 0), secondaryAxis = new Vector3(1, 0, 0),
                lowAngularXLimit = 0f, highAngularXLimit = 135f, angularYLimit = 5f, angularZLimit = 5f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.RightHand, parentBone = HumanBodyBones.RightLowerArm, mass = 0.6f, radius = 0.04f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -45f, highAngularXLimit = 45f, angularYLimit = 30f, angularZLimit = 45f
            },

            // Left Leg
            new BoneJointDef
            {
                bone = HumanBodyBones.LeftUpperLeg, parentBone = HumanBodyBones.Hips, mass = 6f, radius = 0.065f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -90f, highAngularXLimit = 20f, angularYLimit = 25f, angularZLimit = 30f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.LeftLowerLeg, parentBone = HumanBodyBones.LeftUpperLeg, mass = 4f, radius = 0.05f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = 0f, highAngularXLimit = 135f, angularYLimit = 5f, angularZLimit = 5f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.LeftFoot, parentBone = HumanBodyBones.LeftLowerLeg, mass = 1.2f, radius = 0.045f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -35f, highAngularXLimit = 35f, angularYLimit = 15f, angularZLimit = 20f
            },

            // Right Leg
            new BoneJointDef
            {
                bone = HumanBodyBones.RightUpperLeg, parentBone = HumanBodyBones.Hips, mass = 6f, radius = 0.065f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -90f, highAngularXLimit = 20f, angularYLimit = 25f, angularZLimit = 30f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.RightLowerLeg, parentBone = HumanBodyBones.RightUpperLeg, mass = 4f, radius = 0.05f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = 0f, highAngularXLimit = 135f, angularYLimit = 5f, angularZLimit = 5f
            },
            new BoneJointDef
            {
                bone = HumanBodyBones.RightFoot, parentBone = HumanBodyBones.RightLowerLeg, mass = 1.2f, radius = 0.045f,
                primaryAxis = new Vector3(1, 0, 0), secondaryAxis = new Vector3(0, 1, 0),
                lowAngularXLimit = -35f, highAngularXLimit = 35f, angularYLimit = 15f, angularZLimit = 20f
            }
        };

        public static bool RigActiveRagdoll(GameObject avatarGo, bool pinHips = true, float springPower = 1000f, float damping = 100f)
        {
            var animator = avatarGo.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("[RuntimeRagdollRigger] Model must have a Humanoid Animator!");
                return false;
            }

            // 1. Setup Hips
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null)
            {
                Debug.LogError("[RuntimeRagdollRigger] Missing Hips bone!");
                return false;
            }

            var hipsRb = GetOrAddComponent<Rigidbody>(hips.gameObject);
            hipsRb.mass = 12f;
            hipsRb.linearDamping = 0.5f;
            hipsRb.angularDamping = 0.5f;
            hipsRb.isKinematic = pinHips;

            CleanConflictingColliders<BoxCollider>(hips.gameObject);
            var hipsCol = GetOrAddComponent<BoxCollider>(hips.gameObject);
            hipsCol.size = new Vector3(0.22f, 0.15f, 0.16f);
            hipsCol.center = new Vector3(0f, -0.02f, 0f);

            // 2. Setup ActiveRagdollController
            var controller = GetOrAddComponent<ActiveRagdollController>(avatarGo);
            controller.ClearJoints();
            controller.globalSpring = springPower;
            controller.globalDamper = damping;
            controller.pinHips = pinHips;

            // 3. Setup Joints
            var boneMap = new Dictionary<HumanBodyBones, Transform>();
            boneMap[HumanBodyBones.Hips] = hips;

            foreach (var def in JointDefinitions)
            {
                var boneT = animator.GetBoneTransform(def.bone);
                if (boneT == null) continue;

                boneMap[def.bone] = boneT;

                Transform parentT = animator.GetBoneTransform(def.parentBone);
                if (parentT == null && def.parentBone == HumanBodyBones.Chest)
                {
                    parentT = animator.GetBoneTransform(HumanBodyBones.Spine);
                }
                if (parentT == null) parentT = hips;

                var parentRb = parentT.GetComponent<Rigidbody>();
                if (parentRb == null)
                {
                    parentRb = parentT.gameObject.AddComponent<Rigidbody>();
                    parentRb.mass = 8f;
                }

                var rb = GetOrAddComponent<Rigidbody>(boneT.gameObject);
                rb.mass = def.mass;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                SetupBoneCollider(boneT, def);

                var joint = GetOrAddComponent<ConfigurableJoint>(boneT.gameObject);
                joint.connectedBody = parentRb;
                joint.autoConfigureConnectedAnchor = true;
                joint.anchor = Vector3.zero;

                joint.axis = def.primaryAxis;
                joint.secondaryAxis = def.secondaryAxis;

                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;

                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = ConfigurableJointMotion.Limited;
                joint.angularZMotion = ConfigurableJointMotion.Limited;

                joint.lowAngularXLimit = new SoftJointLimit { limit = def.lowAngularXLimit };
                joint.highAngularXLimit = new SoftJointLimit { limit = def.highAngularXLimit };
                joint.angularYLimit = new SoftJointLimit { limit = def.angularYLimit };
                joint.angularZLimit = new SoftJointLimit { limit = def.angularZLimit };

                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.slerpDrive = new JointDrive
                {
                    positionSpring = springPower,
                    positionDamper = damping,
                    maximumForce = float.MaxValue
                };
                joint.configuredInWorldSpace = false;
                joint.enablePreprocessing = false;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;

                controller.RegisterJoint(def.bone, boneT, joint);
            }

            // 4. Ignore adjacent bone collisions
            IgnoreBoneCollisions(animator);

            // 5. Initialize controller basis frames & pose
            controller.SetupCollisionIgnores();
            controller.InitJointBasisTransforms();
            controller.SetNaturalIdlePose();

            // Disable Animator in play mode so it doesn't fight joints
            animator.enabled = false;

            Debug.Log($"[RuntimeRagdollRigger] Successfully configured {controller.Muscles.Count} joints on {avatarGo.name}!");
            return true;
        }

        public static void IgnoreBoneCollisions(Animator animator)
        {
            if (animator == null) return;

            HumanBodyBones[][] adjacentPairs = new[]
            {
                new[] { HumanBodyBones.Hips, HumanBodyBones.Spine },
                new[] { HumanBodyBones.Spine, HumanBodyBones.Chest },
                new[] { HumanBodyBones.Chest, HumanBodyBones.Head },
                new[] { HumanBodyBones.Chest, HumanBodyBones.LeftUpperArm },
                new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm },
                new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
                new[] { HumanBodyBones.Chest, HumanBodyBones.RightUpperArm },
                new[] { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
                new[] { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
                new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg },
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg },
                new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
                new[] { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg },
                new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
                new[] { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg },
                new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg },
                new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot },
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
                var t1 = animator.GetBoneTransform(pair[0]);
                var t2 = animator.GetBoneTransform(pair[1]);
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

        private static void CleanConflictingColliders<TTarget>(GameObject go) where TTarget : Collider
        {
            var colliders = go.GetComponents<Collider>();
            foreach (var col in colliders)
            {
                if (!(col is TTarget))
                {
                    SafeDestroy(col);
                }
            }
        }

        private static void SetupBoneCollider(Transform boneT, BoneJointDef def)
        {
            if (def.bone == HumanBodyBones.Head)
            {
                CleanConflictingColliders<SphereCollider>(boneT.gameObject);
                var sphere = GetOrAddComponent<SphereCollider>(boneT.gameObject);
                sphere.radius = def.radius;
                sphere.center = new Vector3(0f, 0.08f, 0.02f);
            }
            else if (def.bone == HumanBodyBones.LeftFoot || def.bone == HumanBodyBones.RightFoot)
            {
                CleanConflictingColliders<BoxCollider>(boneT.gameObject);
                var box = GetOrAddComponent<BoxCollider>(boneT.gameObject);
                box.size = new Vector3(0.08f, 0.06f, 0.16f);
                box.center = new Vector3(0f, -0.03f, 0.06f);
            }
            else if (def.bone == HumanBodyBones.LeftHand || def.bone == HumanBodyBones.RightHand)
            {
                CleanConflictingColliders<SphereCollider>(boneT.gameObject);
                var sphere = GetOrAddComponent<SphereCollider>(boneT.gameObject);
                sphere.radius = def.radius;
                sphere.center = Vector3.zero;
            }
            else if (def.bone == HumanBodyBones.LeftUpperArm || def.bone == HumanBodyBones.LeftLowerArm)
            {
                CleanConflictingColliders<CapsuleCollider>(boneT.gameObject);
                var capsule = GetOrAddComponent<CapsuleCollider>(boneT.gameObject);
                capsule.radius = def.radius;
                float height = GetBoneSegmentLength(boneT, 0.22f);
                capsule.height = height;
                capsule.direction = 0; // X-axis
                capsule.center = new Vector3(-height * 0.5f, 0f, 0f);
            }
            else if (def.bone == HumanBodyBones.RightUpperArm || def.bone == HumanBodyBones.RightLowerArm)
            {
                CleanConflictingColliders<CapsuleCollider>(boneT.gameObject);
                var capsule = GetOrAddComponent<CapsuleCollider>(boneT.gameObject);
                capsule.radius = def.radius;
                float height = GetBoneSegmentLength(boneT, 0.22f);
                capsule.height = height;
                capsule.direction = 0; // X-axis
                capsule.center = new Vector3(height * 0.5f, 0f, 0f);
            }
            else if (def.bone == HumanBodyBones.LeftUpperLeg || def.bone == HumanBodyBones.RightUpperLeg ||
                     def.bone == HumanBodyBones.LeftLowerLeg || def.bone == HumanBodyBones.RightLowerLeg)
            {
                CleanConflictingColliders<CapsuleCollider>(boneT.gameObject);
                var capsule = GetOrAddComponent<CapsuleCollider>(boneT.gameObject);
                capsule.radius = def.radius;
                float height = GetBoneSegmentLength(boneT, 0.35f);
                capsule.height = height;
                capsule.direction = 1; // Y-axis
                capsule.center = new Vector3(0f, -height * 0.5f, 0f);
            }
            else
            {
                CleanConflictingColliders<CapsuleCollider>(boneT.gameObject);
                var capsule = GetOrAddComponent<CapsuleCollider>(boneT.gameObject);
                capsule.radius = def.radius;
                float height = GetBoneSegmentLength(boneT, 0.18f);
                capsule.height = height;
                capsule.direction = 1; // Y-axis
                capsule.center = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        private static float GetBoneSegmentLength(Transform boneT, float defaultLength)
        {
            if (boneT.childCount > 0)
            {
                Transform firstChild = boneT.GetChild(0);
                float dist = Vector3.Distance(boneT.position, firstChild.position);
                if (dist > 0.08f && dist < 0.8f) return dist;
            }
            return defaultLength;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
