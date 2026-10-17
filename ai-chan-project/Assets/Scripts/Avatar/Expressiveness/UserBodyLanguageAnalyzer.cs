using System;
using UnityEngine;
using AiChan.Limbic;

namespace AiChan.Avatar.Expressiveness
{
    /// <summary>
    /// Tracks distance between avatar head/eyes and the player camera.
    /// Manages personal space intrusion reflex (< 0.6m):
    /// - Induces blush / embarrassment via AnimeExpressivenessEngine
    /// - Triggers gaze aversion (deflecting look-down & away)
    /// - Hysteresis release (> 0.75m) restoring relaxed neutral state.
    /// </summary>
    public class UserBodyLanguageAnalyzer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Head or eye transform of the avatar. If null, will attempt to find HumanBodyBones.Head or self.")]
        [SerializeField] private Transform avatarHead;

        [Tooltip("Player camera. If null, defaults to Camera.main.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Expressiveness engine for blush and facial response.")]
        [SerializeField] private AnimeExpressivenessEngine expressivenessEngine;

        [Tooltip("Optional reference to LimbicController for procedural head/eye look modulation.")]
        [SerializeField] private LimbicController limbicController;

        [Header("Distance Thresholds (Meters)")]
        [Tooltip("Distance below which personal space intrusion reflex is triggered.")]
        [SerializeField] private float intrusionDistance = 0.60f;

        [Tooltip("Distance above which intrusion reflex completely relaxes (hysteresis buffer).")]
        [SerializeField] private float relaxedDistance = 0.75f;

        [Header("Reactivity & Blushing")]
        [Range(0f, 1f)]
        [Tooltip("Blush intensity applied during maximum proximity intrusion.")]
        [SerializeField] private float maxIntrusionBlush = 0.85f;

        [Tooltip("Rate of blush increase and response adaptation.")]
        [SerializeField] private float reactionDamping = 4.0f;

        [Header("Gaze Aversion (Bashfulness)")]
        [Tooltip("Deflect gaze when player intrudes too closely.")]
        [SerializeField] private bool enableGazeAversion = true;

        [Tooltip("Angular deflection downwards (degrees) when shy.")]
        [SerializeField] private float gazeDeflectPitchDegrees = 20.0f;

        [Tooltip("Angular deflection sideways (degrees) away from direct eye contact.")]
        [SerializeField] private float gazeDeflectYawDegrees = 25.0f;

        // Runtime state
        private float _currentDistance = 2.0f;
        private float _intrusionFactor = 0.0f; // 0 = relaxed, 1 = fully intruded
        private bool _isPersonalSpaceBreached = false;
        private Vector3 _originalLookAtTarget;
        private float _gazeSideSign = 1.0f; // alternate left/right aversion on successive breaches

        // Public accessors
        public float CurrentDistance => _currentDistance;
        public float IntrusionFactor => _intrusionFactor;
        public bool IsPersonalSpaceBreached => _isPersonalSpaceBreached;
        public Vector3 EffectiveLookAtTarget { get; private set; }

        private const int PROXIMITY_PRIORITY = 20;

        private void Awake()
        {
            InitializeReferences();
        }

        private void Start()
        {
            InitializeReferences();
        }

