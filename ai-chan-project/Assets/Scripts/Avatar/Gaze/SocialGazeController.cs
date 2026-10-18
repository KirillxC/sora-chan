using System;
using UnityEngine;
using AiChan.Avatar.Expressiveness;
using AiChan.Player;

namespace AiChan.Avatar.Gaze
{
    public enum SocialGazeState
    {
        DirectContact,
        IdleAversion,
        PersonalSpaceAversion
    }

    public enum AversionDirection
    {
        AsideLeft,
        AsideRight,
        DownLeft,
        DownRight,
        RoomAmbient
    }

    /// <summary>
    /// Level 1-3 Social Gaze & Visual Attention Controller.
    /// Replaces the continuous 100% direct stare with a probabilistic human-like visual attention model:
    /// - Direct eye contact with player for natural ~50-60% of time (3-5s bursts).
    /// - Idle gaze aversion / wandering (looking aside, down, into the room) for ~20-30% of time (1-2s bursts).
    /// - Instant smooth reversion to eye contact upon significant player motion or speech.
    /// - Coordinated micro-blink trigger during large saccadic eye/head shifts (> 20 degrees).
    /// - Retains personal space breach aversion (< 0.6m) with bashful blushing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public class SocialGazeController : MonoBehaviour
    {
        [Header("State Timers (Seconds)")]
        [Tooltip("Minimum direct contact duration in seconds.")]
        [SerializeField, Range(1f, 10f)] private float _minDirectDuration = 2.5f;

        [Tooltip("Maximum direct contact duration in seconds.")]
        [SerializeField, Range(2f, 15f)] private float _maxDirectDuration = 4.5f;

        [Tooltip("Minimum idle aversion duration in seconds.")]
        [SerializeField, Range(0.5f, 5f)] private float _minAversionDuration = 1.5f;

        [Tooltip("Maximum idle aversion duration in seconds.")]
        [SerializeField, Range(1f, 8f)] private float _maxAversionDuration = 2.8f;

        [Header("Aversion Angular Offsets (Degrees)")]
        [Tooltip("Horizontal deflection when looking aside.")]
        [SerializeField, Range(10f, 45f)] private float _asideYawAngle = 24.0f;

        [Tooltip("Vertical deflection when looking down.")]
        [SerializeField, Range(5f, 35f)] private float _downPitchAngle = 18.0f;

        [Tooltip("Room ambient glance yaw angle.")]
        [SerializeField, Range(15f, 55f)] private float _roomAmbientYawAngle = 32.0f;

        [Header("Personal Space Breach")]
        [Tooltip("Distance below which personal space breach aversion is enforced.")]
        [SerializeField] private float _personalSpaceDistance = 0.60f;

        [Tooltip("Distance above which personal space breach relaxes.")]
        [SerializeField] private float _personalSpaceRelaxDistance = 0.75f;

        [Header("Player Movement Sensitivity")]
        [Tooltip("Player speed threshold (m/s) that triggers immediate attention reversion.")]
        [SerializeField] private float _motionVelocityThreshold = 0.25f;

        [Tooltip("Large saccade / gaze shift threshold (degrees) that triggers micro-blink.")]
        [SerializeField] private float _largeSaccadeThresholdDegrees = 20.0f;

        [Header("Current State (Diagnostics)")]
        [SerializeField] private SocialGazeState _currentState = SocialGazeState.DirectContact;
        [SerializeField] private AversionDirection _currentAversion = AversionDirection.AsideRight;
        [SerializeField] private float _stateTimer = 0f;
        [SerializeField] private float _currentPhaseDuration = 3.5f;
        [SerializeField] private float _totalDirectContactTime = 0f;
        [SerializeField] private float _totalAversionTime = 0f;
        [SerializeField] private float _directContactRatio = 0.6f;

        [Header("Auxiliary References")]
        [SerializeField] private SaccadeAndBlinkController _saccadeAndBlink;
        [SerializeField] private UserBodyLanguageAnalyzer _bodyLanguage;
        [SerializeField] private Transform _avatarHead;

        private Vector3 _lastPlayerPosition;
        private float _lastDistanceToPlayer = -1f;
        private Vector3 _currentAvertedTarget;
        private Vector3 _lastGazeDirection = Vector3.zero;
        private bool _hasLastGazeDirection = false;
        private bool _isInitialized = false;

        public SocialGazeState CurrentState => _currentState;
        public AversionDirection CurrentAversion => _currentAversion;
        public void SetAversionDirection(AversionDirection direction) => _currentAversion = direction;
        public float DirectContactPercentage => _totalDirectContactTime + _totalAversionTime > 0.001f
            ? (_totalDirectContactTime / (_totalDirectContactTime + _totalAversionTime)) * 100f
            : 60f;

        public event Action<SocialGazeState> OnStateChanged;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
            EnterDirectContact();
        }

