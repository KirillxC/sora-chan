using UnityEngine;
using UnityEngine.InputSystem;

namespace AiChan.Spinal
{
    /// <summary>
    /// Allows the user to grab and physically manipulate ragdoll limbs in Play Mode
    /// using mouse click and drag in Game View.
    /// Uses a physics SpringJoint attached to a kinematic cursor anchor.
    /// Migrated to Unity Input System package to prevent InvalidOperationException.
    /// Hardened against missed release events, component disable, and deadband slack.
    /// </summary>
    public class PhysicalLimbDragger : MonoBehaviour
    {
        [Header("Drag Settings")]
        public float spring = 2500f;
        public float damper = 150f;
        public float maxDistance = 0f;

        private Camera _cam;
        private SpringJoint _springJoint;
        private GameObject _anchorObject;
        private float _hitDistance;

        public bool IsDragging => _springJoint != null;

        private void Start()
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindFirstObjectByType<Camera>();
            EnsureAnchorObject();
        }

        public void EnsureAnchorObject()
        {
            if (_anchorObject == null)
            {
                _anchorObject = new GameObject("__LimbDragAnchor");
                _anchorObject.hideFlags = HideFlags.DontSave;
                var anchorRb = _anchorObject.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
            }
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) _cam = FindFirstObjectByType<Camera>();
                if (_cam == null) return;
            }

            Vector2 mousePos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                DetachSpring();
                Ray ray = _cam.ScreenPointToRay(mousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    var targetRb = hit.rigidbody;
                    if (targetRb != null && !targetRb.isKinematic && targetRb.transform.IsChildOf(transform))
                    {
                        AttachSpring(targetRb, hit.point);
                        _hitDistance = Vector3.Distance(_cam.transform.position, hit.point);
                    }
                }
            }
            else if (mouse.leftButton.isPressed && _springJoint != null)
            {
                if (_springJoint.connectedBody == null)
                {
                    DetachSpring();
                    return;
                }
                Ray ray = _cam.ScreenPointToRay(mousePos);
                Vector3 targetPos = ray.GetPoint(_hitDistance);
                MoveAnchor(targetPos);
            }
            else if (!mouse.leftButton.isPressed && _springJoint != null)
            {
                DetachSpring();
            }
        }

        public void MoveAnchor(Vector3 targetPos)
        {
            if (_anchorObject != null)
            {
                _anchorObject.transform.position = targetPos;
                var anchorRb = _anchorObject.GetComponent<Rigidbody>();
                if (anchorRb != null)
                {
                    anchorRb.position = targetPos;
                }
            }
        }

        public void AttachSpring(Rigidbody targetRb, Vector3 worldHitPoint)
        {
            DetachSpring();
            EnsureAnchorObject();

            _anchorObject.transform.position = worldHitPoint;
            var anchorRb = _anchorObject.GetComponent<Rigidbody>();
            if (anchorRb != null)
            {
                anchorRb.position = worldHitPoint;
            }

            _springJoint = _anchorObject.AddComponent<SpringJoint>();
            _springJoint.connectedBody = targetRb;
            _springJoint.autoConfigureConnectedAnchor = false;
            _springJoint.connectedAnchor = targetRb.transform.InverseTransformPoint(worldHitPoint);
            _springJoint.anchor = Vector3.zero;

            _springJoint.spring = spring;
            _springJoint.damper = damper;
            _springJoint.minDistance = 0f;
            _springJoint.maxDistance = maxDistance;
        }

        public void DetachSpring()
        {
            if (_springJoint != null)
            {
                _springJoint.connectedBody = null;
                if (Application.isPlaying)
                {
                    Destroy(_springJoint);
                }
                else
                {
                    DestroyImmediate(_springJoint);
                }
                _springJoint = null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                DetachSpring();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                DetachSpring();
            }
        }

        private void OnDisable()
        {
            DetachSpring();
        }

        private void OnDestroy()
        {
            DetachSpring();
            if (_anchorObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_anchorObject);
                }
                else
                {
                    DestroyImmediate(_anchorObject);
                }
                _anchorObject = null;
            }
        }
    }
}
