using System;
using UnityEngine;

namespace AiChan.Player
{
    /// <summary>
    /// Grounded first-person / third-person walking controller with VRM Avatar support.
    /// Strictly prevents flying; enforces ground gravity and floor collision via CharacterController.
    /// Supports slotting in any humanoid VRM model as the player avatar.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 2.2f;
        public float sprintSpeed = 4.2f;
        public float gravity = -15.0f;
        public float groundCheckDistance = 0.2f;

        [Header("Look & Camera")]
        public Transform cameraMount;
        public Camera playerCamera;
        public float mouseSensitivity = 2.0f;
        public float minPitch = -80f;
        public float maxPitch = 80f;
        [Header("Mouse & Cursor Control")]
        [Tooltip("If true, CapsLock is enabled: cursor is locked to screen center and camera moves freely without holding RMB.")]
        public bool capsLockLocked = false;

        [Header("Avatar Integration")]
        public Transform avatarModelRoot;
        public Animator avatarAnimator;
        public Transform avatarHead;
        public bool firstPersonHideHeadMesh = true;

        [Header("Perspective (First / Third Person)")]
        public bool isFirstPerson = true;
        public float thirdPersonDistance = 1.8f;
        public float thirdPersonHeightOffset = 0.2f;
        public float thirdPersonSideOffset = 0.35f;

        private CharacterController _characterController;
        private float _yaw = 0f;
        private float _pitch = 0f;
        private float _verticalVelocity = 0f;
        private bool _isGrounded = false;
        private Vector3 _moveDirection = Vector3.zero;

        // Procedural gait & head bobbing
        private float _bobTimer = 0f;
        private Vector3 _cameraDefaultLocalPos;

        // Procedural arms & posture
        private Transform _leftUpperArm;
        private Transform _rightUpperArm;
        private Quaternion _initialLeftArmRot = Quaternion.identity;
        private Quaternion _initialRightArmRot = Quaternion.identity;

