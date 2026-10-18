#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AiChan.Avatar.Gaze;
using AiChan.Avatar.Expressiveness;
using AiChan.Spinal;
using AiChan.Player;

namespace AiChan.Editor
{
    public static class SocialGazeAndReachingSetup
    {
        [MenuItem("AI-Chan/Setup Social Gaze and Limb Reacher in Scene")]
        public static string SetupSocialGazeAndReacherInScene()
        {
            try
            {
                string scenePath = "Assets/Scenes/RoomScene.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                var avatar = GameObject.Find("AiChan_Avatar");
                if (avatar == null)
                {
                    avatar = GameObject.Find("AvatarSample_A");
                    if (avatar == null)
                    {
                        var anims = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
                        foreach (var a in anims)
                        {
                            if (a.isHuman && !a.name.Contains("Player")) { avatar = a.gameObject; break; }
                        }
                    }
                }

                if (avatar == null)
                {
                    return "ERROR: AiChan_Avatar not found in RoomScene!";
                }

                string log = $"Configuring {avatar.name} in RoomScene:\n";

                // 1. SocialGazeController
                var socialGaze = avatar.GetComponent<SocialGazeController>();
                if (socialGaze == null)
                {
                    socialGaze = avatar.AddComponent<SocialGazeController>();
                    log += "- Added SocialGazeController\n";
                }
                else
                {
                    log += "- SocialGazeController already present\n";
                }

                // 2. LimbTargetReacher
                var reacher = avatar.GetComponent<LimbTargetReacher>();
                if (reacher == null)
                {
                    reacher = avatar.AddComponent<LimbTargetReacher>();
                    log += "- Added LimbTargetReacher\n";
                }
                else
                {
                    log += "- LimbTargetReacher already present\n";
                }

                // 3. Ensure AvatarLookAtController is wired to SocialGazeController
                var lookAt = avatar.GetComponent<AvatarLookAtController>();
                if (lookAt != null)
                {
                    var soLookAt = new SerializedObject(lookAt);
                    var gazeProp = soLookAt.FindProperty("_socialGaze");
                    if (gazeProp != null)
                    {
                        gazeProp.objectReferenceValue = socialGaze;
                        soLookAt.ApplyModifiedProperties();
                        log += "- Hooked _socialGaze into AvatarLookAtController\n";
                    }
                }

                // 4. Create or Locate Interactive Affordance Anchors in Scene
                var anchorsRoot = GameObject.Find("Affordance_Anchors");
                if (anchorsRoot == null)
                {
                    anchorsRoot = new GameObject("Affordance_Anchors");
                    anchorsRoot.transform.position = Vector3.zero;
                }

                // High-Five Anchor (Right Hand, in front of avatar right shoulder)
                var highFiveGo = GameObject.Find("Anchor_HighFive");
                if (highFiveGo == null)
                {
                    highFiveGo = new GameObject("Anchor_HighFive");
                    highFiveGo.transform.SetParent(anchorsRoot.transform);
                }
                highFiveGo.transform.position = new Vector3(-0.18f, 1.25f, -0.30f);
                highFiveGo.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

                var highFiveAnchor = highFiveGo.GetComponent<AffordanceAnchor>();
                if (highFiveAnchor == null) highFiveAnchor = highFiveGo.AddComponent<AffordanceAnchor>();
                highFiveAnchor.affordanceType = AffordanceType.HighFive;
                highFiveAnchor.preferredHand = HumanBodyBones.RightHand;
                highFiveAnchor.localApproachDirection = Vector3.forward;
                highFiveAnchor.reachSpeedMultiplier = 1.0f;
                highFiveAnchor.customPoleHint = new Vector3(0.35f, -0.6f, -0.1f);
                log += "- Configured Anchor_HighFive\n";

                // Hand-Hold Anchor (Left Hand, in front of avatar left hip/hand)
                var handHoldGo = GameObject.Find("Anchor_HandHold");
                if (handHoldGo == null)
                {
                    handHoldGo = new GameObject("Anchor_HandHold");
                    handHoldGo.transform.SetParent(anchorsRoot.transform);
                }
                handHoldGo.transform.position = new Vector3(0.18f, 1.00f, -0.28f);
                handHoldGo.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

                var handHoldAnchor = handHoldGo.GetComponent<AffordanceAnchor>();
                if (handHoldAnchor == null) handHoldAnchor = handHoldGo.AddComponent<AffordanceAnchor>();
                handHoldAnchor.affordanceType = AffordanceType.HandHold;
                handHoldAnchor.preferredHand = HumanBodyBones.LeftHand;
                handHoldAnchor.localApproachDirection = Vector3.up;
                handHoldAnchor.reachSpeedMultiplier = 1.0f;
                handHoldAnchor.customPoleHint = new Vector3(-0.35f, -0.6f, -0.1f);
                log += "- Configured Anchor_HandHold\n";

                EditorUtility.SetDirty(avatar);
                EditorUtility.SetDirty(anchorsRoot);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                log += "SUCCESS: Scene configured and saved successfully!";
                Debug.Log($"[SocialGazeAndReachingSetup] {log}");
                return log;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SocialGazeAndReachingSetup] Setup failed: {ex}");
                return $"ERROR: {ex.Message}";
            }
        }

