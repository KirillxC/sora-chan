#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AiChan.Spinal;
using AiChan.Interaction;

namespace AiChan.Editor
{
    public static class RoomInteractiveSetup
    {
        [MenuItem("AI-Chan/Setup Interactive Ball in Scene")]
        public static string SetupInteractiveBallScene()
        {
            string scenePath = "Assets/Scenes/RoomScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Locate or create Avatar
            var avatar = GameObject.Find("AiChan_Avatar");
            if (avatar == null)
            {
                avatar = GameObject.Find("AiChan_Avatar_Reference");
            }

            if (avatar != null)
            {
                // Ensure BallisticPredictor and BallCatcherAndThrower are on avatar
                var predictor = avatar.GetComponent<BallisticPredictor>();
                if (predictor == null) predictor = avatar.AddComponent<BallisticPredictor>();

                var catcher = avatar.GetComponent<BallCatcherAndThrower>();
                if (catcher == null) catcher = avatar.AddComponent<BallCatcherAndThrower>();
                catcher.predictor = predictor;

                var anim = avatar.GetComponent<Animator>();
                if (anim != null && anim.isHuman)
                {
                    var chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                    predictor.chestTransform = chest;
                    catcher.chestTransform = chest;
                    catcher.headTransform = anim.GetBoneTransform(HumanBodyBones.Head);
                }
            }

            // 2. Locate or create Interactive Ball
            var ball = GameObject.Find("InteractiveBall");
            if (ball == null)
            {
                ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "InteractiveBall";
                ball.tag = "InteractiveBall";
            }
            ball.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f); // Basketball scale (~24cm)
            ball.transform.position = new Vector3(0f, 1.25f, -1.5f);

            var ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb == null) ballRb = ball.AddComponent<Rigidbody>();
            ballRb.mass = 0.62f; // Standard basketball mass ~620g
            ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Make ball colorful (orange/red)
            var ballRenderer = ball.GetComponent<Renderer>();
            if (ballRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.95f, 0.45f, 0.1f); // Vibrant Basketball Orange
                ballRenderer.material = mat;
            }

            // 3. Attach PlayerBallInteraction to Main Camera
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null && Camera.main != null) camGo = Camera.main.gameObject;
            if (camGo != null)
            {
                var playerInteraction = camGo.GetComponent<PlayerBallInteraction>();
                if (playerInteraction == null) playerInteraction = camGo.AddComponent<PlayerBallInteraction>();
                playerInteraction.ballRb = ballRb;
                if (avatar != null)
                {
                    playerInteraction.catcher = avatar.GetComponent<BallCatcherAndThrower>();
                    if (playerInteraction.catcher != null)
                        playerInteraction.avatarChest = playerInteraction.catcher.chestTransform;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[RoomInteractiveSetup] Interactive ball and player controls successfully configured in RoomScene!");
            return "ok";
        }
    }
}
#endif
