#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AiChan.Spinal;

namespace AiChan.Editor
{
    public static class VrmRagdollAutoRigger
    {
        private struct BoneJointDef
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

        private static readonly BoneJointDef[] JointDefinitions = new[]
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

            // Left Arm: Elbow hinges inwards (+Y local), prevents hyperextension
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

            // Right Arm: Elbow hinges inwards (-Y local), prevents hyperextension
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

            // Left Leg: Knee hinges backwards (+X local), 0 forward hyperextension
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

            // Right Leg: Knee hinges backwards (+X local), 0 forward hyperextension
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

        [MenuItem("AI-Chan/Rig Active Ragdoll on Avatar")]
        public static string RigActiveRagdollOnSceneAvatar()
        {
            var avatarGo = GameObject.Find("AiChan_Avatar");
            if (avatarGo == null)
            {
                var animatorFound = Object.FindFirstObjectByType<Animator>();
                if (animatorFound != null) avatarGo = animatorFound.gameObject;
            }

            if (avatarGo == null)
            {
                string scenePath = "Assets/Scenes/RoomScene.unity";
                if (System.IO.File.Exists(scenePath))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                    avatarGo = GameObject.Find("AiChan_Avatar");
                    if (avatarGo == null)
                    {
                        var animatorFound = Object.FindFirstObjectByType<Animator>();
                        if (animatorFound != null) avatarGo = animatorFound.gameObject;
                    }
                }
            }

            if (avatarGo == null)
            {
                Debug.LogError("[VrmRagdollAutoRigger] No avatar found in scene!");
                return "{\"error\": \"no_avatar_found\"}";
            }

            return RigActiveRagdoll(avatarGo);
        }

        public static string RigActiveRagdoll(GameObject avatarGo)
        {
            var animator = avatarGo.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("[VrmRagdollAutoRigger] Avatar must have a Humanoid Animator!");
                return "{\"error\": \"not_humanoid\"}";
            }

            // Clean up existing ActiveRagdollController or joint components if any
            var existingCtrl = avatarGo.GetComponent<ActiveRagdollController>();
            if (existingCtrl != null)
            {
                Object.DestroyImmediate(existingCtrl);
            }

