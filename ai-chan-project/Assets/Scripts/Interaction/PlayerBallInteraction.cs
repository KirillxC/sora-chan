using System;
using UnityEngine;

namespace AiChan.Interaction
{
    /// <summary>
    /// Interactive controller for throwing and resetting the test ball in Game View / VR.
    /// Controls:
    /// - Space / Left Click: Throw ball towards the avatar
    /// - R: Reset ball in front of player
    /// - T: Trigger avatar throw back to player
    /// </summary>
    public class PlayerBallInteraction : MonoBehaviour
    {
        public Rigidbody ballRb;
        public Transform avatarChest;
        public AiChan.Spinal.BallCatcherAndThrower catcher;

        public float throwSpeed = 6.5f;
        public Vector3 initialBallSpawnOffset = new Vector3(0f, 1.3f, -1.8f);

        private void Start()
        {
            if (ballRb == null)
            {
                var ballGo = GameObject.Find("InteractiveBall");
                if (ballGo != null) ballRb = ballGo.GetComponent<Rigidbody>();
            }

            if (catcher == null)
            {
                catcher = FindFirstObjectByType<AiChan.Spinal.BallCatcherAndThrower>();
            }

            if (avatarChest == null && catcher != null)
            {
                avatarChest = catcher.chestTransform;
            }
        }

        private void Update()
        {
            if (ballRb == null) return;

            bool throwPressed = false;
            bool resetPressed = false;
            bool returnPressed = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) throwPressed = true;
                if (kb.rKey.wasPressedThisFrame) resetPressed = true;
                if (kb.tKey.wasPressedThisFrame) returnPressed = true;
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                throwPressed = true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!throwPressed && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))) throwPressed = true;
            if (!resetPressed && Input.GetKeyDown(KeyCode.R)) resetPressed = true;
            if (!returnPressed && Input.GetKeyDown(KeyCode.T)) returnPressed = true;
#endif

            // Space or Left Click: Throw ball toward avatar chest
            if (throwPressed)
            {
                ThrowBallAtAvatar();
            }

            // R: Reset ball
            if (resetPressed)
            {
                ResetBall();
            }

            // T: Ask avatar to throw it back
            if (returnPressed)
            {
                if (catcher != null && catcher.isHoldingBall)
                {
                    Vector3 returnPos = transform.position + Vector3.up * 1.2f;
                    catcher.ThrowBallBack(returnPos, 0.7f);
                }
            }
        }

        public void ResetBall()
        {
            if (catcher != null) catcher.isHoldingBall = false;

            ballRb.isKinematic = false;
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
            ballRb.transform.position = transform.position + transform.forward * 0.5f + Vector3.down * 0.1f;
            Debug.Log("[PlayerBallInteraction] Ball reset in front of player.");
        }

        public void ThrowBallAtAvatar()
        {
            if (catcher != null && catcher.isHoldingBall)
            {
                Debug.Log("[PlayerBallInteraction] Avatar is already holding the ball. Press T to throw it back!");
                return;
            }

            ballRb.isKinematic = false;
            ballRb.transform.position = transform.position + transform.forward * 0.4f;
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;

            Vector3 target = avatarChest != null ? avatarChest.position : new Vector3(0f, 1.2f, 0f);
            float flightTime = 0.5f;

            Vector3 throwVelocity = AiChan.Spinal.BallisticPredictor.CalculateThrowVelocity(
                ballRb.position, target, flightTime, Physics.gravity
            );

            ballRb.linearVelocity = throwVelocity;
            Debug.Log($"[PlayerBallInteraction] Threw ball at avatar with velocity: {throwVelocity}");
        }
    }
}
