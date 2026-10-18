using System;
using System.Collections.Generic;
using UnityEngine;
using UniVRM10;
using VRM;

namespace AiChan.Avatar.Expressiveness
{
    public enum EmotionType
    {
        Neutral,
        Joy,
        Angry,
        Sorrow,
        Surprised,
        Fun,
        Blink,
        Blush
    }

    /// <summary>
    /// Wrapper over VRM BlendShape / Expression API (supporting VRM 1.0, VRM 0.x, and SkinnedMesh fallback).
    /// Provides smooth transitions (Lerp / SmoothDamp) and priority handling between emotions.
    /// </summary>
    public class AnimeExpressivenessEngine : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Vrm10Instance vrm10Instance;
        [SerializeField] private VRMBlendShapeProxy vrm0xProxy;
        [SerializeField] private SkinnedMeshRenderer faceRenderer;

        [Header("Expression Blend Settings")]
        [Tooltip("Transition speed for emotion changes (SmoothDamp or Lerp rate)")]
        [SerializeField] private float transitionSpeed = 8.0f;

        [Range(0f, 1f)]
        [SerializeField] private float globalBlush = 0.0f;

        private static readonly EmotionType[] AllEmotionValues = (EmotionType[])Enum.GetValues(typeof(EmotionType));

        // Current active state
        private EmotionType _currentPrimaryEmotion = EmotionType.Neutral;
        private int _currentPriority = 0;
        private float _primaryTargetWeight = 1.0f;

        // Weights tracking
        private readonly Dictionary<EmotionType, float> _currentWeights = new Dictionary<EmotionType, float>();
        private readonly Dictionary<EmotionType, float> _targetWeights = new Dictionary<EmotionType, float>();
        private readonly Dictionary<EmotionType, float> _weightVelocities = new Dictionary<EmotionType, float>();

        // Custom morph names / fallback blendshapes
        private readonly Dictionary<string, float> _customWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _targetCustomWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _customKeysBuffer = new List<string>();

        // Cached SkinnedMesh indices for fallback
        private readonly Dictionary<EmotionType, int> _meshBlendShapeIndices = new Dictionary<EmotionType, int>();
        private int _meshBlushIndex = -1;

        public float GlobalBlush
        {
            get => globalBlush;
            set => SetBlush(value, 0);
        }

        public EmotionType CurrentEmotion => _currentPrimaryEmotion;
        public int CurrentPriority => _currentPriority;

        private void Awake()
        {
            InitializeComponents();
            InitEmotionWeights();
            CacheMeshBlendShapes();
        }

        private void Reset()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            if (vrm10Instance == null)
                vrm10Instance = GetComponent<Vrm10Instance>() ?? GetComponentInParent<Vrm10Instance>() ?? GetComponentInChildren<Vrm10Instance>();

            if (vrm0xProxy == null)
                vrm0xProxy = GetComponent<VRMBlendShapeProxy>() ?? GetComponentInParent<VRMBlendShapeProxy>() ?? GetComponentInChildren<VRMBlendShapeProxy>();

