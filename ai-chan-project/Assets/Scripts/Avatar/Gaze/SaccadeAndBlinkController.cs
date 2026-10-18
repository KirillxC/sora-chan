using System;
using UnityEngine;
using UniVRM10;
using VRM;
using AiChan.Avatar.Expressiveness;

namespace AiChan.Avatar.Gaze
{
    /// <summary>
    /// Handles procedural natural blinking and saccadic eye shifts for VRM avatars (UniVRM 1.0 and 0.x fallback).
    /// </summary>
    [DisallowMultipleComponent]
    public class SaccadeAndBlinkController : MonoBehaviour
    {
        [Header("Blink Settings")]
        [Tooltip("Enable natural automatic blinking.")]
        [SerializeField] private bool _enableBlink = true;

        [Tooltip("Minimum interval between blinks in seconds.")]
        [SerializeField, Range(0.5f, 10f)] private float _minBlinkInterval = 2.5f;

        [Tooltip("Maximum interval between blinks in seconds.")]
        [SerializeField, Range(1f, 15f)] private float _maxBlinkInterval = 6.0f;

        [Tooltip("Duration of blink closing phase in seconds.")]
        [SerializeField, Range(0.02f, 0.2f)] private float _blinkClosingDuration = 0.05f;

        [Tooltip("Duration of blink holding phase in seconds.")]
        [SerializeField, Range(0f, 0.1f)] private float _blinkHoldDuration = 0.02f;

        [Tooltip("Duration of blink opening phase in seconds.")]
        [SerializeField, Range(0.05f, 0.3f)] private float _blinkOpeningDuration = 0.12f;

        [Header("Saccade Settings")]
        [Tooltip("Enable natural rapid micro-shifts of eye focus.")]
        [SerializeField] private bool _enableSaccades = true;

        [Tooltip("Minimum interval between saccades in seconds.")]
        [SerializeField, Range(0.1f, 2f)] private float _minSaccadeInterval = 0.2f;

        [Tooltip("Maximum interval between saccades in seconds.")]
        [SerializeField, Range(0.5f, 4f)] private float _maxSaccadeInterval = 1.5f;

        [Tooltip("Maximum horizontal angular offset in degrees.")]
        [SerializeField, Range(0f, 10f)] private float _saccadeRadiusYaw = 2.5f;

        [Tooltip("Maximum vertical angular offset in degrees.")]
        [SerializeField, Range(0f, 8f)] private float _saccadeRadiusPitch = 1.8f;

        [Tooltip("Speed multiplier for saccade interpolation.")]
        [SerializeField, Range(5f, 40f)] private float _saccadeSpeed = 22f;

        private Vrm10Instance _vrm10Instance;
        private VRMBlendShapeProxy _vrm0XProxy;

        // Blink state
        private enum BlinkPhase
        {
            Waiting,
            Closing,
            Holding,
            Opening
        }

        private BlinkPhase _currentBlinkPhase = BlinkPhase.Waiting;
        private float _blinkTimer;
        private float _nextBlinkDelay;
        private float _currentBlinkWeight = 0f;

        // Saccade state
        private float _saccadeTimer;
        private float _nextSaccadeDelay;
        private Vector2 _currentSaccadeOffset = Vector2.zero;
        private Vector2 _targetSaccadeOffset = Vector2.zero;

        /// <summary>
        /// Current normalized blink weight (0..1).
        /// </summary>
        public float CurrentBlinkWeight => _currentBlinkWeight;

        /// <summary>
        /// Current saccade angular offset in degrees (X: Yaw, Y: Pitch).
        /// </summary>
        public Vector2 CurrentSaccadeOffset => _currentSaccadeOffset;

        public bool EnableBlink
        {
            get => _enableBlink;
            set => _enableBlink = value;
        }

        public bool EnableSaccades
        {
            get => _enableSaccades;
            set => _enableSaccades = value;
        }

        public bool IsBlinking => _currentBlinkPhase != BlinkPhase.Waiting || _currentBlinkWeight > 0.001f;

        private void Awake()
        {
            FindVrmComponents();
            ResetBlinkTimer();
            ResetSaccadeTimer();
        }

        private void OnEnable()
        {
            if (_vrm10Instance == null && _vrm0XProxy == null)
            {
                FindVrmComponents();
            }
        }

        public void FindVrmComponents()
        {
            _vrm10Instance = GetComponent<Vrm10Instance>();
            if (_vrm10Instance == null)
            {
                _vrm10Instance = GetComponentInParent<Vrm10Instance>();
            }

            _vrm0XProxy = GetComponent<VRMBlendShapeProxy>();
            if (_vrm0XProxy == null)
            {
                _vrm0XProxy = GetComponentInParent<VRMBlendShapeProxy>();
            }
        }