        [MenuItem("AI-Chan/Validate Social Gaze and Limb Reaching")]
        public static string ValidateSocialGazeAndLimbReaching()
        {
            var errors = new List<string>();
            Application.LogCallback onLog = (cond, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    errors.Add($"[{type}] {cond}\n{stack}");
                }
            };
            Application.logMessageReceived += onLog;

            try
            {
                Debug.Log("[Validator] Beginning comprehensive Social Gaze & Limb Reaching validation...");

                // Step 1: Ensure Scene Setup
                SetupSocialGazeAndReacherInScene();

                var avatar = GameObject.Find("AiChan_Avatar");
                if (avatar == null)
                {
                    return Fail("AiChan_Avatar not found in RoomScene!");
                }

                var socialGaze = avatar.GetComponent<SocialGazeController>();
                if (socialGaze == null) return Fail("SocialGazeController missing on avatar!");

                var reacher = avatar.GetComponent<LimbTargetReacher>();
                if (reacher == null) return Fail("LimbTargetReacher missing on avatar!");

                var lookAt = avatar.GetComponent<AvatarLookAtController>();
                if (lookAt == null) return Fail("AvatarLookAtController missing on avatar!");

                var saccadeBlink = avatar.GetComponent<SaccadeAndBlinkController>();
                if (saccadeBlink == null) return Fail("SaccadeAndBlinkController missing on avatar!");

                var ragdoll = avatar.GetComponent<ActiveRagdollController>();
                if (ragdoll == null) return Fail("ActiveRagdollController missing on avatar!");

                var anim = avatar.GetComponent<Animator>();
                if (anim == null || !anim.isHuman) return Fail("Humanoid Animator missing on avatar!");

                ragdoll.InitJointBasisTransforms();
                ragdoll.SetupCollisionIgnores();
                ragdoll.SetNaturalIdlePose();
                socialGaze.EnsureInitialized();
                reacher.EnsureInitialized();

                // Step 2: Validate Two-Bone IK Analytical Precision (Kinematic test on both arms)
                Transform rUpper = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform rLower = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Transform rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);