            if (faceRenderer == null)
            {
                var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var r in renderers)
                {
                    if (r.name.ToLowerInvariant().Contains("face") || (r.sharedMesh != null && r.sharedMesh.blendShapeCount > 5))
                    {
                        faceRenderer = r;
                        break;
                    }
                }
            }
        }

        private void InitEmotionWeights()
        {
            foreach (EmotionType emo in Enum.GetValues(typeof(EmotionType)))
            {
                _currentWeights[emo] = 0f;
                _targetWeights[emo] = 0f;
                _weightVelocities[emo] = 0f;
            }
            _targetWeights[EmotionType.Neutral] = 1f;
        }

        private void CacheMeshBlendShapes()
        {
            if (faceRenderer == null || faceRenderer.sharedMesh == null) return;

            Mesh mesh = faceRenderer.sharedMesh;
            int count = mesh.blendShapeCount;

            for (int i = 0; i < count; i++)
            {
                string shapeName = mesh.GetBlendShapeName(i).ToLowerInvariant();

                if (_meshBlushIndex < 0 && (shapeName.Contains("blush") || shapeName.Contains("skinfung") || shapeName.Contains("fung1") || shapeName.Contains("cheek")))
                {
                    _meshBlushIndex = i;
                }

                CheckMeshEmotion(shapeName, i, "joy", EmotionType.Joy);
                CheckMeshEmotion(shapeName, i, "angry", EmotionType.Angry);
                CheckMeshEmotion(shapeName, i, "sorrow", EmotionType.Sorrow);
                CheckMeshEmotion(shapeName, i, "sad", EmotionType.Sorrow);
                CheckMeshEmotion(shapeName, i, "surprised", EmotionType.Surprised);
                CheckMeshEmotion(shapeName, i, "fun", EmotionType.Fun);
                CheckMeshEmotion(shapeName, i, "relaxed", EmotionType.Fun);
                CheckMeshEmotion(shapeName, i, "blink", EmotionType.Blink);
                CheckMeshEmotion(shapeName, i, "neutral", EmotionType.Neutral);
            }
        }

        private void CheckMeshEmotion(string shapeName, int index, string keyword, EmotionType emotion)
        {
            if (!_meshBlendShapeIndices.ContainsKey(emotion) && shapeName.Contains(keyword))
            {
                _meshBlendShapeIndices[emotion] = index;
            }
        }

        /// <summary>
        /// Set emotion with priority. If new priority >= current priority, it takes over.
        /// Priority 0 = default/idle, 10 = dialogue/social, 20 = reflex/proximity, 50 = system/override.
        /// </summary>
        public void SetEmotion(EmotionType emotion, float targetWeight = 1.0f, int priority = 0)
        {
            if (priority < _currentPriority && _currentPrimaryEmotion != emotion)
            {
                return; // Suppressed by higher priority emotion
            }

            _currentPrimaryEmotion = emotion;
            _currentPriority = priority;
            _primaryTargetWeight = Mathf.Clamp01(targetWeight);

            for (int i = 0; i < AllEmotionValues.Length; i++)
            {
                var emo = AllEmotionValues[i];
                if (emo == EmotionType.Blush || emo == EmotionType.Blink) continue; // Kept layered
                _targetWeights[emo] = (emo == emotion) ? _primaryTargetWeight : 0f;
            }

            if (emotion != EmotionType.Neutral && _primaryTargetWeight < 1.0f)
            {
                _targetWeights[EmotionType.Neutral] = 1.0f - _primaryTargetWeight;
            }
        }

        /// <summary>
        /// Resets emotion if called from same or higher priority.
        /// </summary>
        public void ClearEmotion(int priority = 0)
        {
            if (priority >= _currentPriority)
            {
                _currentPriority = 0;
                SetEmotion(EmotionType.Neutral, 1.0f, 0);
            }
        }

        /// <summary>
        /// Controls blush independently as an overlay expression.
        /// </summary>
        public void SetBlush(float amount, int priority = 0)
        {
            globalBlush = Mathf.Clamp01(amount);
            _targetWeights[EmotionType.Blush] = globalBlush;
        }

        /// <summary>
        /// Trigger eyelid blink overlay.
        /// </summary>
        public void SetBlink(float amount)
        {
            _targetWeights[EmotionType.Blink] = Mathf.Clamp01(amount);
        }

        /// <summary>
        /// Set custom blend shape weight (by name).
        /// </summary>
        public void SetCustomMorph(string morphName, float targetWeight)
        {
            if (string.IsNullOrEmpty(morphName)) return;
            _targetCustomWeights[morphName] = Mathf.Clamp01(targetWeight);
            if (!_customWeights.ContainsKey(morphName))
            {
                _customWeights[morphName] = 0f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Interpolate emotion weights
            for (int i = 0; i < AllEmotionValues.Length; i++)
            {
                var emo = AllEmotionValues[i];
                if (!_targetWeights.TryGetValue(emo, out float target)) continue;
                _currentWeights.TryGetValue(emo, out float current);
                _weightVelocities.TryGetValue(emo, out float vel);

                // Smooth damp for natural facial muscle motion
                float next = Mathf.SmoothDamp(current, target, ref vel, 1f / Mathf.Max(transitionSpeed, 0.01f), Mathf.Infinity, dt);
                _currentWeights[emo] = next;
                _weightVelocities[emo] = vel;
            }

            // Interpolate custom weights
            _customKeysBuffer.Clear();
            _customKeysBuffer.AddRange(_targetCustomWeights.Keys);
            for (int i = 0; i < _customKeysBuffer.Count; i++)
            {
                string key = _customKeysBuffer[i];
                float current = _customWeights[key];
                float target = _targetCustomWeights[key];
                float next = Mathf.MoveTowards(current, target, transitionSpeed * dt);
                _customWeights[key] = next;
            }

            ApplyWeightsToModel();
        }

        private void ApplyWeightsToModel()
        {
            bool applied = false;

            // Try VRM 1.0 first
            if (vrm10Instance != null && vrm10Instance.Runtime != null && vrm10Instance.Runtime.Expression != null)
            {
                ApplyVRM10(vrm10Instance.Runtime.Expression);
                applied = true;
            }
            // Fallback to VRM 0.x
            else if (vrm0xProxy != null)
            {
                ApplyVRM0x(vrm0xProxy);
                applied = true;
            }

            // Apply Blush & fallback blendshapes to SkinnedMeshRenderer
            ApplyToMeshRenderer(!applied);
        }

        private void ApplyVRM10(Vrm10RuntimeExpression expression)
        {
            SafeSetVrm10Weight(expression, ExpressionKey.Happy, _currentWeights[EmotionType.Joy]);
            SafeSetVrm10Weight(expression, ExpressionKey.Angry, _currentWeights[EmotionType.Angry]);
            SafeSetVrm10Weight(expression, ExpressionKey.Sad, _currentWeights[EmotionType.Sorrow]);
            SafeSetVrm10Weight(expression, ExpressionKey.Surprised, _currentWeights[EmotionType.Surprised]);
            SafeSetVrm10Weight(expression, ExpressionKey.Relaxed, _currentWeights[EmotionType.Fun]);
            SafeSetVrm10Weight(expression, ExpressionKey.Neutral, _currentWeights[EmotionType.Neutral]);
            SafeSetVrm10Weight(expression, ExpressionKey.Blink, _currentWeights[EmotionType.Blink]);

            // Custom blush key or morphs
            if (_currentWeights[EmotionType.Blush] > 0.001f)
            {
                var blushKey = ExpressionKey.CreateCustom("Blush");
                SafeSetVrm10Weight(expression, blushKey, _currentWeights[EmotionType.Blush]);
            }

            foreach (var kvp in _customWeights)
            {
                var customKey = ExpressionKey.CreateCustom(kvp.Key);
                SafeSetVrm10Weight(expression, customKey, kvp.Value);
            }
        }

        private static void SafeSetVrm10Weight(Vrm10RuntimeExpression expression, ExpressionKey key, float weight)
        {
            try
            {
                expression.SetWeight(key, weight);
            }
            catch (KeyNotFoundException) { }
            catch (ArgumentException) { }
        }

        private void ApplyVRM0x(VRMBlendShapeProxy proxy)
        {
            proxy.ImmediatelySetValue(BlendShapePreset.Joy, _currentWeights[EmotionType.Joy]);
            proxy.ImmediatelySetValue(BlendShapePreset.Angry, _currentWeights[EmotionType.Angry]);
            proxy.ImmediatelySetValue(BlendShapePreset.Sorrow, _currentWeights[EmotionType.Sorrow]);
            proxy.ImmediatelySetValue(BlendShapePreset.Fun, _currentWeights[EmotionType.Fun]);
            proxy.ImmediatelySetValue(BlendShapePreset.Neutral, _currentWeights[EmotionType.Neutral]);
            proxy.ImmediatelySetValue(BlendShapePreset.Blink, _currentWeights[EmotionType.Blink]);

            if (_currentWeights[EmotionType.Blush] > 0.001f)
            {
                proxy.ImmediatelySetValue("Blush", _currentWeights[EmotionType.Blush]);
            }

            foreach (var kvp in _customWeights)
            {
                proxy.ImmediatelySetValue(kvp.Key, kvp.Value);
            }
        }

        private void ApplyToMeshRenderer(bool applyFullEmotions)
        {
            if (faceRenderer == null || faceRenderer.sharedMesh == null) return;

            // Blush application on mesh
            if (_meshBlushIndex >= 0)
            {
                faceRenderer.SetBlendShapeWeight(_meshBlushIndex, _currentWeights[EmotionType.Blush] * 100f);
            }

            // Fallback for full emotions if VRM runtime isn't active yet
            if (applyFullEmotions)
            {
                foreach (var kvp in _meshBlendShapeIndices)
                {
                    if (kvp.Value >= 0 && _currentWeights.TryGetValue(kvp.Key, out float w))
                    {
                        faceRenderer.SetBlendShapeWeight(kvp.Value, w * 100f);
                    }
                }
            }
        }
    }
}