        public void Step(float deltaTime)
        {
            UpdateBlink(deltaTime);
            UpdateSaccades(deltaTime);
            ApplyBlinkWeight(_currentBlinkWeight);
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        private void LateUpdate()
        {
            ApplyBlinkWeight(_currentBlinkWeight);
        }

        #region Blinking Logic

        private void ResetBlinkTimer()
        {
            _blinkTimer = 0f;
            _nextBlinkDelay = UnityEngine.Random.Range(_minBlinkInterval, _maxBlinkInterval);
            _currentBlinkPhase = BlinkPhase.Waiting;
        }

        private void UpdateBlink(float deltaTime)
        {
            if (!_enableBlink)
            {
                if (_currentBlinkWeight > 0f)
                {
                    _currentBlinkWeight = Mathf.MoveTowards(_currentBlinkWeight, 0f, deltaTime * 8f);
                }
                return;
            }

            _blinkTimer += deltaTime;

            switch (_currentBlinkPhase)
            {
                case BlinkPhase.Waiting:
                    _currentBlinkWeight = 0f;
                    if (_blinkTimer >= _nextBlinkDelay)
                    {
                        _currentBlinkPhase = BlinkPhase.Closing;
                        _blinkTimer = 0f;
                    }
                    break;

                case BlinkPhase.Closing:
                    if (_blinkClosingDuration > 0.0001f)
                    {
                        _currentBlinkWeight = Mathf.Clamp01(_blinkTimer / _blinkClosingDuration);
                    }
                    else
                    {
                        _currentBlinkWeight = 1f;
                    }

                    if (_blinkTimer >= _blinkClosingDuration)
                    {
                        _currentBlinkWeight = 1f;
                        _currentBlinkPhase = BlinkPhase.Holding;
                        _blinkTimer = 0f;
                    }
                    break;

                case BlinkPhase.Holding:
                    _currentBlinkWeight = 1f;
                    if (_blinkTimer >= _blinkHoldDuration)
                    {
                        _currentBlinkPhase = BlinkPhase.Opening;
                        _blinkTimer = 0f;
                    }
                    break;

                case BlinkPhase.Opening:
                    if (_blinkOpeningDuration > 0.0001f)
                    {
                        float progress = Mathf.Clamp01(_blinkTimer / _blinkOpeningDuration);
                        // Smooth easing out when opening eyes
                        _currentBlinkWeight = 1f - Mathf.SmoothStep(0f, 1f, progress);
                    }
                    else
                    {
                        _currentBlinkWeight = 0f;
                    }

                    if (_blinkTimer >= _blinkOpeningDuration)
                    {
                        _currentBlinkWeight = 0f;
                        ResetBlinkTimer();
                    }
                    break;
            }
        }

        private void ApplyBlinkWeight(float weight)
        {
            if (_vrm10Instance != null && _vrm10Instance.Runtime != null && _vrm10Instance.Runtime.Expression != null)
            {
                try
                {
                    _vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Blink, weight);
                }
                catch (Exception) { }
            }
            else if (_vrm0XProxy != null)
            {
                try
                {
                    _vrm0XProxy.SetValue(BlendShapePreset.Blink, weight);
                }
                catch (Exception) { }
            }

            // Also forward to AnimeExpressivenessEngine if present (supports fallback SkinnedMeshRenderer blendshapes)
            var expressiveness = GetComponent<AnimeExpressivenessEngine>() ?? GetComponentInParent<AnimeExpressivenessEngine>();
            if (expressiveness != null)
            {
                expressiveness.SetEmotion(EmotionType.Blink, weight, 15);
            }
        }

        /// <summary>
        /// Triggers an immediate voluntary or reflexive micro-blink.
        /// </summary>
        public void ForceBlink()
        {
            _currentBlinkPhase = BlinkPhase.Closing;
            _blinkTimer = 0f;
        }

        /// <summary>
        /// Triggers a target saccade shift. Large saccades (>= 20 degrees) automatically trigger a coordinated micro-blink.
        /// </summary>
        public void TriggerSaccade(Vector2 targetOffsetDeg)
        {
            _targetSaccadeOffset = targetOffsetDeg;
            if (targetOffsetDeg.magnitude >= 20.0f)
            {
                ForceBlink();
            }
        }

        #endregion

        #region Saccades Logic

        private void ResetSaccadeTimer()
        {
            _saccadeTimer = 0f;
            _nextSaccadeDelay = UnityEngine.Random.Range(_minSaccadeInterval, _maxSaccadeInterval);
        }

        private void UpdateSaccades(float deltaTime)
        {
            if (!_enableSaccades)
            {
                _targetSaccadeOffset = Vector2.zero;
                _currentSaccadeOffset = Vector2.MoveTowards(_currentSaccadeOffset, Vector2.zero, deltaTime * _saccadeSpeed);
                return;
            }

            _saccadeTimer += deltaTime;
            if (_saccadeTimer >= _nextSaccadeDelay)
            {
                ResetSaccadeTimer();

                // Generate random point inside elliptical bounds
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle;
                _targetSaccadeOffset = new Vector2(
                    randomCircle.x * _saccadeRadiusYaw,
                    randomCircle.y * _saccadeRadiusPitch
                );

                // Small chance of center re-fixation
                if (UnityEngine.Random.value < 0.25f)
                {
                    _targetSaccadeOffset = Vector2.zero;
                }
            }

            // Rapid saccadic movement towards target offset
            _currentSaccadeOffset = Vector2.Lerp(_currentSaccadeOffset, _targetSaccadeOffset, deltaTime * _saccadeSpeed);
        }

        /// <summary>
        /// Calculates world target offset position given camera/head distance for 3D tracking targets.
        /// </summary>
        public Vector3 GetWorldSaccadeOffset(Transform referenceTransform, float distance)
        {
            if (!_enableSaccades || referenceTransform == null)
            {
                return Vector3.zero;
            }

            float yawRad = _currentSaccadeOffset.x * Mathf.Deg2Rad;
            float pitchRad = _currentSaccadeOffset.y * Mathf.Deg2Rad;

            float horizontalDistance = Mathf.Tan(yawRad) * distance;
            float verticalDistance = Mathf.Tan(pitchRad) * distance;

            return referenceTransform.right * horizontalDistance + referenceTransform.up * verticalDistance;
        }

        #endregion
    }
}
