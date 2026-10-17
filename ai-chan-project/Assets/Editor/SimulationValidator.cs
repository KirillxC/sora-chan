#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AiChan.Core;
using AiChan.Spinal;
using AiChan.Thalamus;
using AiChan.Limbic;
using AiChan.Cortex;

namespace AiChan.Editor
{
    public static class SimulationValidator
    {
        private static readonly List<string> CapturedErrors = new List<string>();

        [MenuItem("AI-Chan/Validate Physics and Console")]
        public static void ValidatePlayModePhysicsAndConsole()
        {
            CapturedErrors.Clear();
            Application.logMessageReceived += OnLogMessage;

            try
            {
                Debug.Log("[Validator] Starting comprehensive automated physics, input, and intent validation...");

                // 1. Open Scene
                string scenePath = "Assets/Scenes/RoomScene.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Fail($"Scene {scenePath} could not be loaded!");
                    return;
                }
                Debug.Log("[Validator] RoomScene loaded successfully.");

                // 2. Locate Avatar
                var avatar = GameObject.Find("AiChan_Avatar");
                if (avatar == null)
                {
                    Fail("AiChan_Avatar GameObject not found in RoomScene!");
                    return;
                }

                // 3. Check 4-Level Architecture Components
                var ragdoll = avatar.GetComponent<ActiveRagdollController>();
                if (ragdoll == null) { Fail("ActiveRagdollController missing!"); return; }
                if (ragdoll.Muscles.Count < 10) { Fail($"ActiveRagdollController has only {ragdoll.Muscles.Count} joints (expected >= 10)!"); return; }

                var thalamus = avatar.GetComponent<ThalamusSensor>();
                if (thalamus == null) { Fail("ThalamusSensor missing!"); return; }

                var limbic = avatar.GetComponent<LimbicController>();
                if (limbic == null) { Fail("LimbicController missing!"); return; }

                var cortex = avatar.GetComponent<CortexBridgeClient>();
                if (cortex == null) { Fail("CortexBridgeClient missing!"); return; }

                var dragger = avatar.GetComponent<PhysicalLimbDragger>();
                if (dragger == null) { Fail("PhysicalLimbDragger missing!"); return; }

                Debug.Log($"[Validator] All 4-level decoupled architecture components verified on {avatar.name}.");

                // 4. Verify Zero Duplicate Colliders on Avatar Bones
                var cols = avatar.GetComponentsInChildren<Collider>();
                var boneDict = new Dictionary<GameObject, List<Collider>>();
                foreach (var c in cols)
                {
                    if (!boneDict.ContainsKey(c.gameObject)) boneDict[c.gameObject] = new List<Collider>();
                    boneDict[c.gameObject].Add(c);
                }
                foreach (var kvp in boneDict)
                {
                    if (kvp.Value.Count > 1)
                    {
                        Fail($"Duplicate colliders detected on {kvp.Key.name} ({kvp.Value.Count} colliders)!");
                        return;
                    }
                }
                Debug.Log("[Validator] Zero duplicate colliders verified across all bones.");

                // 5. Verify Zero Unignored Penetration Overlaps at Rest
                ragdoll.SetHipsKinematic(true);
                ragdoll.SetupCollisionIgnores();
                ragdoll.SetNaturalIdlePose();

                int overlapCount = 0;
                for (int i = 0; i < cols.Length; i++)
                {
                    for (int j = i + 1; j < cols.Length; j++)
                    {
                        var c1 = cols[i];
                        var c2 = cols[j];
                        if (Physics.GetIgnoreCollision(c1, c2)) continue;

                        if (Physics.ComputePenetration(c1, c1.transform.position, c1.transform.rotation,
                                                       c2, c2.transform.position, c2.transform.rotation,
                                                       out Vector3 dir, out float dist))
                        {
                            if (dist > 0.005f) // > 5mm overlap
                            {
                                Debug.LogWarning($"[Validator] Unignored collision overlap: {c1.name} <-> {c2.name} ({dist * 1000f:F1}mm)");
                                overlapCount++;
                            }
                        }
                    }
                }
                if (overlapCount > 0)
                {
                    Fail($"Found {overlapCount} unignored collision overlaps on humanoid rig!");
                    return;
                }
                Debug.Log("[Validator] Zero unignored collision overlaps verified.");

                // 6. Check Joint Limits and Coordinate Frames
                var anim = avatar.GetComponent<Animator>();
                var leftKneeT = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                var leftElbowT = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                if (leftKneeT != null)
                {
                    var joint = leftKneeT.GetComponent<ConfigurableJoint>();
                    if (joint == null || joint.lowAngularXLimit.limit < -1f)
                    {
                        Fail("Left knee joint limit misconfigured (must prevent forward hyperextension)!");
                        return;
                    }
                }
                if (leftElbowT != null)
                {
                    var joint = leftElbowT.GetComponent<ConfigurableJoint>();
                    if (joint == null || joint.lowAngularXLimit.limit < -1f)
                    {
                        Fail("Left elbow joint limit misconfigured (must prevent backward hyperextension)!");
                        return;
                    }
                }

                // 7. Active Physics Simulation with Muscle Dynamics & Look-At (100 steps = 2.0s)
                var hipsT = anim.GetBoneTransform(HumanBodyBones.Hips);
                var headT = anim.GetBoneTransform(HumanBodyBones.Head);
                var leftFootT = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                var rightFootT = anim.GetBoneTransform(HumanBodyBones.RightFoot);

                Vector3 startHipsPos = hipsT.position;
                Vector3 startHeadPos = headT.position;

                limbic.EnsureInitialized();
                cortex.EnsureInitialized();

                // Point look-at target towards main camera
                Vector3 camLookTarget = new Vector3(0f, 1.45f, -2.2f);
                limbic.SetLookAtTarget(camLookTarget);

                Physics.simulationMode = SimulationMode.Script;
                for (int step = 0; step < 100; step++)
                {
                    limbic.Step();
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);

                    if (float.IsNaN(hipsT.position.x) || float.IsNaN(hipsT.position.y) || float.IsNaN(hipsT.position.z))
                    {
                        Fail($"Physics exploded: NaN detected in hips position at step {step}!");
                        return;
                    }

                    float footDist = Vector3.Distance(leftFootT.position, rightFootT.position);
                    if (footDist > 0.8f)
                    {
                        Fail($"Legs tangling / exploding: Foot separation {footDist:F2}m at step {step}!");
                        return;
                    }
                }

