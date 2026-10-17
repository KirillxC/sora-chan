using UnityEngine;

namespace AiChan.Player
{
    /// <summary>
    /// Simple First-Person WASD + Mouse Look spectator controller
    /// Allows the user to inspect AI-Chan, walk up close (<60cm) to trigger proximity blush,
    /// or walk around to test LookAt tracking.
    /// </summary>
    public class SimpleSpectatorCamera : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 2.0f;
        public float sprintMultiplier = 2.0f;
        public float verticalSpeed = 1.5f;

        [Header("Look")]
        public float lookSensitivity = 2.5f;
        public bool requireRightClickToLook = true;

        private float _yaw = 0f;
        private float _pitch = 0f;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _pitch = angles.x;
            _yaw = angles.y;
        }

        private void Update()
        {
            // Mouse Look
            bool canLook = !requireRightClickToLook || Input.GetMouseButton(1);
            if (canLook)
            {
                float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

                _yaw += mouseX;
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);

                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            // Keyboard Movement (WASD + QE)
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1.0f);
            float h = Input.GetAxis("Horizontal"); // A/D
            float v = Input.GetAxis("Vertical");   // W/S

            Vector3 moveDir = transform.forward * v + transform.right * h;

            // Vertical movement: E (Up), Q (Down), Space (Up), LeftControl (Down)
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))
            {
                moveDir += Vector3.up * (verticalSpeed / moveSpeed);
            }
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl))
            {
                moveDir -= Vector3.up * (verticalSpeed / moveSpeed);
            }

            transform.position += moveDir * (speed * Time.deltaTime);
        }
    }
}