        private void InitializeReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (avatarHead == null)
            {
                var anim = GetComponent<Animator>() ?? GetComponentInParent<Animator>() ?? GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    avatarHead = anim.GetBoneTransform(HumanBodyBones.Head);
                }
                if (avatarHead == null)
                {
                    avatarHead = transform;
                }
            }

            if (expressivenessEngine == null)
            {
                expressivenessEngine = GetComponent<AnimeExpressivenessEngine>() ?? 
                                       GetComponentInParent<AnimeExpressivenessEngine>() ?? 
                                       GetComponentInChildren<AnimeExpressivenessEngine>();
            }

            if (limbicController == null)
            {
                limbicController = GetComponent<LimbicController>() ?? 
                                   GetComponentInParent<LimbicController>() ?? 
                                   GetComponentInChildren<LimbicController>();
            }
        }

        private void Update()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            Vector3 headPos = avatarHead != null ? avatarHead.position : transform.position;
            Vector3 cameraPos = playerCamera.transform.position;

            _currentDistance = Vector3.Distance(headPos, cameraPos);

            UpdateProximityState();
            UpdateFacialAndGazeReaction(headPos, cameraPos);
        }

        private void UpdateProximityState()
        {
            // Hysteresis logic
            if (!_isPersonalSpaceBreached)
            {
                if (_currentDistance < intrusionDistance)
                {
                    _isPersonalSpaceBreached = true;
                    // Randomly choose subtle left or right averted gaze
                    _gazeSideSign = (UnityEngine.Random.value > 0.5f) ? 1.0f : -1.0f;
                }
            }
            else
            {
                if (_currentDistance > relaxedDistance)
                {
                    _isPersonalSpaceBreached = false;
                }
            }

            // Calculate target intrusion factor (0.0 to 1.0)
            float targetFactor = 0f;
            if (_isPersonalSpaceBreached)
            {
                // Linearly ramp from 0 at relaxedDistance to 1 at 0.2m or closer
                targetFactor = Mathf.InverseLerp(relaxedDistance, intrusionDistance * 0.5f, _currentDistance);
                targetFactor = Mathf.Clamp01(targetFactor);
            }

            // Smooth transition
            _intrusionFactor = Mathf.MoveTowards(_intrusionFactor, targetFactor, Time.deltaTime * reactionDamping);
        }

        private void UpdateFacialAndGazeReaction(Vector3 headPos, Vector3 cameraPos)
        {
            // 1. Emotion and Blush via AnimeExpressivenessEngine
            if (expressivenessEngine != null)
            {
                if (_isPersonalSpaceBreached && _intrusionFactor > 0.1f)
                {
                    float blushAmount = Mathf.Lerp(0f, maxIntrusionBlush, _intrusionFactor);
                    expressivenessEngine.SetBlush(blushAmount, PROXIMITY_PRIORITY);

                    // Express Surprised / Shy (Fun/Joy blend or Surprised depending on proximity level)
                    if (_intrusionFactor > 0.6f)
                    {
                        expressivenessEngine.SetEmotion(EmotionType.Surprised, _intrusionFactor * 0.7f, PROXIMITY_PRIORITY);
                    }
                    else
                    {
                        expressivenessEngine.SetEmotion(EmotionType.Fun, _intrusionFactor * 0.5f, PROXIMITY_PRIORITY);
                    }
                }
                else
                {
                    expressivenessEngine.ClearEmotion(PROXIMITY_PRIORITY);
                    expressivenessEngine.SetBlush(Mathf.Lerp(0f, maxIntrusionBlush, _intrusionFactor), PROXIMITY_PRIORITY);
                }
            }

            // 2. Gaze Aversion calculation
            Vector3 directLookDir = (cameraPos - headPos).normalized;
            if (directLookDir.sqrMagnitude < 0.001f) directLookDir = avatarHead != null ? avatarHead.forward : transform.forward;

            Vector3 deflectedLookTarget = cameraPos;

            if (enableGazeAversion && _intrusionFactor > 0.05f)
            {
                // Deflect down and slightly to the side
                Vector3 rightDir = avatarHead != null ? avatarHead.right : transform.right;
                Vector3 upDir = avatarHead != null ? avatarHead.up : transform.up;

                Quaternion deflection = Quaternion.AngleAxis(-gazeDeflectPitchDegrees * _intrusionFactor, rightDir)
                                      * Quaternion.AngleAxis(gazeDeflectYawDegrees * _gazeSideSign * _intrusionFactor, upDir);

                Vector3 avertedDir = deflection * directLookDir;
                deflectedLookTarget = headPos + avertedDir * _currentDistance;
            }

            EffectiveLookAtTarget = deflectedLookTarget;

            // 3. Integrate with LimbicController if present
            if (limbicController != null)
            {
                if (_isPersonalSpaceBreached || _intrusionFactor > 0.01f)
                {
                    limbicController.SetLookAtTarget(deflectedLookTarget);
                    // Add subtle cute head tilt angle on intrusion
                    limbicController.headTiltAngle = Mathf.Lerp(0f, 8f * _gazeSideSign, _intrusionFactor);
                    limbicController.SetBlush(Mathf.Max(limbicController.state.blush, _intrusionFactor * maxIntrusionBlush));
                }
                else
                {
                    // Reset head tilt angle when comfortable
                    limbicController.headTiltAngle = 0f;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = avatarHead != null ? avatarHead.position : transform.position;
            Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(center, intrusionDistance);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(center, relaxedDistance);

            if (Application.isPlaying && _intrusionFactor > 0.01f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(center, EffectiveLookAtTarget);
                Gizmos.DrawSphere(EffectiveLookAtTarget, 0.04f);
            }
        }
    }
}
