using System;
using UnityEngine;
using UniVRM10;
using VRM;
using AiChan.Avatar.Expressiveness;

namespace AiChan.Avatar.Gaze
{
    /// <summary>
    /// Advanced gaze controller managing neck & head rotation, natural head tilt,
    /// saccades, and eye direction targeting for AI-Chan VRM avatar.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Runs before VRM10Instance (11000)
    public class AvatarLookAtController : MonoBehaviour
    {
        [Header("Target Tracking")]
        [Tooltip("Target transform to look at. Defaults to Camera.main if null.")]
        [SerializeField] private Transform _target;

        [Tooltip("Auto-acquire Camera.main if no target is assigned.")]
        [SerializeField] private bool _autoAssignMainCamera = true;

        [Tooltip("World offset applied to the target position.")]
        [SerializeField] private Vector3 _targetOffset = Vector3.zero;

        [Header("Weight & Limits")]
        [Tooltip("Overall gaze tracking influence weight (0 = fully disabled, 1 = fully active).")]
        [SerializeField, Range(0f, 1f)] private float _lookAtWeight = 1f;

        [Tooltip("Maximum horizontal yaw angle (degrees) relative to body forward.")]
        [SerializeField, Range(20f, 85f)] private float _maxHorizontalAngle = 60f;

        [Tooltip("Maximum upward pitch angle (degrees).")]
        [SerializeField, Range(10f, 60f)] private float _maxPitchUpAngle = 35f;

        [Tooltip("Maximum downward pitch angle (degrees).")]
        [SerializeField, Range(10f, 60f)] private float _maxPitchDownAngle = 45f;

        [Header("Rotation Distribution")]
        [Tooltip("Fraction of head/neck rotation allocated to the neck bone (remainder goes to head).")]
        [SerializeField, Range(0f, 1f)] private float _neckRotationRatio = 0.35f;

        [Tooltip("Smoothing speed for neck and head rotation.")]
        [SerializeField, Range(1f, 25f)] private float _headTurnSpeed = 8f;

        [Header("Head Tilt (Roll)")]
        [Tooltip("Enable subtle head tilt curiosity/focus micro-movements.")]
        [SerializeField] private bool _enableHeadTilt = true;

        [Tooltip("Maximum subtle tilt angle in degrees.")]
        [SerializeField, Range(0f, 15f)] private float _maxTiltAngle = 6.0f;

        [Tooltip("Smoothing speed when tilting head.")]
        [SerializeField, Range(0.2f, 5f)] private float _tiltSpeed = 1.2f;

        [Tooltip("Minimum time between changing head tilt bias in seconds.")]
        [SerializeField, Range(1f, 10f)] private float _minTiltInterval = 3f;

        [Tooltip("Maximum time between changing head tilt bias in seconds.")]
        [SerializeField, Range(2f, 20f)] private float _maxTiltInterval = 8f;

        [Header("Bone References (Optional: Auto-detected from Animator)")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _neckBone;
        [SerializeField] private Transform _headBone;

        [Header("Auxiliary Components")]
        [SerializeField] private SaccadeAndBlinkController _saccadeAndBlink;

        private Vrm10Instance _vrm10Instance;
        private VRMLookAtHead _vrm0XLookAtHead;

        // Rotation tracking state
        private Quaternion _initialNeckLocalRotation = Quaternion.identity;
        private Quaternion _initialHeadLocalRotation = Quaternion.identity;

        private Quaternion _currentNeckRotation = Quaternion.identity;
        private Quaternion _currentHeadRotation = Quaternion.identity;

        // Head tilt state
        private float _currentTiltAngle = 0f;
        private float _targetTiltAngle = 0f;
        private float _tiltTimer = 0f;
        private float _nextTiltInterval = 4f;

        // Smoothed target position
        private Vector3 _smoothedTargetPosition;
        private bool _isFirstUpdate = true;

        #region Public Properties

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        public float LookAtWeight
        {
            get => _lookAtWeight;
            set => _lookAtWeight = Mathf.Clamp01(value);
        }

        public bool EnableHeadTilt
        {
            get => _enableHeadTilt;
            set => _enableHeadTilt = value;
        }

        public Vector3 SmoothedTargetPosition => _smoothedTargetPosition;

        #endregion

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            ResolveTarget();
            if (_target != null)
            {
                _smoothedTargetPosition = _target.position + _targetOffset;
            }
            else
            {
                _smoothedTargetPosition = transform.position + transform.forward * 3f + Vector3.up * 1.5f;
            }
            ResetTiltTimer();
        }

