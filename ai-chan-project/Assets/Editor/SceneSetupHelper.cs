#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AiChan.Editor
{
    public static class SceneSetupHelper
    {
        [MenuItem("AI-Chan/Build Room Scene")]
        public static string BuildRoomScene()
        {
            // 1. Create a clean new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Main Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            camGo.transform.position = new Vector3(0f, 1.45f, -2.2f);
            camGo.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            camGo.AddComponent<AudioListener>();

            // 3. Directional Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 4. Room Floor (Wooden Plane)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "RoomFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            // 5. Look for imported VRM prefab or model asset
            GameObject avatarInstance = null;
            AssetDatabase.ImportAsset("Assets/Models/AvatarSample_A.vrm", ImportAssetOptions.ForceUpdate);
            var vrmAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/AvatarSample_A.vrm");
            if (vrmAsset != null)
            {
                avatarInstance = (GameObject)PrefabUtility.InstantiatePrefab(vrmAsset);
            }

            if (avatarInstance == null)
            {
                string[] guids = AssetDatabase.FindAssets("AvatarSample_A");
                foreach (var g in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go != null)
                    {
                        avatarInstance = (GameObject)PrefabUtility.InstantiatePrefab(go);
                        break;
                    }
                }
            }

            if (avatarInstance != null)
            {
                avatarInstance.name = "AiChan_Avatar";
                avatarInstance.transform.position = Vector3.zero;
                avatarInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            if (avatarInstance == null)
            {
                // Create a humanoid reference dummy until VRM finishes importing
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = "AiChan_Avatar_Reference";
                dummy.transform.position = new Vector3(0f, 0.9f, 0f);
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.position = new Vector3(0f, 1.55f, 0f);
                head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                head.transform.parent = dummy.transform;
            }

            // 6. Save Scene
            Directory.CreateDirectory("Assets/Scenes");
            string scenePath = "Assets/Scenes/RoomScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();

            // Render camera view to file
            try
            {
                int width = 1280;
                int height = 720;
                var rt = new RenderTexture(width, height, 24);
                var prevRt = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                cam.targetTexture = prevRt;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);

                byte[] bytes = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);

                string outPath = Path.Combine(Application.dataPath, "..", "scene_preview.png");
                File.WriteAllBytes(outPath, bytes);
                Debug.Log($"[SceneSetupHelper] Screenshot saved to: {outPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SceneSetupHelper] Screenshot error: {ex.Message}");
            }

            Debug.Log($"[SceneSetupHelper] RoomScene created and saved to {scenePath}");
            return scenePath;
        }

        [MenuItem("AI-Chan/Render Screenshot")]
        public static string RenderScreenshot()
        {
            var cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return "no_camera";

            int width = 1280;
            int height = 720;
            var rt = new RenderTexture(width, height, 24);
            var prevRt = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevRt;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string outPath = Path.Combine(Application.dataPath, "..", "scene_preview.png");
            File.WriteAllBytes(outPath, bytes);
            Debug.Log($"[SceneSetupHelper] Screenshot saved to: {outPath}");
            return outPath;
        }
    }
}
#endif