        public bool IsGrounded => _isGrounded;
        public Vector3 Velocity => _characterController != null ? _characterController.velocity : Vector3.zero;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                _characterController = gameObject.AddComponent<CharacterController>();
            }

            // Configure comfortable humanoid collider bounds
            _characterController.height = 1.65f;
            _characterController.radius = 0.22f;
            _characterController.center = new Vector3(0f, 0.825f, 0f);
            _characterController.stepOffset = 0.3f;
            _characterController.slopeLimit = 45f;

            SetupCameraMount();
        }

        private void Start()
        {
            _yaw = transform.eulerAngles.y;
            if (cameraMount != null)
            {
                _pitch = cameraMount.localEulerAngles.x;
                if (_pitch > 180f) _pitch -= 360f;
                if (Mathf.Abs(_pitch) < 0.001f) _pitch = 3.0f; // Subtle natural downward gaze towards companion
                cameraMount.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
                _cameraDefaultLocalPos = cameraMount.localPosition;
            }

            // Cursor is free by default
            capsLockLocked = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            FindOrSetupAvatar();
        }

        public void SetupCameraMount()
        {
            if (cameraMount == null)
            {
                var mount = transform.Find("CameraMount");
                if (mount != null)
                {
                    cameraMount = mount;
                }
                else
                {
                    var mountGo = new GameObject("CameraMount");
                    mountGo.transform.SetParent(transform, false);
                    mountGo.transform.localPosition = new Vector3(0f, 1.45f, 0f); // Default eye level
                    cameraMount = mountGo.transform;
                }
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null && Camera.main != null)
                {
                    playerCamera = Camera.main;
                }
            }

            if (playerCamera != null)
            {
                playerCamera.transform.SetParent(cameraMount, false);
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;

                // Remove legacy fly spectator script if attached
                var flyCam = playerCamera.GetComponent<SimpleSpectatorCamera>();
                if (flyCam != null)
                {
                    Destroy(flyCam);
                }
            }
        }

        public void FindOrSetupAvatar()
        {
            if (avatarModelRoot == null)
            {
                var root = transform.Find("ModelRoot");
                if (root != null) avatarModelRoot = root;
            }

            if (avatarModelRoot != null)
            {
                avatarAnimator = avatarModelRoot.GetComponentInChildren<Animator>();
                if (avatarAnimator != null && avatarAnimator.isHuman)
                {
                    avatarHead = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
                    if (avatarHead != null && cameraMount != null)
                    {
                        // Align camera mount height precisely with avatar eye level
                        float headHeight = avatarHead.position.y - transform.position.y;
                        if (headHeight > 0.8f && headHeight < 2.2f)
                        {
                            cameraMount.localPosition = new Vector3(0f, headHeight + 0.08f, 0.04f);
                            _cameraDefaultLocalPos = cameraMount.localPosition;
                        }
                    }

                    _leftUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    _rightUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    if (_leftUpperArm != null) _initialLeftArmRot = _leftUpperArm.localRotation;
                    if (_rightUpperArm != null) _initialRightArmRot = _rightUpperArm.localRotation;
                }
            }
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleLook();
            HandleMovement();
            HandlePerspectiveToggle();
            UpdateCameraPosition();
            UpdateAvatarProceduralPose();
        }

        private void HandleCursorToggle()
        {
            // CapsLock toggles locked cursor mode
            if (Input.GetKeyDown(KeyCode.CapsLock))
            {
                capsLockLocked = !capsLockLocked;
                if (capsLockLocked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

            // Escape releases CapsLock and frees cursor
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                capsLockLocked = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleLook()
        {
            // Camera rotates ONLY when CapsLock is active OR Right Mouse Button (RMB) is held down!
            bool canLook = capsLockLocked || Input.GetMouseButton(1);
            if (!canLook) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Horizontal rotation (Yaw) rotates the whole player body
            _yaw += mouseX;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // Vertical rotation (Pitch) rotates the camera mount only
            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            if (cameraMount != null)
            {
                cameraMount.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            _isGrounded = _characterController.isGrounded;

            if (_isGrounded && _verticalVelocity < 0f)
            {
                // Small downward force to stay glued to stairs / slopes
                _verticalVelocity = -2.0f;
            }

            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Project movement strictly onto the horizontal plane (NO vertical flight!)
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 inputDirection = (forward * v + right * h).normalized;

            bool isSprinting = Input.GetKey(KeyCode.LeftShift);
            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 horizontalMove = inputDirection * currentSpeed;

            // Gravity simulation
            _verticalVelocity += gravity * Time.deltaTime;

            _moveDirection = horizontalMove;
            _moveDirection.y = _verticalVelocity;

            _characterController.Move(_moveDirection * Time.deltaTime);

            // Head bobbing when walking on foot
            float flatSpeed = new Vector2(_characterController.velocity.x, _characterController.velocity.z).magnitude;
            if (_isGrounded && flatSpeed > 0.1f)
            {
                float bobFreq = isSprinting ? 12f : 8f;
                _bobTimer += Time.deltaTime * bobFreq;
                float bobOffset = Mathf.Sin(_bobTimer) * 0.02f;
                if (cameraMount != null && isFirstPerson)
                {
                    cameraMount.localPosition = _cameraDefaultLocalPos + new Vector3(0f, bobOffset, 0f);
                }
            }
            else
            {
                _bobTimer = 0f;
                if (cameraMount != null && isFirstPerson)
                {
                    cameraMount.localPosition = Vector3.Lerp(cameraMount.localPosition, _cameraDefaultLocalPos, Time.deltaTime * 6f);
                }
            }
        }

        private void HandlePerspectiveToggle()
        {
            // V key toggles First-Person / Third-Person view
            if (Input.GetKeyDown(KeyCode.V))
            {
                SetFirstPerson(!isFirstPerson);
            }

            // Scroll wheel zooms in/out
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (scroll > 0f && !isFirstPerson)
                {
                    thirdPersonDistance = Mathf.Max(0.5f, thirdPersonDistance - 0.3f);
                    if (thirdPersonDistance <= 0.6f) SetFirstPerson(true);
                }
                else if (scroll < 0f)
                {
                    if (isFirstPerson) SetFirstPerson(false);
                    thirdPersonDistance = Mathf.Min(4.0f, thirdPersonDistance + 0.3f);
                }
            }
        }

        public void SetFirstPerson(bool fp)
        {
            isFirstPerson = fp;
            if (avatarModelRoot != null && firstPersonHideHeadMesh)
            {
                // In first person, configure head shadow rendering or layers so player can see body without camera clipping inside face
                ApplyFirstPersonMeshVisibility(fp);
            }
        }

        private void ApplyFirstPersonMeshVisibility(bool fp)
        {
            if (avatarHead == null) return;
            // When in 1st person, avatar meshes render shadows so the player casts a shadow on the floor
            var renderers = avatarModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.name.ToLower().Contains("face") || r.name.ToLower().Contains("head") || r.name.ToLower().Contains("hair"))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                else
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        private void UpdateCameraPosition()
        {
            if (playerCamera == null || cameraMount == null) return;

            if (isFirstPerson)
            {
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Third person over-the-shoulder offset
                Vector3 targetCamLocalPos = new Vector3(thirdPersonSideOffset, thirdPersonHeightOffset, -thirdPersonDistance);
                playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, targetCamLocalPos, Time.deltaTime * 12f);
                playerCamera.transform.localRotation = Quaternion.identity;
            }
        }

        private void UpdateAvatarProceduralPose()
        {
            if (avatarAnimator == null || !avatarAnimator.isHuman) return;

            // Tilt avatar spine slightly with look pitch when in 3rd person or viewed by companion
            var spine = avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null)
            {
                spine.localRotation = Quaternion.Euler(_pitch * 0.25f, 0f, 0f);
            }

            // Procedural relaxed arms + walking swing
            float flatSpeed = new Vector2(_characterController.velocity.x, _characterController.velocity.z).magnitude;
            float armSwing = Mathf.Sin(_bobTimer) * Mathf.Clamp01(flatSpeed / walkSpeed) * 25f;

            if (_leftUpperArm != null)
            {
                // Relaxed angle ~70 degrees down from T-pose + gait swing
                _leftUpperArm.localRotation = _initialLeftArmRot * Quaternion.Euler(armSwing, 0f, 68f);
            }
            if (_rightUpperArm != null)
            {
                _rightUpperArm.localRotation = _initialRightArmRot * Quaternion.Euler(-armSwing, 0f, -68f);
            }
        }

        /// <summary>
        /// Installs a new VRM or Humanoid avatar model under this player controller.
        /// </summary>
        public void AttachAvatarModel(GameObject avatarInstance)
        {
            if (avatarModelRoot == null)
            {
                var rootGo = new GameObject("ModelRoot");
                rootGo.transform.SetParent(transform, false);
                avatarModelRoot = rootGo.transform;
            }

            // Clear old children if any
            for (int i = avatarModelRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(avatarModelRoot.GetChild(i).gameObject);
            }

            avatarInstance.transform.SetParent(avatarModelRoot, false);
            avatarInstance.transform.localPosition = Vector3.zero;
            avatarInstance.transform.localRotation = Quaternion.identity;

            FindOrSetupAvatar();
            SetFirstPerson(isFirstPerson);
            Debug.Log($"[PlayerController] Successfully integrated avatar model: {avatarInstance.name}");
        }
    }
}