        public void EnsureInitialized()
        {
            if (_isInitialized) return;

            if (_saccadeAndBlink == null)
            {
                _saccadeAndBlink = GetComponent<SaccadeAndBlinkController>() ?? GetComponentInParent<SaccadeAndBlinkController>();
            }

            if (_bodyLanguage == null)
            {
                _bodyLanguage = GetComponent<UserBodyLanguageAnalyzer>() ?? GetComponentInParent<UserBodyLanguageAnalyzer>();
            }

            if (_avatarHead == null)
            {
                var anim = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
                if (anim != null && anim.isHuman)
                {
                    _avatarHead = anim.GetBoneTransform(HumanBodyBones.Head);
                }
                if (_avatarHead == null) _avatarHead = transform;
            }

            var player = PlayerController.Instance != null ? PlayerController.Instance : FindFirstObjectByType<PlayerController>();
            if (player != null && player.GazeTarget != null)
            {
                _lastPlayerPosition = player.GazeTarget.position;
                Vector3 headPos = _avatarHead != null ? _avatarHead.position : transform.position;
                _lastDistanceToPlayer = Vector3.Distance(headPos, _lastPlayerPosition);
            }
            _isInitialized = true;
        }

        public void Step(float dt)
        {
            EnsureInitialized();
            CheckPlayerReactivity(dt);
            UpdateAttentionState(dt);
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        private void CheckPlayerReactivity(float dt)
        {
            var player = PlayerController.Instance != null ? PlayerController.Instance : FindFirstObjectByType<PlayerController>();
            if (player == null) return;

            Vector3 playerPos = player.GazeTarget != null ? player.GazeTarget.position : player.transform.position;
            Vector3 headPos = _avatarHead != null ? _avatarHead.position : transform.position;
            float distanceToPlayer = Vector3.Distance(headPos, playerPos);

            // 1. Enforce personal space breach aversion when closer than 0.6m
            if (distanceToPlayer < _personalSpaceDistance)
            {
                if (_currentState != SocialGazeState.PersonalSpaceAversion)
                {
                    TransitionTo(SocialGazeState.PersonalSpaceAversion);
                }
                _lastDistanceToPlayer = distanceToPlayer;
                _lastPlayerPosition = playerPos;
                return;
            }
            else if (_currentState == SocialGazeState.PersonalSpaceAversion && distanceToPlayer > _personalSpaceRelaxDistance)
            {
                // Relax personal space breach and return to direct contact
                _lastDistanceToPlayer = distanceToPlayer;
                _lastPlayerPosition = playerPos;
                EnterDirectContact();
                return;
            }

            if (_currentState == SocialGazeState.PersonalSpaceAversion)
            {
                _lastDistanceToPlayer = distanceToPlayer;
                _lastPlayerPosition = playerPos;
                return;
            }

            // 2. Detect significant player motion or approaching
            if (_lastDistanceToPlayer < 0f)
            {
                _lastDistanceToPlayer = distanceToPlayer;
                _lastPlayerPosition = playerPos;
                return;
            }

            Vector3 playerVel = player.Velocity;
            float speed = playerVel.magnitude;
            if (dt > 0.0001f)
            {
                float frameSpeed = Vector3.Distance(playerPos, _lastPlayerPosition) / dt;
                if (frameSpeed > speed) speed = frameSpeed;
            }
            _lastPlayerPosition = playerPos;

            float approachSpeed = 0f;
            if (dt > 0.0001f && _lastDistanceToPlayer > 0f)
            {
                approachSpeed = (_lastDistanceToPlayer - distanceToPlayer) / dt;
            }
            _lastDistanceToPlayer = distanceToPlayer;

            if (speed > _motionVelocityThreshold || approachSpeed > _motionVelocityThreshold)
            {
                // Significant motion or approaching detected: immediately revert to eye contact!
                if (_currentState == SocialGazeState.IdleAversion)
                {
                    EnterDirectContact();
                }
                else if (_currentState == SocialGazeState.DirectContact)
                {
                    // Refresh direct contact duration while player is active
                    _stateTimer = 0f;
                }
            }
        }

        private void UpdateAttentionState(float dt)
        {
            _stateTimer += dt;

            if (_currentState == SocialGazeState.DirectContact)
            {
                _totalDirectContactTime += dt;
                if (_stateTimer >= _currentPhaseDuration)
                {
                    EnterIdleAversion();
                }
            }
            else if (_currentState == SocialGazeState.IdleAversion)
            {
                _totalAversionTime += dt;
                if (_stateTimer >= _currentPhaseDuration)
                {
                    EnterDirectContact();
                }
            }

            float totalTime = _totalDirectContactTime + _totalAversionTime;
            _directContactRatio = totalTime > 0.001f ? (_totalDirectContactTime / totalTime) : 0.6f;
        }

        public void EnterDirectContact()
        {
            float duration = UnityEngine.Random.Range(_minDirectDuration, _maxDirectDuration);
            if (_totalDirectContactTime + _totalAversionTime > 4f)
            {
                if (DirectContactPercentage > 56f)
                {
                    duration = UnityEngine.Random.Range(2.2f, 2.8f);
                }
                else if (DirectContactPercentage < 50f)
                {
                    duration = UnityEngine.Random.Range(Mathf.Lerp(_minDirectDuration, _maxDirectDuration, 0.5f), _maxDirectDuration);
                }
            }

            _currentPhaseDuration = duration;
            _stateTimer = 0f;
            TransitionTo(SocialGazeState.DirectContact);
        }

        public void EnterIdleAversion()
        {
            float duration = UnityEngine.Random.Range(_minAversionDuration, _maxAversionDuration);
            if (_totalDirectContactTime + _totalAversionTime > 4f)
            {
                if (DirectContactPercentage > 56f)
                {
                    duration = UnityEngine.Random.Range(2.0f, 2.8f);
                }
                else if (DirectContactPercentage < 50f)
                {
                    duration = UnityEngine.Random.Range(_minAversionDuration, Mathf.Lerp(_minAversionDuration, _maxAversionDuration, 0.5f));
                }
            }

            _currentPhaseDuration = duration;
            _stateTimer = 0f;

            // Pick randomized natural human aversion direction
            float roll = UnityEngine.Random.value;
            if (roll < 0.30f)
            {
                _currentAversion = AversionDirection.AsideRight;
            }
            else if (roll < 0.55f)
            {
                _currentAversion = AversionDirection.AsideLeft;
            }
            else if (roll < 0.75f)
            {
                _currentAversion = AversionDirection.DownRight;
            }
            else if (roll < 0.90f)
            {
                _currentAversion = AversionDirection.DownLeft;
            }
            else
            {
                _currentAversion = AversionDirection.RoomAmbient;
            }

            TransitionTo(SocialGazeState.IdleAversion);
        }

        private void TransitionTo(SocialGazeState newState)
        {
            if (_currentState != newState)
            {
                var oldState = _currentState;
                _currentState = newState;

                if (_currentState == SocialGazeState.PersonalSpaceAversion)
                {
                    var expr = GetComponent<AnimeExpressivenessEngine>() ?? GetComponentInParent<AnimeExpressivenessEngine>();
                    if (expr != null) expr.SetBlush(0.85f, 25);
                    var limbic = GetComponent<AiChan.Limbic.LimbicController>() ?? GetComponentInParent<AiChan.Limbic.LimbicController>();
                    if (limbic != null) limbic.SetBlush(0.85f);
                }
                else if (oldState == SocialGazeState.PersonalSpaceAversion)
                {
                    var expr = GetComponent<AnimeExpressivenessEngine>() ?? GetComponentInParent<AnimeExpressivenessEngine>();
                    if (expr != null) expr.SetBlush(0.0f, 25);
                    var limbic = GetComponent<AiChan.Limbic.LimbicController>() ?? GetComponentInParent<AiChan.Limbic.LimbicController>();
                    if (limbic != null) limbic.SetBlush(0.0f);
                }

                // Coordinated micro-blink trigger during attention state transitions (lifeless stare elimination)
                if (_saccadeAndBlink != null && (oldState != newState))
                {
                    _saccadeAndBlink.ForceBlink();
                }

                OnStateChanged?.Invoke(_currentState);
            }
        }

        /// <summary>
        /// External trigger: player spoke or audio activity detected.
        /// Reverts attention immediately to direct eye contact.
        /// </summary>
        public void NotifyPlayerSpoke()
        {
            if (_currentState == SocialGazeState.IdleAversion)
            {
                EnterDirectContact();
            }
            else if (_currentState == SocialGazeState.DirectContact)
            {
                // Refresh direct eye contact duration while player speaks
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// External trigger: manual focus on player.
        /// </summary>
        public void FocusOnPlayer(float duration = 4.0f)
        {
            _currentPhaseDuration = duration;
            _stateTimer = 0f;
            TransitionTo(SocialGazeState.DirectContact);
        }

        /// <summary>
        /// Calculates the final 3D gaze target taking into account social attention,
        /// aversion deflections, personal space rules, and coordinates blink on saccades.
        /// </summary>
        public Vector3 GetEffectiveGazeTarget(Vector3 defaultPlayerTarget)
        {
            Vector3 headPos = _avatarHead != null ? _avatarHead.position : transform.position;
            Vector3 toPlayer = defaultPlayerTarget - headPos;
            float distance = Mathf.Max(0.5f, toPlayer.magnitude);
            Vector3 directDir = toPlayer.normalized;

            Vector3 finalTarget = defaultPlayerTarget;

            if (_currentState == SocialGazeState.PersonalSpaceAversion)
            {
                if (_bodyLanguage != null && _bodyLanguage.IsPersonalSpaceBreached)
                {
                    finalTarget = _bodyLanguage.EffectiveLookAtTarget;
                }
                else
                {
                    // Shy aversion in avatar body frame: deflect down 20 deg and aside 25 deg
                    Quaternion shyRot = Quaternion.AngleAxis(25f, transform.up) * Quaternion.AngleAxis(20f, transform.right);
                    Vector3 avertedDir = (shyRot * directDir).normalized;
                    finalTarget = headPos + avertedDir * distance;
                }
            }
            else if (_currentState == SocialGazeState.IdleAversion)
            {
                float yaw = 0f;
                float pitch = 0f;

                switch (_currentAversion)
                {
                    case AversionDirection.AsideRight:
                        yaw = _asideYawAngle;
                        pitch = 4f; // slightly down
                        break;
                    case AversionDirection.AsideLeft:
                        yaw = -_asideYawAngle;
                        pitch = 4f;
                        break;
                    case AversionDirection.DownRight:
                        yaw = _asideYawAngle * 0.6f;
                        pitch = _downPitchAngle; // positive pitch rotates down around transform.right
                        break;
                    case AversionDirection.DownLeft:
                        yaw = -_asideYawAngle * 0.6f;
                        pitch = _downPitchAngle;
                        break;
                    case AversionDirection.RoomAmbient:
                        yaw = _roomAmbientYawAngle;
                        pitch = -6f; // slightly upward room ambient
                        break;
                }

                // In avatar body frame: yaw around up, pitch around right (+pitch tilts down towards ground)
                Quaternion deflectRot = Quaternion.AngleAxis(yaw, transform.up) * Quaternion.AngleAxis(pitch, transform.right);
                Vector3 avertedDir = (deflectRot * directDir).normalized;
                finalTarget = headPos + avertedDir * distance;
            }
            else
            {
                // Direct Contact: point directly at player target
                finalTarget = defaultPlayerTarget;
            }

            // Check for large saccadic shifts (> 20 deg) to trigger coordinated micro-blink
            Vector3 currentGazeDir = (finalTarget - headPos).normalized;
            if (_hasLastGazeDirection && _lastGazeDirection.sqrMagnitude > 0.001f)
            {
                float shiftAngle = Vector3.Angle(_lastGazeDirection, currentGazeDir);
                if (shiftAngle >= _largeSaccadeThresholdDegrees)
                {
                    if (_saccadeAndBlink != null)
                    {
                        _saccadeAndBlink.ForceBlink();
                    }
                }
            }
            _lastGazeDirection = currentGazeDir;
            _hasLastGazeDirection = true;

            return finalTarget;
        }
    }
}
