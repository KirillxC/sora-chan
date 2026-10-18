using System;
using System.Collections.Generic;
using UnityEngine;
using AiChan.Core;

namespace AiChan.Thalamus
{
    /// <summary>
    /// Level 2: Thalamus & Somatosensory Layer.
    /// Manages touch sensor zones on the body, classifies contacts,
    /// and tracks user gaze / distance telemetry.
    /// Runs at 50-90 Hz without blocking physics.
    /// </summary>
    public class ThalamusSensor : MonoBehaviour
    {
        [Header("Telemetry Settings")]
        public Transform headBone;
        public Camera playerCamera;

        [Header("State")]
        [SerializeField] private float _playerDistance = 2.0f;
        [SerializeField] private string _gazeStatus = "looking_away";

        private readonly Queue<SomatosensoryEvent> _touchEventQueue = new Queue<SomatosensoryEvent>();
        private readonly object _lock = new object();

        public float PlayerDistance => _playerDistance;
        public string GazeStatus => _gazeStatus;
        public bool HasPendingTouch
        {
            get
            {
                lock (_lock)
                {
                    return _touchEventQueue.Count > 0;
                }
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) playerCamera = FindFirstObjectByType<Camera>();
            }

            if (headBone == null)
            {
                var anim = GetComponent<Animator>();
                if (anim != null) headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        public void Step()
        {
            EnsureInitialized();
            UpdateUserTelemetry();
        }

        private void Update()
        {
            Step();
        }

        private void UpdateUserTelemetry()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            Vector3 targetPos = headBone != null ? headBone.position : transform.position + Vector3.up * 1.4f;

            // Measure physical distance to player avatar (not disconnected 3rd person camera)
            var player = AiChan.Player.PlayerController.Instance != null ? AiChan.Player.PlayerController.Instance : FindFirstObjectByType<AiChan.Player.PlayerController>();
            Vector3 userAvatarPos = (player != null && player.GazeTarget != null) ? player.GazeTarget.position : playerCamera.transform.position;
            _playerDistance = Vector3.Distance(targetPos, userAvatarPos);

            Vector3 toTarget = targetPos - playerCamera.transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(playerCamera.transform.forward, toTarget);
                if (angle < 15f)
                {
                    _gazeStatus = "direct_eye_contact";
                }
                else if (angle < 35f)
                {
                    _gazeStatus = "looking_at_body";
                }
                else
                {
                    _gazeStatus = "looking_away";
                }
            }
        }

        public void TriggerTouch(BodyZone zone, TouchGesture gesture, float intensity = 1.0f)
        {
            lock (_lock)
            {
                while (_touchEventQueue.Count >= 50)
                {
                    _touchEventQueue.Dequeue();
                }
                _touchEventQueue.Enqueue(new SomatosensoryEvent
                {
                    zone = zone,
                    gesture = gesture,
                    intensity = intensity,
                    durationSec = 0.2f,
                    contactSide = "center",
                    timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
        }

        public bool TryPopRecentTouch(out string zoneStr, out string gestureStr, out float intensity)
        {
            lock (_lock)
            {
                if (_touchEventQueue.Count > 0)
                {
                    var ev = _touchEventQueue.Dequeue();
                    zoneStr = ev.zone.ToString();
                    gestureStr = ev.gesture.ToString();
                    intensity = ev.intensity;
                    return true;
                }
            }

            zoneStr = null;
            gestureStr = null;
            intensity = 0f;
            return false;
        }
    }
}