        private void OnEnable()
        {
            InitializeComponents();
        }

        public void InitializeComponents()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                if (_animator == null)
                {
                    _animator = GetComponentInParent<Animator>();
                }
            }

            if (_animator != null && _animator.isHuman)
            {
                if (_neckBone == null)
                {
                    _neckBone = _animator.GetBoneTransform(HumanBodyBones.Neck);
                }
                if (_headBone == null)
                {
                    _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
                }
            }

            if (_neckBone != null)
            {
                _initialNeckLocalRotation = _neckBone.localRotation;
                _currentNeckRotation = _neckBone.localRotation;
            }

            if (_headBone != null)
            {
                _initialHeadLocalRotation = _headBone.localRotation;
                _currentHeadRotation = _headBone.localRotation;
            }

            if (_saccadeAndBlink == null)
            {
                _saccadeAndBlink = GetComponent<SaccadeAndBlinkController>();
                if (_saccadeAndBlink == null)
                {
                    _saccadeAndBlink = gameObject.AddComponent<SaccadeAndBlinkController>();
                }
            }

            // Detect VRM components
            _vrm10Instance = GetComponent<Vrm10Instance>();
            if (_vrm10Instance == null)
            {
                _vrm10Instance = GetComponentInParent<Vrm10Instance>();
            }

