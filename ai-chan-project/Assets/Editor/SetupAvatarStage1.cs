#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniVRM10;
using VRM;
using AiChan.Avatar.Gaze;
using AiChan.Avatar.Expressiveness;
using AiChan.Limbic;

namespace AiChan.Editor
{
    public static class SetupAvatarStage1
    {
        public static string Setup()
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.name != "RoomScene")
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/RoomScene.unity");
                }

                // Find AvatarSample_A in root or children
                GameObject avatarGo = null;
                var rootObjs = EditorSceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var go in rootObjs)
                {
                    if (go.name.Contains("AvatarSample_A"))
                    {
                        avatarGo = go;
                        break;
                    }
                    var child = go.transform.Find("AvatarSample_A");
                    if (child != null)
                    {
                        avatarGo = child.gameObject;
                        break;
                    }
                }

                if (avatarGo == null)
                {
                    // Search by Vrm10Instance or VRMMeta or Animator
                    var vrm10 = UnityEngine.Object.FindFirstObjectByType<Vrm10Instance>();
                    if (vrm10 != null) avatarGo = vrm10.gameObject;
                }

                if (avatarGo == null)
                {
                    var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
                    foreach (var anim in animators)
                    {
                        if (anim.isHuman)
                        {
                            avatarGo = anim.gameObject;
                            break;
                        }
                    }
                }

                if (avatarGo == null)
                {
                    return "ERROR: Avatar GameObject not found in RoomScene!";
                }

                string report = $"Found Avatar: {avatarGo.name}. Adding/Verifying components...\n";

                // 1. AvatarLookAtController
                var lookAt = avatarGo.GetComponent<AvatarLookAtController>();
                if (lookAt == null)
                {
                    lookAt = avatarGo.AddComponent<AvatarLookAtController>();
                    report += "- Added AvatarLookAtController\n";
                }
                else
                {
                    report += "- AvatarLookAtController already present\n";
                }

                // 2. SaccadeAndBlinkController
                var saccadeBlink = avatarGo.GetComponent<SaccadeAndBlinkController>();
                if (saccadeBlink == null)
                {
                    saccadeBlink = avatarGo.AddComponent<SaccadeAndBlinkController>();
                    report += "- Added SaccadeAndBlinkController\n";
                }
                else
                {
                    report += "- SaccadeAndBlinkController already present\n";
                }

                // 3. AnimeExpressivenessEngine
                var expressiveness = avatarGo.GetComponent<AnimeExpressivenessEngine>();
                if (expressiveness == null)
                {
                    expressiveness = avatarGo.AddComponent<AnimeExpressivenessEngine>();
                    report += "- Added AnimeExpressivenessEngine\n";
                }
                else
                {
                    report += "- AnimeExpressivenessEngine already present\n";
                }

                // Auto-hook face renderer if possible
                var skinnedRenderers = avatarGo.GetComponentsInChildren<SkinnedMeshRenderer>();
                var faceRenderer = skinnedRenderers.FirstOrDefault(r => r.name.ToLower().Contains("face"));
                if (faceRenderer != null)
                {
                    var soExpr = new SerializedObject(expressiveness);
                    var faceProp = soExpr.FindProperty("faceRenderer");
                    if (faceProp != null && faceProp.objectReferenceValue == null)
                    {
                        faceProp.objectReferenceValue = faceRenderer;
                        soExpr.ApplyModifiedProperties();
                        report += $"  - Hooked faceRenderer: {faceRenderer.name}\n";
                    }
                }

                // 4. UserBodyLanguageAnalyzer
                var bodyLang = avatarGo.GetComponent<UserBodyLanguageAnalyzer>();
                if (bodyLang == null)
                {
                    bodyLang = avatarGo.AddComponent<UserBodyLanguageAnalyzer>();
                    report += "- Added UserBodyLanguageAnalyzer\n";
                }
                else
                {
                    report += "- UserBodyLanguageAnalyzer already present\n";
                }

                // Auto-wire UserBodyLanguageAnalyzer references
                var soBody = new SerializedObject(bodyLang);
                var exprProp = soBody.FindProperty("expressivenessEngine");
                if (exprProp != null && exprProp.objectReferenceValue == null)
                {
                    exprProp.objectReferenceValue = expressiveness;
                    report += "  - Hooked expressivenessEngine to UserBodyLanguageAnalyzer\n";
                }

                var limbic = avatarGo.GetComponent<LimbicController>() ?? UnityEngine.Object.FindFirstObjectByType<LimbicController>();
                var limbicProp = soBody.FindProperty("limbicController");
                if (limbicProp != null && limbic != null && limbicProp.objectReferenceValue == null)
                {
                    limbicProp.objectReferenceValue = limbic;
                    report += "  - Hooked limbicController to UserBodyLanguageAnalyzer\n";
                }

                var cam = Camera.main;
                var camProp = soBody.FindProperty("playerCamera");
                if (camProp != null && cam != null && camProp.objectReferenceValue == null)
                {
                    camProp.objectReferenceValue = cam;
                    report += "  - Hooked playerCamera to UserBodyLanguageAnalyzer\n";
                }

                var animComponent = avatarGo.GetComponent<Animator>();
                if (animComponent != null && animComponent.isHuman)
                {
                    var headTrans = animComponent.GetBoneTransform(HumanBodyBones.Head);
                    var headProp = soBody.FindProperty("avatarHead");
                    if (headProp != null && headTrans != null && headProp.objectReferenceValue == null)
                    {
                        headProp.objectReferenceValue = headTrans;
                        report += "  - Hooked avatarHead to UserBodyLanguageAnalyzer\n";
                    }
                }
                soBody.ApplyModifiedProperties();

                if (cam != null && cam.GetComponent<AiChan.Player.SimpleSpectatorCamera>() == null)
                {
                    cam.gameObject.AddComponent<AiChan.Player.SimpleSpectatorCamera>();
                    EditorUtility.SetDirty(cam.gameObject);
                    report += "  - Added SimpleSpectatorCamera to Main Camera\n";
                }

                // Mark scene dirty and save
                EditorUtility.SetDirty(avatarGo);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                report += "SUCCESS: Scene saved with configured components!";
                Debug.Log($"[SetupAvatarStage1] {report}");
                return report;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetupAvatarStage1] Exception: {ex}");
                return $"ERROR: {ex.Message}";
            }
        }
    }
}
#endif
