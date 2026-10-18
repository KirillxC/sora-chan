#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AiChan.Player;

namespace AiChan.Editor
{
    public static class PlayerAvatarSetupHelper
    {
        [MenuItem("AI-Chan/Setup Player Walking Controller")]
        public static string SetupPlayerWalkingControllerInScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "RoomScene")
            {
                EditorSceneManager.OpenScene("Assets/Scenes/RoomScene.unity");
            }

            // 1. Locate or Create Player_Avatar root
            var playerGo = GameObject.Find("Player_Avatar");
            if (playerGo == null)
            {
                playerGo = new GameObject("Player_Avatar");
                playerGo.transform.position = new Vector3(0f, 0f, -2.2f);
                playerGo.transform.rotation = Quaternion.identity;
            }

            // 2. CharacterController
            var charCtrl = playerGo.GetComponent<CharacterController>();
            if (charCtrl == null)
            {
                charCtrl = playerGo.AddComponent<CharacterController>();
            }
            charCtrl.height = 1.65f;
            charCtrl.radius = 0.22f;
            charCtrl.center = new Vector3(0f, 0.825f, 0f);
            charCtrl.stepOffset = 0.3f;
            charCtrl.slopeLimit = 45f;

            // 3. PlayerController & InGameVrmLoaderUI
            var playerCtrl = playerGo.GetComponent<PlayerController>();
            if (playerCtrl == null)
            {
                playerCtrl = playerGo.AddComponent<PlayerController>();
            }

            if (playerGo.GetComponent<InGameVrmLoaderUI>() == null)
            {
                playerGo.AddComponent<InGameVrmLoaderUI>();
            }

            // 4. Mount Main Camera
            var cam = Camera.main;
            if (cam != null)
            {
                // Remove fly spectator camera
                var flyCam = cam.GetComponent<SimpleSpectatorCamera>();
                if (flyCam != null)
                {
                    Object.DestroyImmediate(flyCam);
                }

                playerCtrl.SetupCameraMount();
            }

            // 5. Build placeholder body if no VRM model attached yet
            var modelRoot = playerGo.transform.Find("ModelRoot");
            if (modelRoot == null)
            {
                var rootGo = new GameObject("ModelRoot");
                rootGo.transform.SetParent(playerGo.transform, false);
                modelRoot = rootGo.transform;
            }

            if (modelRoot.childCount == 0)
            {
                CreatePlaceholderBody(modelRoot.gameObject);
            }

            playerCtrl.FindOrSetupAvatar();

            // 6. Mark scene dirty & save
            EditorUtility.SetDirty(playerGo);
            if (cam != null) EditorUtility.SetDirty(cam.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[PlayerAvatarSetupHelper] Grounded PlayerController with avatar support successfully configured in RoomScene!");
            return "SUCCESS: PlayerController configured";
        }

        public static void CreatePlaceholderBody(GameObject parent)
        {
            // Simple stylized humanoid placeholder (Torso, Head, Arms) casting shadows
            var body = new GameObject("Placeholder_Body");
            body.transform.SetParent(parent.transform, false);

            // Torso
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Torso";
            torso.transform.SetParent(body.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            torso.transform.localScale = new Vector3(0.35f, 0.45f, 0.25f);
            var col = torso.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(body.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.48f, 0f);
            head.transform.localScale = new Vector3(0.24f, 0.26f, 0.24f);
            col = head.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            // Material with soft tint
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.25f, 0.45f, 0.75f);
            torso.GetComponent<Renderer>().material = mat;
            head.GetComponent<Renderer>().material = mat;
        }

        /// <summary>
        /// Loads and attaches a VRM model file to the Player_Avatar in the active scene.
        /// </summary>
        public static string InstallPlayerVrm(string vrmAssetPath)
        {
            if (!File.Exists(vrmAssetPath))
            {
                return $"ERROR: File not found at {vrmAssetPath}";
            }

            SetupPlayerWalkingControllerInScene();

            var playerGo = GameObject.Find("Player_Avatar");
            if (playerGo == null) return "ERROR: Player_Avatar not found";

            var playerCtrl = playerGo.GetComponent<PlayerController>();

            // Ensure asset is imported by Unity
            AssetDatabase.ImportAsset(vrmAssetPath, ImportAssetOptions.ForceUpdate);
            var vrmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vrmAssetPath);

            if (vrmPrefab == null)
            {
                return $"ERROR: Could not load VRM GameObject from {vrmAssetPath}";
            }

            var vrmInstance = (GameObject)PrefabUtility.InstantiatePrefab(vrmPrefab);
            vrmInstance.name = Path.GetFileNameWithoutExtension(vrmAssetPath);

            playerCtrl.AttachAvatarModel(vrmInstance);

            EditorUtility.SetDirty(playerGo);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"[PlayerAvatarSetupHelper] Successfully installed player avatar from {vrmAssetPath}!");
            return $"SUCCESS: Installed {vrmInstance.name}";
        }

        /// <summary>
        /// Imports an external VRM file from any disk path into Assets/Models and assigns it to the Player.
        /// </summary>
        public static string InstallPlayerVrmFromDisk(string externalVrmPath)
        {
            if (!File.Exists(externalVrmPath))
            {
                return $"ERROR: External VRM file not found at: {externalVrmPath}";
            }

            string modelsDir = Path.Combine(Application.dataPath, "Models");
            if (!Directory.Exists(modelsDir)) Directory.CreateDirectory(modelsDir);

            string filename = Path.GetFileName(externalVrmPath);
            string targetPath = Path.Combine(modelsDir, filename);

            File.Copy(externalVrmPath, targetPath, true);
            AssetDatabase.Refresh();

            string assetRelativePath = "Assets/Models/" + filename;
            return InstallPlayerVrm(assetRelativePath);
        }

        /// <summary>
        /// Diagnostic verification that AI-Chan tracks Player avatar head and NOT the 3rd person camera.
        /// </summary>
        public static string VerifyThirdPersonLookAt()
        {
            var player = PlayerController.Instance != null ? PlayerController.Instance : Object.FindFirstObjectByType<PlayerController>();
            if (player == null) return "ERROR: PlayerController not found in scene";

            var lookAt = Object.FindFirstObjectByType<AiChan.Avatar.Gaze.AvatarLookAtController>();
            if (lookAt == null) return "ERROR: AvatarLookAtController not found in scene";

            // Switch player to 3rd person
            player.SetFirstPerson(false);

            Transform gazeTarget = player.GazeTarget;
            Transform currentTarget = lookAt.Target;
            Camera mainCam = Camera.main;

            bool isLookingAtAvatar = (currentTarget == gazeTarget && currentTarget != null);
            bool isLookingAtCamera = (mainCam != null && currentTarget == mainCam.transform);

            Vector3 avatarHeadPos = gazeTarget != null ? gazeTarget.position : Vector3.zero;
            Vector3 camPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
            float distBetweenAvatarAndCamera = Vector3.Distance(avatarHeadPos, camPos);

            return $"{{\"isThirdPerson\": {!player.isFirstPerson}, \"targetName\": \"{(currentTarget != null ? currentTarget.name : "null")}\", \"gazeTargetName\": \"{(gazeTarget != null ? gazeTarget.name : "null")}\", \"isLookingAtAvatar\": {isLookingAtAvatar.ToString().ToLower()}, \"isLookingAtCamera\": {isLookingAtCamera.ToString().ToLower()}, \"avatarToCamDist\": {distBetweenAvatarAndCamera:F2}}}";
        }
    }
}
#endif