                float hipsDelta = Vector3.Distance(hipsT.position, startHipsPos);
                if (hipsDelta > 0.05f)
                {
                    Fail($"Avatar hips drifted too far: delta={hipsDelta:F3}m (expected < 0.05m with pinned hips)!");
                    return;
                }

                if (headT.position.y < 1.30f)
                {
                    Fail($"Avatar collapsed: Head height is {headT.position.y:F2}m (expected >= 1.30m)!");
                    return;
                }

                // Verify Head Gaze points at camera target
                Vector3 targetDir = (camLookTarget - headT.position).normalized;
                float headGazeAngle = Vector3.Angle(headT.up, targetDir);
                if (headGazeAngle > 10f)
                {
                    Fail($"Procedural head look-at misaligned: angle to camera={headGazeAngle:F2}° (expected < 10°)!");
                    return;
                }
                Debug.Log($"[Validator] Active physics simulation: 100 steps completed stably. Hips delta={hipsDelta:F3}m, Head height={headT.position.y:F2}m, Camera gaze angle={headGazeAngle:F2}°.");

                // Test Side Look-At (40 steps)
                Vector3 sideLookTarget = new Vector3(-1.0f, 1.45f, -2.2f);
                limbic.SetLookAtTarget(sideLookTarget);
                for (int step = 0; step < 40; step++)
                {
                    limbic.Step();
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }
                Vector3 sideTargetDir = (sideLookTarget - headT.position).normalized;
                float sideGazeAngle = Vector3.Angle(headT.up, sideTargetDir);
                if (sideGazeAngle > 10f)
                {
                    Fail($"Procedural side look-at misaligned: angle to side target={sideGazeAngle:F2}° (expected < 10°)!");
                    return;
                }
                Debug.Log($"[Validator] Side look-at verified: gaze angle={sideGazeAngle:F2}°.");

