using System.Collections.Generic;
using UnityEngine;
using AIEmbodiment; // For BlendshapeAnimationData, FakeAnimationModel, etc.

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Animates a SkinnedMeshRenderer using a FakeAnimationModel driven by conversation audio.
    /// Loads blendshape data from a TextAsset (JSON) and maps it to the mesh.
    /// </summary>
    public class FaceAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SkinnedMeshRenderer _faceMesh;
        [SerializeField] private TextAsset _animationJson;

        private FakeAnimationModel _model;
        private Dictionary<string, int> _blendShapeIndexMap;
        private Mesh _sharedMesh;
        

        
        // Cache available blendshape names for fuzzy matching
        private HashSet<string> _availableBlendshapeNames;
        
        private void OnEnable()
        {
            Debug.Log("[FaceAnimator] OnEnable called.");
        }
        
        private void OnDisable()
        {
            Debug.Log("[FaceAnimator] OnDisable called.");
        }

        private void Start()
        {
            Debug.Log($"[FaceAnimator] Start. Mesh assigned: {_faceMesh != null}, Json assigned: {_animationJson != null}");
            if (_faceMesh == null)
            {
                Debug.LogWarning("[FaceAnimator] Face Mesh is not assigned. Blendshapes will be skipped.");
            }

            if (_animationJson == null)
            {
                Debug.LogWarning("[FaceAnimator] Animation JSON is not assigned.");
                return;
            }

            // 1. Load Data
            BlendshapeAnimationData data = BlendshapeAnimationConverter.ParseJson(_animationJson.text);
            if (data == null)
            {
                Debug.LogError("[FaceAnimator] Failed to parse animation data.");
                return;
            }

            // 2. Initialize Model
            _model = new FakeAnimationModel(data);
            _model.SetFrameCallback(OnFrameReady);
            Debug.Log("[FaceAnimator] Model initialized.");

            // 3. Map Blendshapes (JSON Name -> Mesh Index)
            _sharedMesh = _faceMesh.sharedMesh;
            _blendShapeIndexMap = new Dictionary<string, int>();
            _availableBlendshapeNames = new HashSet<string>();

            if (_faceMesh != null)
            {
                _sharedMesh = _faceMesh.sharedMesh;
                for (int i = 0; i < _sharedMesh.blendShapeCount; i++)
                {
                    _availableBlendshapeNames.Add(_sharedMesh.GetBlendShapeName(i));
                }
            }

            var animNames = data.GetAllBlendshapeNames();
            foreach (var animName in animNames)
            {
                if (_faceMesh == null) break;

                // Use the library's fuzzy matcher
                string meshName = BlendshapeAnimationConverter.FindFuzzyBlendshapeMatch(animName, _availableBlendshapeNames);
                if (!string.IsNullOrEmpty(meshName))
                {
                    int index = _sharedMesh.GetBlendShapeIndex(meshName);
                    if (index >= 0)
                    {
                        _blendShapeIndexMap[animName] = index;
                    }
                }
            }
            
            Debug.Log($"[FaceAnimator] Mapped {_blendShapeIndexMap.Count} / {animNames.Count} blendshapes.");
            

        }



        private void Update()
        {
            if (Time.frameCount % 60 == 0 && _model != null)
            {
               // Debug.Log($"[FaceAnimator] Update. Model Budget: {_model.AnimationBudgetSeconds:F3}");
            }

            if (_model != null)
            {
                _model.Tick(Time.deltaTime);
            }
        }

        /// <summary>
        /// Feed audio samples to the animation model to advance the animation budget.
        /// </summary>
        public void ProcessAudio(float[] samples, int sampleRate = 24000)
        {
            Debug.Log($"[FaceAnimator] ProcessAudio (wrapper). Samples: {samples.Length}");
            if (_model != null)
            {
                _model.ProcessAudio(samples, sampleRate);
            }
            else
            {
                Debug.LogWarning("[FaceAnimator] ProcessAudio called but Model is null.");
            }
        }

        /// <summary>
        /// Cancels current animation and resets budget.
        /// </summary>
        public void Cancel()
        {
            Debug.Log("[FaceAnimator] Cancel called.");
            if (_model != null)
            {
                _model.Cancel();
                _model.Reset();
            }
            ResetToNeutral();
        }

        public void ResetToNeutral()
        {
            Debug.Log("[FaceAnimator] ResetToNeutral.");
            if (_faceMesh != null && _blendShapeIndexMap != null)
            {
                foreach (var index in _blendShapeIndexMap.Values)
                {
                    _faceMesh.SetBlendShapeWeight(index, 0f);
                }
            }
        }

        private void OnFrameReady(BlendshapeFrame frame)
        {
            Debug.Log($"[FaceAnimator] OnFrameReady. Frame Index: {_model.CurrentFrameIndex}");
            
            // Apply Blendshapes
            if (_faceMesh != null && frame.blendshapes != null && _blendShapeIndexMap != null)
            {
                foreach (var kvp in frame.blendshapes)
                {
                    if (_blendShapeIndexMap.TryGetValue(kvp.Key, out int index))
                    {
                        // JSON weights are typically 0-1, Unity uses 0-100
                        float weight = kvp.Value * 100f;
                        _faceMesh.SetBlendShapeWeight(index, weight);
                    }
                }
            }
        }
    }
}