            _vrm0XLookAtHead = GetComponent<VRMLookAtHead>();
            if (_vrm0XLookAtHead == null)
            {
                _vrm0XLookAtHead = GetComponentInParent<VRMLookAtHead>();
            }
        }

        private void ResolveTarget()
        {
            if (_target == null && _autoAssignMainCamera)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    _target = mainCam.transform;
                }
            }
        }

        private void ResetTiltTimer()
        {
            _tiltTimer = 0f;
            _nextTiltInterval = UnityEngine.Random.Range(_minTiltInterval, _maxTiltInterval);
        }

        private void Update()
        {
            ResolveTarget();
            UpdateHeadTilt(Time.deltaTime);
        }

        private void UpdateHeadTilt(float deltaTime)
        {
            if (!_enableHeadTilt)
            {
                _targetTiltAngle = 0f;
                _currentTiltAngle = Mathf.MoveTowards(_currentTiltAngle, 0f, deltaTime * _tiltSpeed * 2f);
                return;
            }

            _tiltTimer += deltaTime;
            if (_tiltTimer >= _nextTiltInterval)
            {
                ResetTiltTimer();
                // Pick subtle tilt angle: bias slightly to left or right, or return to center
                float roll = UnityEngine.Random.value;
                if (roll < 0.35f)
                {
                    _targetTiltAngle = UnityEngine.Random.Range(1.5f, _maxTiltAngle);
                }
                else if (roll < 0.70f)
                {
                    _targetTiltAngle = UnityEngine.Random.Range(-_maxTiltAngle, -1.5f);
                }
                else
                {
                    _targetTiltAngle = 0f;
                }
            }

            _currentTiltAngle = Mathf.Lerp(_currentTiltAngle, _targetTiltAngle, deltaTime * _tiltSpeed);
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                ResolveTarget();
                if (_target == null) return;
            }

            Vector3 rawTargetPos = _target.position + _targetOffset;
            float distanceToTarget = Vector3.Distance(transform.position, rawTargetPos);

            // Apply saccadic micro-movements
            Vector3 saccadeOffset = Vector3.zero;
            if (_saccadeAndBlink != null && _saccadeAndBlink.EnableSaccades)
            {
                Transform gazeRef = _headBone != null ? _headBone : transform;
                saccadeOffset = _saccadeAndBlink.GetWorldSaccadeOffset(gazeRef, Mathf.Max(1.0f, distanceToTarget));
            }

            // Support deflected look-at target from UserBodyLanguageAnalyzer if present
            var bodyLang = GetComponent<UserBodyLanguageAnalyzer>() ?? GetComponentInParent<UserBodyLanguageAnalyzer>();
            if (bodyLang != null && bodyLang.IsPersonalSpaceBreached)
            {
                rawTargetPos = bodyLang.EffectiveLookAtTarget;
            }

            Vector3 effectiveTargetPos = rawTargetPos + saccadeOffset;

            if (_isFirstUpdate)
            {
                _smoothedTargetPosition = effectiveTargetPos;
                _isFirstUpdate = false;
            }
            else
            {
                _smoothedTargetPosition = Vector3.Lerp(_smoothedTargetPosition, effectiveTargetPos, Time.deltaTime * _headTurnSpeed);
            }

            // Head and neck tracking
            ApplyNeckAndHeadRotations(_smoothedTargetPosition, _lookAtWeight);

            // Update VRM Eye look at
            ApplyVrmLookAt(_smoothedTargetPosition);
        }

        private void ApplyNeckAndHeadRotations(Vector3 worldTarget, float weight)
        {
            if (_headBone == null) return;

            Transform baseReference = _neckBone != null && _neckBone.parent != null ? _neckBone.parent : transform;
            Vector3 originPos = _neckBone != null ? _neckBone.position : _headBone.position;
            Vector3 directionToTarget = worldTarget - originPos;

            if (directionToTarget.sqrMagnitude < 0.0001f) return;

            // Compute target rotation in world space looking towards target
            Quaternion targetLookRotation = Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);

            // Compute delta relative to base reference
            Quaternion relativeRotation = Quaternion.Inverse(baseReference.rotation) * targetLookRotation;
            Vector3 euler = relativeRotation.eulerAngles;
            float yaw = Mathf.DeltaAngle(0f, euler.y);
            float pitch = Mathf.DeltaAngle(0f, euler.x);

            // Clamp angles to realistic physiological bounds
            float clampedYaw = Mathf.Clamp(yaw, -_maxHorizontalAngle, _maxHorizontalAngle);
            float clampedPitch = Mathf.Clamp(pitch, -_maxPitchUpAngle, _maxPitchDownAngle);

            // Calculate rotation offsets
            float effectiveYaw = clampedYaw * weight;
            float effectivePitch = clampedPitch * weight;
            float effectiveRoll = _currentTiltAngle * weight;

            Quaternion totalLookRotation = Quaternion.Euler(effectivePitch, effectiveYaw, effectiveRoll);

            // Distribute rotation between neck and head
            float neckRatio = Mathf.Clamp01(_neckRotationRatio);
            float headRatio = 1f - neckRatio;

            Quaternion neckDelta = Quaternion.Slerp(Quaternion.identity, totalLookRotation, neckRatio);
            Quaternion headDelta = Quaternion.Slerp(Quaternion.identity, totalLookRotation, headRatio);

            if (_neckBone != null)
            {
                _neckBone.rotation = baseReference.rotation * neckDelta;
            }

            _headBone.rotation = (_neckBone != null ? _neckBone.rotation : baseReference.rotation) * headDelta;
        }

        private void ApplyVrmLookAt(Vector3 targetWorldPosition)
        {
            // VRM 1.0 support
            if (_vrm10Instance != null && _vrm10Instance.Runtime != null)
            {
                var lookAt = _vrm10Instance.Runtime.LookAt;
                if (lookAt != null)
                {
                    lookAt.LookAtInput = new LookAtInput
                    {
                        WorldPosition = targetWorldPosition
                    };
                }
            }
            // VRM 0.x support
            else if (_vrm0XLookAtHead != null)
            {
                if (_vrm0XLookAtHead.Target != _target)
                {
                    _vrm0XLookAtHead.Target = _target;
                }
            }
        }

        /// <summary>
        /// Explicitly look at custom world position or target.
        /// </summary>
        public void SetTarget(Transform target, Vector3 offset = default)
        {
            _target = target;
            _targetOffset = offset;
        }

        private void OnDrawGizmosSelected()
        {
            if (_target != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_target.position + _targetOffset, 0.05f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + Vector3.up * 1.4f, _smoothedTargetPosition);
                Gizmos.DrawWireSphere(_smoothedTargetPosition, 0.03f);
            }
        }
    }
}