                // Reset look-at back to camera
                limbic.SetLookAtTarget(camLookTarget);
                for (int step = 0; step < 20; step++)
                {
                    limbic.Step();
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                // 8. Test PhysicalLimbDragger & Muscle Spring-Back
                var lHandT = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                var handRb = lHandT.GetComponent<Rigidbody>();
                Vector3 handRestPos = lHandT.position;

                dragger.AttachSpring(handRb, lHandT.position);
                if (!dragger.IsDragging)
                {
                    Fail("PhysicalLimbDragger.IsDragging should be true after AttachSpring!");
                    return;
                }
                Vector3 dragTarget = handRestPos + new Vector3(0f, 0.2f, -0.3f);

                for (int step = 0; step < 40; step++)
                {
                    limbic.Step();
                    ragdoll.StepMuscles();
                    dragger.MoveAnchor(Vector3.Lerp(handRestPos, dragTarget, (step + 1) / 40f));
                    Physics.Simulate(0.02f);
                }

                float draggedDist = Vector3.Distance(handRestPos, lHandT.position);
                if (draggedDist < 0.10f)
                {
                    Fail($"PhysicalLimbDragger failed to displace limb: displaced {draggedDist:F3}m!");
                    return;
                }

                // Release spring and test spring-back
                dragger.DetachSpring();
                if (dragger.IsDragging)
                {
                    Fail("PhysicalLimbDragger.IsDragging should be false after DetachSpring!");
                    return;
                }
                for (int step = 0; step < 40; step++)
                {
                    limbic.Step();
                    ragdoll.StepMuscles();
                    Physics.Simulate(0.02f);
                }

                float residualOffset = Vector3.Distance(handRestPos, lHandT.position);
                if (residualOffset > 0.05f)
                {
                    Fail($"Active ragdoll failed to restore pose after limb drag: residual offset={residualOffset:F3}m (expected < 0.05m)!");
                    return;
                }
                Debug.Log($"[Validator] PhysicalLimbDragger spring drag & recovery verified: dragged={draggedDist:F3}m, recovery offset={residualOffset:F3}m.");

                // 9. Test Level 2 Thalamus Touch & Telemetry
                thalamus.TriggerTouch(BodyZone.HeadTop, TouchGesture.Stroke, 1.0f);
                if (!thalamus.TryPopRecentTouch(out var zoneStr, out var gestureStr, out var intensity))
                {
                    Fail("Thalamus touch event not captured!");
                    return;
                }
                if (zoneStr != "HeadTop" || gestureStr != "Stroke")
                {
                    Fail($"Thalamus touch event corrupted: zone={zoneStr}, gesture={gestureStr}!");
                    return;
                }
                Debug.Log("[Validator] Level 2 Thalamus Touch Trigger & Pop verified.");

                // 10. Test Level 3 Intent Reception & Facial Blendshape (including neutral-to-joy switch)
                limbic.SetEmotion("neutral");
                limbic.Step();
                int neutralShapeIdx = -1;
                int joyShapeIdx = -1;
                if (limbic.FaceRenderer != null && limbic.FaceRenderer.sharedMesh != null)
                {
                    for (int bi = 0; bi < limbic.FaceRenderer.sharedMesh.blendShapeCount; bi++)
                    {
                        string name = limbic.FaceRenderer.sharedMesh.GetBlendShapeName(bi).ToLowerInvariant();
                        if (name.Contains("neutral") && neutralShapeIdx < 0) neutralShapeIdx = bi;
                        if (name.Contains("joy") && joyShapeIdx < 0) joyShapeIdx = bi;
                    }
                }

                if (neutralShapeIdx >= 0 && limbic.FaceRenderer.GetBlendShapeWeight(neutralShapeIdx) < 50f)
                {
                    Fail("Neutral facial blendshape not activated on SetEmotion('neutral')!");
                    return;
                }

                limbic.SetEmotion("joy", 0.75f);
                limbic.Step();
                if (limbic.CurrentState.blush < 0.7f)
                {
                    Fail($"Limbic blush not updated: {limbic.CurrentState.blush} (expected >= 0.7)!");
                    return;
                }
                if (neutralShapeIdx >= 0 && limbic.FaceRenderer.GetBlendShapeWeight(neutralShapeIdx) > 1f)
                {
                    Fail("Neutral blendshape was NOT reset to 0 after switching to joy!");
                    return;
                }
                if (joyShapeIdx >= 0 && limbic.FaceRenderer.GetBlendShapeWeight(joyShapeIdx) < 50f)
                {
                    Fail("Joy blendshape was not activated after switching from neutral!");
                    return;
                }
                Debug.Log("[Validator] Level 3 Limbic Intent & Facial Blendshapes (and emotion switching) verified.");

                // 11. Test Level 4 Cortex Bridge (Direct Intent & Live Connection)
                cortex.ApplyCortexResponse(new CortexOutboundResponse
                {
                    speech = "Привет, я готова к игре!",
                    inner_thought = "Проверяю системы.",
                    motor_intent = new MotorIntent
                    {
                        action = "look_at",
                        targetId = "user_head",
                        target_coordinates = new Vector3(0f, 1.45f, -2.2f),
                        navigation_intent = "approach_player",
                        facial_expression = "joy",
                        blush = 0.8f
                    },
                    emotion_delta = new AffectiveState { valence = 0.3f, blush = 0.2f }
                });

                if (cortex.LastReceivedAction != "look_at" || cortex.LastNavigationIntent != "approach_player" || cortex.LastSpeech != "Привет, я готова к игре!")
                {
                    Fail($"CortexBridgeClient direct intent application failed: action={cortex.LastReceivedAction}, nav={cortex.LastNavigationIntent}!");
                    return;
                }
                Debug.Log("[Validator] Level 4 Cortex Bridge Direct Intent Application verified.");

                // Test Live WebSocket if server reachable
                cortex.autoConnect = false;
                cortex.ConnectToCortex();
                float connectWait = Time.realtimeSinceStartup;
                while (!cortex.IsConnected && (Time.realtimeSinceStartup - connectWait) < 3.0f)
                {
                    System.Threading.Thread.Sleep(50);
                }

                if (cortex.IsConnected)
                {
                    int initialCount = cortex.ReceivedIntentsCount;
                    cortex.EmitTelemetry();
                    float waitStart = Time.realtimeSinceStartup;
                    while (cortex.ReceivedIntentsCount == initialCount && (Time.realtimeSinceStartup - waitStart) < 3.0f)
                    {
                        System.Threading.Thread.Sleep(50);
                        cortex.Step();
                    }

                    if (cortex.ReceivedIntentsCount == initialCount)
                    {
                        Fail("Live WebSocket connected but received 0 intents from Python Cortex within timeout!");
                        return;
                    }

                    // Test live touch interaction over WebSocket
                    thalamus.TriggerTouch(BodyZone.HeadTop, TouchGesture.Stroke, 1.0f);
                    cortex.Step(); // Immediately emits touch packet via HasPendingTouch
                    int preTouchCount = cortex.ReceivedIntentsCount;
                    waitStart = Time.realtimeSinceStartup;
                    while ((cortex.LastReceivedAction != "lean_into_hand" || cortex.ReceivedIntentsCount <= preTouchCount) && (Time.realtimeSinceStartup - waitStart) < 3.0f)
                    {
                        System.Threading.Thread.Sleep(50);
                        cortex.Step();
                    }

                    if (cortex.LastReceivedAction != "lean_into_hand" || cortex.ReceivedIntentsCount <= preTouchCount)
                    {
                        Fail($"Live WebSocket touch reaction failed: action='{cortex.LastReceivedAction}', receivedIntents={cortex.ReceivedIntentsCount} (expected 'lean_into_hand' with count > {preTouchCount})!");
                        return;
                    }

                    Debug.Log($"[Validator] Level 4 Cortex Live WebSocket verified: connected=true, receivedIntents={cortex.ReceivedIntentsCount}, lastAction='{cortex.LastReceivedAction}', lastNav='{cortex.LastNavigationIntent}', speech='{cortex.LastSpeech}'.");
                    cortex.Disconnect();
                }
                else
                {
                    Debug.Log("[Validator] Live WebSocket test skipped (server not running during standalone validator run).");
                }

                // 12. Check for Console Errors
                if (CapturedErrors.Count > 0)
                {
                    Fail($"Console captured {CapturedErrors.Count} runtime error(s):\n" + string.Join("\n", CapturedErrors));
                    return;
                }

                Debug.Log("=== [VALIDATION PASSED] All physics, input, and intent criteria SATISFIED with 0 errors! ===");
            }
            finally
            {
                Application.logMessageReceived -= OnLogMessage;
                Physics.simulationMode = SimulationMode.FixedUpdate;
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            {
                CapturedErrors.Add($"[{type}] {condition}\n{stackTrace}");
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[Validator FAILED] {message}");
            EditorApplication.Exit(1);
        }
    }
}
#endif