            // 1. Setup Hips (Root)
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null)
            {
                return "{\"error\": \"no_hips_bone\"}";
            }

            var hipsRb = GetOrAddComponent<Rigidbody>(hips.gameObject);
            hipsRb.mass = 12f;
            hipsRb.linearDamping = 0.5f;
            hipsRb.angularDamping = 0.5f;
            hipsRb.isKinematic = true;

            CleanConflictingColliders<BoxCollider>(hips.gameObject);
            var hipsCol = GetOrAddComponent<BoxCollider>(hips.gameObject);
            hipsCol.size = new Vector3(0.22f, 0.15f, 0.16f);
            hipsCol.center = new Vector3(0f, -0.02f, 0f);

            // 2. Setup ActiveRagdollController on Root
            var controller = avatarGo.AddComponent<ActiveRagdollController>();
            controller.ClearJoints();

            // 3. Setup Bones and Joints
            var boneMap = new Dictionary<HumanBodyBones, Transform>();
            boneMap[HumanBodyBones.Hips] = hips;

            foreach (var def in JointDefinitions)
            {
                var boneT = animator.GetBoneTransform(def.bone);
                if (boneT == null) continue;

                boneMap[def.bone] = boneT;

                // Parent bone
                HumanBodyBones parentEnum = def.parentBone;
                Transform parentT = animator.GetBoneTransform(parentEnum);
                if (parentT == null && parentEnum == HumanBodyBones.Chest)
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

                // Setup Rigidbody
                var rb = GetOrAddComponent<Rigidbody>(boneT.gameObject);
                rb.mass = def.mass;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // Setup Collider (cleans duplicate/conflicting colliders)
                SetupBoneCollider(boneT, def);

                // Setup ConfigurableJoint
                var joint = GetOrAddComponent<ConfigurableJoint>(boneT.gameObject);
                joint.connectedBody = parentRb;
                joint.autoConfigureConnectedAnchor = true;
                joint.anchor = Vector3.zero;

                // Set primary and secondary axes
                joint.axis = def.primaryAxis;
                joint.secondaryAxis = def.secondaryAxis;

                // Lock linear motion
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;

                // Configure realistic angular limits
                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = ConfigurableJointMotion.Limited;
                joint.angularZMotion = ConfigurableJointMotion.Limited;

                joint.lowAngularXLimit = new SoftJointLimit { limit = def.lowAngularXLimit };
                joint.highAngularXLimit = new SoftJointLimit { limit = def.highAngularXLimit };
                joint.angularYLimit = new SoftJointLimit { limit = def.angularYLimit };
                joint.angularZLimit = new SoftJointLimit { limit = def.angularZLimit };

                // Slerp drive for muscle action
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                var drive = new JointDrive
                {
                    positionSpring = 1000f,
                    positionDamper = 100f,
                    maximumForce = float.MaxValue
                };
                joint.slerpDrive = drive;
                joint.configuredInWorldSpace = false;
                joint.enablePreprocessing = false;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;

                // Register joint in controller
                controller.RegisterJoint(def.bone, boneT, joint);
            }

            // 4. Ignore collisions between adjacent and conflicting bones
            IgnoreBoneCollisions(animator);

            // 5. Add 4-level decoupled architecture components
            if (avatarGo.GetComponent<AiChan.Thalamus.ThalamusSensor>() == null)
            {
                avatarGo.AddComponent<AiChan.Thalamus.ThalamusSensor>();
            }

            if (avatarGo.GetComponent<AiChan.Limbic.LimbicController>() == null)
            {
                avatarGo.AddComponent<AiChan.Limbic.LimbicController>();
            }

            if (avatarGo.GetComponent<AiChan.Cortex.CortexBridgeClient>() == null)
            {
                avatarGo.AddComponent<AiChan.Cortex.CortexBridgeClient>();
            }

            // Interactive limb dragger
            if (avatarGo.GetComponent<PhysicalLimbDragger>() == null)
            {
                avatarGo.AddComponent<PhysicalLimbDragger>();
            }

            // 6. Ensure Animator doesn't overwrite physics in Play mode
            animator.enabled = false;

            EditorUtility.SetDirty(avatarGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(avatarGo.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(avatarGo.scene);

            Debug.Log($"[VrmRagdollAutoRigger] Successfully rigged Active Ragdoll on {avatarGo.name} with {controller.Muscles.Count} joints!");
            return "{\"status\": \"rigged\", \"jointCount\": " + controller.Muscles.Count + "}";
        }

        public static void IgnoreBoneCollisions(Animator animator)
        {
            if (animator == null) return;

            HumanBodyBones[][] adjacentPairs = new[]
            {
                // Torso
                new[] { HumanBodyBones.Hips, HumanBodyBones.Spine },
                new[] { HumanBodyBones.Spine, HumanBodyBones.Chest },
                new[] { HumanBodyBones.Chest, HumanBodyBones.Head },

                // Left Arm
                new[] { HumanBodyBones.Chest, HumanBodyBones.LeftUpperArm },
                new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm },
                new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },

                // Right Arm
                new[] { HumanBodyBones.Chest, HumanBodyBones.RightUpperArm },
                new[] { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
                new[] { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },

                // Legs (adjacent bones)
                new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg },
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg },
                new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },

                new[] { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg },
                new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
                new[] { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },

                // Inter-leg collisions (prevent thigh/shin collision fights)
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
                    Object.DestroyImmediate(col);
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
                // Left arm extends along -X
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
                // Right arm extends along +X
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
                // Legs extend downwards along -Y
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
                // Spine & Chest extend upwards along +Y
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
    }
}
#endif
