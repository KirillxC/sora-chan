using System;
using UnityEngine;
using AiChan.Core;
using AiChan.Spinal;

namespace AiChan.Limbic
{
    /// <summary>
    /// Level 3: Limbic System & Drives.
    /// Manages affective state (valence, arousal, blush), facial blendshapes,
    /// and procedural head tilt / look-at targeting. Runs at 10-30 Hz.
    /// Preserves natural humanoid bone coordinate systems to avoid joint limit spasms.
    /// </summary>
    public class LimbicController : MonoBehaviour
    {
        [Header("Affective State")]
        public AffectiveState state = new AffectiveState
        {
            valence = 0.5f,
            arousal = 0.3f,
            dominance = 0.0f,
            blush = 0.0f,
            trust = 0.5f,
            heartRateBpm = 75f
        };

        [Header("Look-At Targeting")]
        public bool enableLookAt = true;
        public Vector3 lookAtWorldTarget = new Vector3(0f, 1.45f, -2.2f);
        public float lookAtWeight = 1.0f;
        public float headTiltAngle = 0.0f;

        [Header("Components")]
        public ActiveRagdollController ragdollController;
        public Transform headBone;

        private SkinnedMeshRenderer _faceRenderer;
        private Quaternion _baseHeadLocalRot = Quaternion.identity;

        public AffectiveState CurrentState => state;
        public SkinnedMeshRenderer FaceRenderer => _faceRenderer;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (ragdollController == null)
            {
                ragdollController = GetComponent<ActiveRagdollController>();
            }

            var anim = GetComponent<Animator>();
            if (anim != null && headBone == null)
            {
                headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }

            if (headBone != null && (_baseHeadLocalRot == Quaternion.identity || (_baseHeadLocalRot.x == 0 && _baseHeadLocalRot.y == 0 && _baseHeadLocalRot.z == 0 && _baseHeadLocalRot.w == 0)))
            {
                _baseHeadLocalRot = headBone.localRotation;
            }

            // Find face skinned mesh renderer
            if (_faceRenderer == null)
            {
                var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var r in renderers)
                {
                    if (r.name.ToLower().Contains("face") || (r.sharedMesh != null && r.sharedMesh.blendShapeCount > 5))
                    {
                        _faceRenderer = r;
                        break;
                    }
                }
            }
        }

        public void Step()
        {
            EnsureInitialized();
            UpdateProceduralHeadLook();
            UpdateFacialExpression();
        }

        private void Update()
        {
            Step();
        }

        public void SetLookAtTarget(Vector3 targetWorld)
        {
            lookAtWorldTarget = targetWorld;
            enableLookAt = true;
        }

        public void SetBlush(float blushLevel)
        {
            state.blush = Mathf.Clamp01(blushLevel);
        }

        public void SetEmotion(string expression, float blushLevel = -1f)
        {
            if (blushLevel >= 0f) state.blush = Mathf.Clamp01(blushLevel);
            ApplyExpression(expression);
        }

        public void ApplyEmotionDelta(AffectiveState delta)
        {
            state.valence = Mathf.Clamp(state.valence + delta.valence, -1f, 1f);
            state.arousal = Mathf.Clamp(state.arousal + delta.arousal, 0f, 1f);
            state.blush = Mathf.Clamp(state.blush + delta.blush, 0f, 1f);
            state.trust = Mathf.Clamp(state.trust + delta.trust, 0f, 1f);
        }

        private void UpdateProceduralHeadLook()
        {
            if (!enableLookAt || headBone == null || ragdollController == null) return;

            if (Application.isPlaying)
            {
                var lookAt = GetComponent<AiChan.Avatar.Gaze.AvatarLookAtController>();
                if (lookAt != null && lookAt.enabled) return;
            }

            Vector3 targetDir = (lookAtWorldTarget - headBone.position).normalized;
            if (targetDir.sqrMagnitude < 0.001f) return;

            Transform parent = headBone.parent != null ? headBone.parent : transform;
            // Direction to target in parent coordinate frame
            Vector3 localDir = parent.InverseTransformDirection(targetDir);

            // Compute yaw and pitch relative to forward (+Z in parent space)
            float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(localDir.y, Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z)) * Mathf.Rad2Deg;

            // Clamp to natural anatomical neck rotation limits
            yaw = Mathf.Clamp(yaw, -40f, 40f);
            pitch = Mathf.Clamp(pitch, -25f, 25f);

            // Cute anime head tilt on blush
            float roll = state.blush > 0.3f ? 10f : headTiltAngle;
            roll = Mathf.Clamp(roll, -15f, 15f);

            // In parent frame, pitch=X, yaw=Y, roll=Z
            Quaternion neckOffset = Quaternion.Euler(pitch, yaw, roll);

            // Blend with weight
            if (lookAtWeight < 0.999f)
            {
                neckOffset = Quaternion.Slerp(Quaternion.identity, neckOffset, lookAtWeight);
            }

            // Target local rotation modulates base local head posture
            if (_baseHeadLocalRot == Quaternion.identity && headBone != null)
            {
                _baseHeadLocalRot = headBone.localRotation;
            }

            Quaternion localTargetRot = neckOffset * _baseHeadLocalRot;
            ragdollController.SetMuscleTarget(HumanBodyBones.Head, localTargetRot);
        }

        private void UpdateFacialExpression()
        {
            if (_faceRenderer == null || _faceRenderer.sharedMesh == null) return;

            // Search for blush / cheek puff blendshape
            int blushIdx = FindBlendshapeIndex("skinfung");
            if (blushIdx < 0) blushIdx = FindBlendshapeIndex("fung1");
            if (blushIdx < 0) blushIdx = FindBlendshapeIndex("blush");
            if (blushIdx >= 0)
            {
                _faceRenderer.SetBlendShapeWeight(blushIdx, state.blush * 100f);
            }
        }

        public void ApplyExpression(string expr)
        {
            if (_faceRenderer == null || _faceRenderer.sharedMesh == null) return;
            string lower = expr.ToLowerInvariant();

            // Reset standard emotion shapes
            string[] emotions = { "joy", "angry", "sorrow", "fun", "surprised", "neutral" };
            foreach (var emo in emotions)
            {
                int idx = FindBlendshapeIndex(emo);
                if (idx >= 0) _faceRenderer.SetBlendShapeWeight(idx, 0f);
            }

            if (lower == "neutral" || lower == "idle")
            {
                int neutralIdx = FindBlendshapeIndex("neutral");
                if (neutralIdx >= 0) _faceRenderer.SetBlendShapeWeight(neutralIdx, 100f);
                return;
            }

            int targetIdx = FindBlendshapeIndex(lower);
            if (targetIdx >= 0)
            {
                _faceRenderer.SetBlendShapeWeight(targetIdx, 80f);
            }
        }

        private int FindBlendshapeIndex(string keyword)
        {
            if (_faceRenderer == null || _faceRenderer.sharedMesh == null) return -1;
            int count = _faceRenderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string name = _faceRenderer.sharedMesh.GetBlendShapeName(i).ToLowerInvariant();
                if (name.Contains(keyword)) return i;
            }
            return -1;
        }
    }
}