                Transform lUpper = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform lLower = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                Transform lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);

                Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Spine);

                var highFiveAnchor = GameObject.Find("Anchor_HighFive")?.GetComponent<AffordanceAnchor>();
                if (highFiveAnchor == null) return Fail("Anchor_HighFive component missing!");

                var handHoldAnchor = GameObject.Find("Anchor_HandHold")?.GetComponent<AffordanceAnchor>();
                if (handHoldAnchor == null) return Fail("Anchor_HandHold component missing!");

                // Right Arm Kinematic IK Test
                Quaternion origRUpperRot = rUpper.localRotation;
                Quaternion origRLowerRot = rLower.localRotation;

                Vector3 targetRight = highFiveAnchor.WorldPosition;
                Vector3 poleHintRight = rUpper.position + reacher.RightArm.DefaultPoleHint;
                float rL1 = reacher.RightArm.upperArmLength;
                float rL2 = reacher.RightArm.lowerArmLength;
                Transform rParent = rUpper.parent != null ? rUpper.parent : chest;

                var rUpperMuscle = ragdoll.Muscles.Find(m => m.bone == HumanBodyBones.RightUpperArm);
                var rLowerMuscle = ragdoll.Muscles.Find(m => m.bone == HumanBodyBones.RightLowerArm);
                Quaternion initRUpper = rUpperMuscle != null ? rUpperMuscle.initialLocalRotation : rUpper.localRotation;
                Quaternion initRLower = rLowerMuscle != null ? rLowerMuscle.initialLocalRotation : rLower.localRotation;

                bool rIkSolved = LimbTargetReacher.SolveTwoBoneIK(
                    rUpper, rLower, rHand, rParent,
                    targetRight, poleHintRight, rL1, rL2,
                    initRUpper, initRLower,
                    reacher.RightArm.upperBoneLocal, reacher.RightArm.lowerBoneLocal,
                    HumanBodyBones.RightHand,
                    out Quaternion ikRUpperLocal, out Quaternion ikRLowerLocal
                );

                if (!rIkSolved) return Fail("Right Arm Two-Bone IK failed to solve!");
                rUpper.localRotation = ikRUpperLocal;
                rLower.localRotation = ikRLowerLocal;
                float rKinematicErr = Vector3.Distance(rHand.position, targetRight);
                rUpper.localRotation = origRUpperRot;
                rLower.localRotation = origRLowerRot;

                if (rKinematicErr > 0.005f)
                {
                    return Fail($"Right Arm Kinematic IK precision error too large: {rKinematicErr:F4}m (expected < 0.005m)!");
                }
                Debug.Log($"[Validator] Right Arm Analytical IK verified: error={rKinematicErr:F4}m.");

                // Left Arm Kinematic IK Test
                Quaternion origLUpperRot = lUpper.localRotation;
                Quaternion origLLowerRot = lLower.localRotation;

                Vector3 targetLeft = handHoldAnchor.WorldPosition;
                Vector3 poleHintLeft = lUpper.position + reacher.LeftArm.DefaultPoleHint;
                float lL1 = reacher.LeftArm.upperArmLength;
                float lL2 = reacher.LeftArm.lowerArmLength;
                Transform lParent = lUpper.parent != null ? lUpper.parent : chest;

                var lUpperMuscle = ragdoll.Muscles.Find(m => m.bone == HumanBodyBones.LeftUpperArm);
                var lLowerMuscle = ragdoll.Muscles.Find(m => m.bone == HumanBodyBones.LeftLowerArm);
                Quaternion initLUpper = lUpperMuscle != null ? lUpperMuscle.initialLocalRotation : lUpper.localRotation;
                Quaternion initLLower = lLowerMuscle != null ? lLowerMuscle.initialLocalRotation : lLower.localRotation;

                bool lIkSolved = LimbTargetReacher.SolveTwoBoneIK(
                    lUpper, lLower, lHand, lParent,
                    targetLeft, poleHintLeft, lL1, lL2,
                    initLUpper, initLLower,
                    reacher.LeftArm.upperBoneLocal, reacher.LeftArm.lowerBoneLocal,
                    HumanBodyBones.LeftHand,
                    out Quaternion ikLUpperLocal, out Quaternion ikLLowerLocal
                );

                if (!lIkSolved) return Fail("Left Arm Two-Bone IK failed to solve!");
                lUpper.localRotation = ikLUpperLocal;
                lLower.localRotation = ikLLowerLocal;
                float lKinematicErr = Vector3.Distance(lHand.position, targetLeft);
                lUpper.localRotation = origLUpperRot;
                lLower.localRotation = origLLowerRot;

                if (lKinematicErr > 0.005f)
                {
                    return Fail($"Left Arm Kinematic IK precision error too large: {lKinematicErr:F4}m (expected < 0.005m)!");
                }
                Debug.Log($"[Validator] Left Arm Analytical IK verified: error={lKinematicErr:F4}m.");

                // Step 3: Validate Social Gaze Cycles, Aversion, Motion Reversion & Personal Space Breach
                socialGaze.EnterDirectContact();
                if (socialGaze.CurrentState != SocialGazeState.DirectContact)
                {
                    return Fail($"Expected DirectContact state, got {socialGaze.CurrentState}!");
                }

                Vector3 playerHeadTarget = new Vector3(0f, 1.55f, -2.2f);
                Vector3 gazeTarget1 = socialGaze.GetEffectiveGazeTarget(playerHeadTarget);
                float directDist = Vector3.Distance(gazeTarget1, playerHeadTarget);
                if (directDist > 0.01f)
                {
                    return Fail($"DirectContact gaze target diverted by {directDist:F3}m (expected direct lock on player)!");
                }

                // Force idle aversion DownRight and verify gaze deflects downwards
                socialGaze.EnterIdleAversion();
                socialGaze.SetAversionDirection(AversionDirection.DownRight);
                if (socialGaze.CurrentState != SocialGazeState.IdleAversion)
                {
                    return Fail($"Expected IdleAversion state, got {socialGaze.CurrentState}!");
                }

                Vector3 gazeTargetAverted = socialGaze.GetEffectiveGazeTarget(playerHeadTarget);
                float avertedDist = Vector3.Distance(gazeTargetAverted, playerHeadTarget);
                if (avertedDist < 0.20f)
                {
                    return Fail($"IdleAversion failed to divert gaze: diverted only {avertedDist:F3}m!");
                }
                if (gazeTargetAverted.y >= playerHeadTarget.y)
                {
                    return Fail($"DownRight IdleAversion failed to deflect downwards: avertedY={gazeTargetAverted.y:F3} >= playerY={playerHeadTarget.y:F3}!");
                }

                // Test Motion/Speech Reversion: While in IdleAversion, notify player spoke
                socialGaze.NotifyPlayerSpoke();
                if (socialGaze.CurrentState != SocialGazeState.DirectContact)
                {
                    return Fail("Failed to revert to DirectContact upon speech notification!");
                }

                // Test Personal Space Breach (< 0.60m)
                var player = PlayerController.Instance != null ? PlayerController.Instance : UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                Vector3 savedPlayerPos = player != null ? player.transform.position : Vector3.zero;
                if (player != null)
                {
                    // Place player 0.45m in front of avatar head
                    player.transform.position = new Vector3(0f, 0f, -0.45f);
                    if (player.GazeTarget != null) player.GazeTarget.position = new Vector3(0f, 1.30f, -0.45f);

                    socialGaze.Step(0.02f);
                    if (socialGaze.CurrentState != SocialGazeState.PersonalSpaceAversion)
                    {
                        return Fail($"Personal space breach (< 0.6m) did not trigger PersonalSpaceAversion state! Current: {socialGaze.CurrentState}");
                    }

                    Vector3 personalAverted = socialGaze.GetEffectiveGazeTarget(player.GazeTarget.position);
                    if (personalAverted.y >= player.GazeTarget.position.y)
                    {
                        return Fail($"Personal space bashful aversion failed to deflect downwards: avertedY={personalAverted.y:F3} >= playerY={player.GazeTarget.position.y:F3}!");
                    }

                    // Check Anime blush expression
                    var expr = avatar.GetComponent<AnimeExpressivenessEngine>();
                    if (expr != null && expr.GlobalBlush < 0.5f)
                    {
                        return Fail($"Anime blushing not active during personal space breach: blush={expr.GlobalBlush:F2}");
                    }

                    // Restore player position
                    player.transform.position = savedPlayerPos;
                    if (player.GazeTarget != null) player.GazeTarget.position = new Vector3(0f, 1.52f, -2.2f);
                    socialGaze.Step(0.02f);
                    socialGaze.EnterDirectContact();
                }

                // Step 4: Validate Saccade-to-Blink Coordination (> 20 degrees triggers micro-blink)
                for (int b = 0; b < 25; b++) saccadeBlink.Step(0.02f);
                if (saccadeBlink.IsBlinking)
                {
                    return Fail("Blink state failed to settle before saccade test!");
                }

                // A: Trigger 25-degree saccade directly and verify blink activates
                saccadeBlink.TriggerSaccade(new Vector2(25f, 0f));
                if (!saccadeBlink.IsBlinking)
                {
                    return Fail("TriggerSaccade (25 degrees) did not trigger coordinated micro-blink!");
                }

                // Settle blink again
                for (int b = 0; b < 25; b++) saccadeBlink.Step(0.02f);

                // B: Transition from DirectContact to IdleAversion (> 20 degrees shift) and verify micro-blink triggers
                socialGaze.EnterDirectContact();
                socialGaze.GetEffectiveGazeTarget(playerHeadTarget);
                for (int b = 0; b < 10; b++) saccadeBlink.Step(0.02f);

                socialGaze.EnterIdleAversion();
                socialGaze.GetEffectiveGazeTarget(playerHeadTarget);
                if (!saccadeBlink.IsBlinking)
                {
                    return Fail("Social gaze shift from DirectContact to IdleAversion did not trigger coordinated micro-blink!");
                }

                Debug.Log($"[Validator] Social Gaze direct contact, aversion, personal space, and saccade blink verified.");

                // Step 5: Settle ragdoll into relaxed idle pose first
                Physics.simulationMode = SimulationMode.Script;
                for (int step = 0; step < 40; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                // Step 6: Validate Active PhysX Right Hand Reaching within 0.6s
                Vector3 rHandStartPos = rHand.position;
                Vector3 highFivePos = highFiveAnchor.WorldPosition;
                bool reachStarted = reacher.ReachTarget(HumanBodyBones.RightHand, highFiveAnchor);
                if (!reachStarted || !reacher.IsRightArmReaching)
                {
                    return Fail("ReachTarget failed to initiate right arm reaching!");
                }

                // Physics simulation loop (30 steps at dt=0.02s = 0.60 seconds)
                for (int step = 0; step < 30; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float finalRightHandDist = Vector3.Distance(rHand.position, highFivePos);
                float rightHandDisplacement = Vector3.Distance(rHand.position, rHandStartPos);

                if (finalRightHandDist > 0.20f)
                {
                    return Fail($"Right hand failed to reach target anchor within 0.20m: finalDist={finalRightHandDist:F3}m!");
                }

                if (rightHandDisplacement < 0.25f)
                {
                    return Fail($"Right hand displaced insufficiently: displaced only {rightHandDisplacement:F3}m (expected >= 0.25m)!");
                }

                // Check active ragdoll stability during reach (avatar must not collapse or fall)
                Transform headBone = anim.GetBoneTransform(HumanBodyBones.Head);
                if (headBone.position.y < 1.30f)
                {
                    return Fail($"Avatar collapsed while reaching: Head height={headBone.position.y:F2}m (expected >= 1.30m)!");
                }

                if (reacher.CurrentTorsoTiltAngle > 15.0f)
                {
                    return Fail($"Avatar lost balance while reaching: Torso tilt={reacher.CurrentTorsoTiltAngle:F1}° (expected < 15°)!");
                }
                Debug.Log($"[Validator] Right hand reach verified: Dist={finalRightHandDist:F3}m, Displaced={rightHandDisplacement:F3}m, HeadHeight={headBone.position.y:F2}m, Tilt={reacher.CurrentTorsoTiltAngle:F1}°.");

                // Step 7: Test Continuous / Repeated ReachTarget Calls (Idempotency)
                for (int step = 0; step < 10; step++)
                {
                    reacher.ReachTarget(HumanBodyBones.RightHand, highFiveAnchor);
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }
                float repeatedCallDist = Vector3.Distance(rHand.position, highFivePos);
                if (repeatedCallDist > 0.20f)
                {
                    return Fail($"Calling ReachTarget consecutively caused arm to drift/reset! Dist={repeatedCallDist:F3}m");
                }
                Debug.Log("[Validator] ReachTarget idempotency verified.");

                // Step 8: Validate ClearReach & Return to Relaxed Balance Pose
                reacher.ClearReach(HumanBodyBones.RightHand);
                if (reacher.IsRightArmReaching)
                {
                    return Fail("Right arm should not be reaching after ClearReach!");
                }

                for (int step = 0; step < 35; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float rightReturnDist = Vector3.Distance(rHand.position, rHandStartPos);
                if (rightReturnDist > 0.10f)
                {
                    return Fail($"Right arm failed to return to relaxed pose: residual offset={rightReturnDist:F3}m (expected < 0.10m)!");
                }
                Debug.Log($"[Validator] Right arm ClearReach recovery verified: residual offset={rightReturnDist:F3}m.");

                // Step 9: Validate Active PhysX Left Hand Reaching (Anchor_HandHold) within 0.6s
                Vector3 lHandStartPos = lHand.position;
                Vector3 handHoldPos = handHoldAnchor.WorldPosition;
                bool leftReachStarted = reacher.ReachTarget(HumanBodyBones.LeftHand, handHoldAnchor);
                if (!leftReachStarted || !reacher.IsLeftArmReaching)
                {
                    return Fail("ReachTarget failed to initiate left arm reaching!");
                }

                for (int step = 0; step < 30; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float finalLeftHandDist = Vector3.Distance(lHand.position, handHoldPos);
                float leftHandDisplacement = Vector3.Distance(lHand.position, lHandStartPos);

                if (finalLeftHandDist > 0.15f)
                {
                    return Fail($"Left hand failed to reach target anchor within 0.15m: finalDist={finalLeftHandDist:F3}m!");
                }

                if (leftHandDisplacement < 0.14f)
                {
                    return Fail($"Left hand displaced insufficiently: displaced only {leftHandDisplacement:F3}m (expected >= 0.14m)!");
                }

                if (reacher.CurrentTorsoTiltAngle > 15.0f)
                {
                    return Fail($"Torso tilted excessively during left reach: tilt={reacher.CurrentTorsoTiltAngle:F1}° (expected < 15°)!");
                }
                Debug.Log($"[Validator] Left hand reach verified: Dist={finalLeftHandDist:F3}m, Displaced={leftHandDisplacement:F3}m, Tilt={reacher.CurrentTorsoTiltAngle:F1}°.");

                reacher.ClearReach(HumanBodyBones.LeftHand);
                for (int step = 0; step < 35; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float leftReturnDist = Vector3.Distance(lHand.position, lHandStartPos);
                if (leftReturnDist > 0.10f)
                {
                    return Fail($"Left arm failed to return to relaxed pose: residual offset={leftReturnDist:F3}m (expected < 0.10m)!");
                }
                Debug.Log($"[Validator] Left arm ClearReach recovery verified: residual offset={leftReturnDist:F3}m.");

                // Step 10: Validate Protective Fall Reflex (> 45 deg tilt disengages reaching)
                reacher.ReachTarget(HumanBodyBones.RightHand, highFiveAnchor);
                if (!reacher.IsRightArmReaching) return Fail("Failed to re-engage reach for fall reflex test!");

                // Simulate severe body tilt by rotating avatar root by 50 degrees
                Quaternion origRot = avatar.transform.rotation;
                avatar.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
                Physics.SyncTransforms();

                // Run 1 fixed update step to detect tilt
                reacher.Step(0.02f);
                ragdoll.StepMuscles();
                Physics.Simulate(0.02f);

                if (!reacher.IsFallReflexActive)
                {
                    return Fail($"Fall reflex did not activate at tilt={reacher.CurrentTorsoTiltAngle:F1}° (expected > 45°)! ");
                }
                if (reacher.IsRightArmReaching)
                {
                    return Fail("Reaching was not disengaged after fall reflex triggered!");
                }

                // Restore avatar orientation
                avatar.transform.rotation = origRot;
                Physics.SyncTransforms();
                ragdoll.SetNaturalIdlePose();
                reacher.DisengageAllReaches(immediate: true);

                for (int step = 0; step < 20; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                if (reacher.IsFallReflexActive)
                {
                    return Fail($"Fall reflex did not reset after avatar recovered upright posture (tilt={reacher.CurrentTorsoTiltAngle:F1}°)");
                }
                Debug.Log("[Validator] Protective fall reflex trigger and recovery verified.");

                // Step 11: Validate Simultaneous Dual-Arm Reaching (HighFive + HandHold) without breaking balance
                bool dualRStarted = reacher.ReachTarget(HumanBodyBones.RightHand, highFiveAnchor);
                bool dualLStarted = reacher.ReachTarget(HumanBodyBones.LeftHand, handHoldAnchor);
                if (!dualRStarted || !dualLStarted || !reacher.IsRightArmReaching || !reacher.IsLeftArmReaching)
                {
                    return Fail("Failed to initiate simultaneous dual arm reaches!");
                }

                for (int step = 0; step < 35; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float dualRightDist = Vector3.Distance(rHand.position, highFiveAnchor.WorldPosition);
                float dualLeftDist = Vector3.Distance(lHand.position, handHoldAnchor.WorldPosition);

                if (dualRightDist > 0.20f)
                {
                    return Fail($"Dual reaching right hand failed to reach: dist={dualRightDist:F3}m (expected < 0.20m)!");
                }
                if (dualLeftDist > 0.15f)
                {
                    return Fail($"Dual reaching left hand failed to reach: dist={dualLeftDist:F3}m (expected < 0.15m)!");
                }
                if (reacher.CurrentTorsoTiltAngle > 15.0f)
                {
                    return Fail($"Torso tilted excessively during simultaneous dual reach: tilt={reacher.CurrentTorsoTiltAngle:F1}° (expected < 15°)!");
                }
                if (headBone.position.y < 1.30f)
                {
                    return Fail($"Avatar collapsed during dual reach: headY={headBone.position.y:F2}m (expected >= 1.30m)!");
                }

                reacher.ClearAllReaches();
                for (int step = 0; step < 30; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }
                Debug.Log($"[Validator] Simultaneous dual arm reaching verified: RDist={dualRightDist:F3}m, LDist={dualLeftDist:F3}m, Tilt={reacher.CurrentTorsoTiltAngle:F1}°.");

                // Step 12: Validate Rapid In-Flight Retargeting & Dynamic Target Switching
                var anchorBGo = new GameObject("Anchor_Retarget_Test");
                anchorBGo.transform.position = new Vector3(-0.24f, 1.15f, -0.28f);
                var anchorB = anchorBGo.AddComponent<AffordanceAnchor>();
                anchorB.preferredHand = HumanBodyBones.RightHand;

                reacher.ReachTarget(HumanBodyBones.RightHand, highFiveAnchor);
                // Step 5 frames (in-flight)
                for (int step = 0; step < 5; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                // Switch to anchorB mid-flight
                reacher.ReachTarget(HumanBodyBones.RightHand, anchorB);
                for (int step = 0; step < 30; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float retargetDist = Vector3.Distance(rHand.position, anchorB.WorldPosition);
                UnityEngine.Object.DestroyImmediate(anchorBGo);
                reacher.ClearAllReaches();
                for (int step = 0; step < 25; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                if (retargetDist > 0.18f)
                {
                    return Fail($"Mid-flight retargeting failed to reach new target: dist={retargetDist:F3}m (expected < 0.18m)!");
                }
                Debug.Log($"[Validator] Rapid in-flight retargeting verified: finalDist={retargetDist:F3}m.");

                // Step 13: Validate Target Anchor Destruction Safety (zero crashes or zero-target excursions)
                var tempAnchorGo = new GameObject("Anchor_Destruction_Test");
                tempAnchorGo.transform.position = new Vector3(0.15f, 1.10f, -0.30f);
                var tempAnchor = tempAnchorGo.AddComponent<AffordanceAnchor>();

                reacher.ReachTarget(HumanBodyBones.LeftHand, tempAnchor);
                for (int step = 0; step < 5; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                // Destroy anchor GameObject while arm is actively reaching
                UnityEngine.Object.DestroyImmediate(tempAnchorGo);
                reacher.Step(0.02f);
                ragdoll.StepMuscles();
                Physics.Simulate(0.02f);

                if (reacher.IsLeftArmReaching)
                {
                    return Fail("LimbTargetReacher did not disengage reach when target anchor was destroyed!");
                }
                for (int step = 0; step < 20; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }
                Debug.Log("[Validator] Target anchor destruction safety verified.");

                // Step 14: Validate External Physical Collision & Motor Compliance while Reaching
                reacher.ReachTarget(HumanBodyBones.RightHand, highFiveAnchor);
                for (int step = 0; step < 30; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballGo.transform.localScale = Vector3.one * 0.15f;
                ballGo.transform.position = rLower.position + new Vector3(0f, 0f, -0.50f);
                var ballRb = ballGo.AddComponent<Rigidbody>();
                ballRb.mass = 0.5f;
                ballRb.linearVelocity = new Vector3(0f, 0f, 5.0f); // 5 m/s physical impact on forearm

                for (int step = 0; step < 30; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float postImpactDist = Vector3.Distance(rHand.position, highFiveAnchor.WorldPosition);
                UnityEngine.Object.DestroyImmediate(ballGo);
                reacher.ClearAllReaches();
                for (int step = 0; step < 30; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                if (postImpactDist > 0.20f)
                {
                    return Fail($"Motor failed to recover from physical projectile impact: dist={postImpactDist:F3}m (expected < 0.20m)!");
                }
                if (reacher.CurrentTorsoTiltAngle > 15.0f)
                {
                    return Fail($"Torso balance broken by forearm impact: tilt={reacher.CurrentTorsoTiltAngle:F1}° (expected < 15°)!");
                }
                Debug.Log($"[Validator] Forearm projectile impact compliance verified: postImpactDist={postImpactDist:F3}m, Tilt={reacher.CurrentTorsoTiltAngle:F1}°.");

                // Step 15: Validate Out-of-Workspace / Rear Reach Rejection & Balance Safety
                Vector3 behindTarget = rUpper.position + new Vector3(0f, 0f, 0.40f); // 0.40m behind avatar shoulder
                bool rearReachAllowed = reacher.ReachTarget(HumanBodyBones.RightHand, behindTarget);
                if (rearReachAllowed || reacher.IsRightArmReaching)
                {
                    return Fail("LimbTargetReacher allowed reach target located behind avatar coronal plane!");
                }
                if (reacher.IsInReachWorkspace(HumanBodyBones.RightHand, behindTarget))
                {
                    return Fail("IsInReachWorkspace reported true for target located behind avatar coronal plane!");
                }
                if (reacher.CurrentTorsoTiltAngle > 15.0f)
                {
                    return Fail($"Torso tilted excessively during rear target rejection: tilt={reacher.CurrentTorsoTiltAngle:F1}° (expected < 15°)!");
                }
                Debug.Log("[Validator] Out-of-workspace rear reach rejection & balance safety verified.");

                // Step 16: Validate Disabled Component Reach Safety
                highFiveAnchor.enabled = false;
                bool disabledReachAllowed = reacher.ReachTarget(HumanBodyBones.RightHand, highFiveAnchor);
                highFiveAnchor.enabled = true;
                if (disabledReachAllowed || reacher.IsRightArmReaching)
                {
                    return Fail("LimbTargetReacher initiated reach towards a disabled AffordanceAnchor component!");
                }
                Debug.Log("[Validator] Disabled component reach safety verified.");

                // Step 17: Validate Long-Horizon Social Attention Ratio Convergence (50-60%)
                var fieldD = typeof(SocialGazeController).GetField("_totalDirectContactTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fieldA = typeof(SocialGazeController).GetField("_totalAversionTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldD != null && fieldA != null)
                {
                    fieldD.SetValue(socialGaze, 0f);
                    fieldA.SetValue(socialGaze, 0f);
                    socialGaze.EnterDirectContact();
                    for (int step = 0; step < 3000; step++) // 60 simulated seconds at dt=0.02s
                    {
                        socialGaze.Step(0.02f);
                    }
                    float ratio = socialGaze.DirectContactPercentage;
                    if (ratio < 50.0f || ratio > 60.0f)
                    {
                        return Fail($"Social gaze attention ratio failed to converge into 50-60% band: ratio={ratio:F1}% (expected 50-60%)!");
                    }
                    Debug.Log($"[Validator] Long-horizon attention ratio convergence verified: directRatio={ratio:F1}%.");
                }

                // Step 18: Validate Dynamic Moving Anchor Continuous Tracking
                var movingAnchorGo = new GameObject("Anchor_Moving_Test");
                movingAnchorGo.transform.position = rUpper.position + new Vector3(-0.09f, -0.05f, -0.30f);
                var movingAnchor = movingAnchorGo.AddComponent<AffordanceAnchor>();
                bool movingReachStarted = reacher.ReachTarget(HumanBodyBones.RightHand, movingAnchor);
                if (!movingReachStarted || !reacher.IsRightArmReaching)
                {
                    UnityEngine.Object.DestroyImmediate(movingAnchorGo);
                    return Fail("Failed to initiate reach towards moving anchor!");
                }

                for (int step = 0; step < 40; step++)
                {
                    movingAnchorGo.transform.position += new Vector3(0.001f, 0.001f, -0.001f);
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float finalMovingDist = Vector3.Distance(rHand.position, movingAnchor.WorldPosition);
                UnityEngine.Object.DestroyImmediate(movingAnchorGo);
                reacher.ClearAllReaches();
                for (int step = 0; step < 25; step++)
                {
                    reacher.Step(0.02f);
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                if (finalMovingDist > 0.18f)
                {
                    return Fail($"Dynamic moving anchor tracking error too large: dist={finalMovingDist:F3}m (expected < 0.18m)!");
                }
                Debug.Log($"[Validator] Dynamic moving anchor continuous tracking verified: finalDist={finalMovingDist:F3}m.");

                // Check for console errors captured during execution
                if (errors.Count > 0)
                {
                    return Fail($"Console captured runtime error(s):\n" + string.Join("\n", errors));
                }

                Debug.Log("=== [VALIDATION PASSED] All Social Gaze & Hand Reaching requirements SATISFIED with 0 errors! ===");
                return "SUCCESS: All requirements verified";
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                Physics.simulationMode = SimulationMode.FixedUpdate;
            }
        }

        private static string Fail(string message)
        {
            Debug.LogError($"[Validation FAILED] {message}");
            return $"ERROR: {message}";
        }

        public static string VerifyLivePlayModeState()
        {
            var avatar = GameObject.Find("AiChan_Avatar");
            if (avatar == null) return "ERROR: Avatar not found";

            var gaze = avatar.GetComponent<SocialGazeController>();
            var reacher = avatar.GetComponent<LimbTargetReacher>();
            var lookAt = avatar.GetComponent<AvatarLookAtController>();

            if (gaze == null || reacher == null) return "ERROR: Controllers missing";

            return JsonUtility.ToJson(new LiveDiagnostics
            {
                gazeState = gaze.CurrentState.ToString(),
                directPercentage = gaze.DirectContactPercentage,
                isRightArmReaching = reacher.IsRightArmReaching,
                isLeftArmReaching = reacher.IsLeftArmReaching,
                torsoTilt = reacher.CurrentTorsoTiltAngle,
                isFallReflexActive = reacher.IsFallReflexActive,
                lookAtWeight = lookAt != null ? lookAt.LookAtWeight : 0f
            });
        }

        [Serializable]
        public struct LiveDiagnostics
        {
            public string gazeState;
            public float directPercentage;
            public bool isRightArmReaching;
            public bool isLeftArmReaching;
            public float torsoTilt;
            public bool isFallReflexActive;
            public float lookAtWeight;
        }

        public static string TriggerLiveReachHighFive()
        {
            var avatar = GameObject.Find("AiChan_Avatar");
            if (avatar == null) return "ERROR: Avatar not found";

            var reacher = avatar.GetComponent<LimbTargetReacher>();
            var anchor = GameObject.Find("Anchor_HighFive")?.GetComponent<AffordanceAnchor>();
            if (reacher == null || anchor == null) return "ERROR: Components missing";

            bool ok = reacher.ReachTarget(HumanBodyBones.RightHand, anchor);
            return ok ? "REACH_STARTED" : "REACH_FAILED";
        }

        public static string TriggerLiveClearReach()
        {
            var avatar = GameObject.Find("AiChan_Avatar");
            if (avatar == null) return "ERROR: Avatar not found";

            var reacher = avatar.GetComponent<LimbTargetReacher>();
            if (reacher == null) return "ERROR: Reacher missing";

            reacher.ClearAllReaches();
            return "REACH_CLEARED";
        }

        public static string InspectHierarchyAndTransforms()
        {
            var avatar = GameObject.Find("AiChan_Avatar");
            if (avatar == null) return "ERROR: Avatar not found";

            var anim = avatar.GetComponent<Animator>();
            if (anim == null) return "ERROR: Animator not found";

            var rUpper = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rLower = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);

            var lUpper = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lLower = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);

            var chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);

            var highFive = GameObject.Find("Anchor_HighFive");
            var handHold = GameObject.Find("Anchor_HandHold");
            var player = PlayerController.Instance != null ? PlayerController.Instance : UnityEngine.Object.FindFirstObjectByType<PlayerController>();

            float rL1 = rUpper != null && rLower != null ? Vector3.Distance(rUpper.position, rLower.position) : 0;
            float rL2 = rLower != null && rHand != null ? Vector3.Distance(rLower.position, rHand.position) : 0;

            float lL1 = lUpper != null && lLower != null ? Vector3.Distance(lUpper.position, lLower.position) : 0;
            float lL2 = lLower != null && lHand != null ? Vector3.Distance(lLower.position, lHand.position) : 0;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Avatar pos={avatar.transform.position}, fwd={avatar.transform.forward}, euler={avatar.transform.eulerAngles}");
            if (hips != null) sb.AppendLine($"Hips pos={hips.position}");
            if (chest != null) sb.AppendLine($"Chest pos={chest.position}");
            if (rUpper != null) sb.AppendLine($"RightUpper={rUpper.position}, rL1={rL1:F3}, RightLower={rLower.position}, rL2={rL2:F3}, RightHand={rHand.position}, TotalR={rL1 + rL2:F3}");
            if (lUpper != null) sb.AppendLine($"LeftUpper={lUpper.position}, lL1={lL1:F3}, LeftLower={lLower.position}, lL2={lL2:F3}, LeftHand={lHand.position}, TotalL={lL1 + lL2:F3}");
            if (highFive != null) sb.AppendLine($"Anchor_HighFive pos={highFive.transform.position}, distFromRShoulder={(rUpper != null ? Vector3.Distance(rUpper.position, highFive.transform.position) : -1):F3}");
            if (handHold != null) sb.AppendLine($"Anchor_HandHold pos={handHold.transform.position}, distFromLShoulder={(lUpper != null ? Vector3.Distance(lUpper.position, handHold.transform.position) : -1):F3}");
            if (player != null) sb.AppendLine($"Player pos={player.transform.position}, gazeTarget={(player.GazeTarget != null ? player.GazeTarget.position.ToString() : "null")}");

            Debug.Log($"[HierarchyInspection]\n{sb}");
            return sb.ToString();
        }
    }
}
#endif
