using System.Collections.Generic;
using UnityEngine;
using AIEmbodiment;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Consumes streaming <see cref="BlendshapeFrame"/> events from <see cref="Audio2Animation"/>
    /// and applies them in real-time to <see cref="SkinnedMeshRenderer"/>(s) on the Android XR
    /// face rig via direct <c>SetBlendShapeWeight</c> calls.
    ///
    /// <para>
    /// This MonoBehaviour drives <see cref="FakeAnimationModel.Tick"/> each frame and manages
    /// a frame queue for smooth playback. Blendshape name-to-index mappings are cached at
    /// initialization to avoid per-frame string allocations. Fuzzy matching via
    /// <see cref="BlendshapeAnimationConverter.NormalizeBlendshapeName"/> handles naming
    /// variations between JSON data and mesh (e.g., JawDrop vs Body_geo.JAW_DROP).
    /// </para>
    ///
    /// <para>
    /// Full pipeline: PersonaSession.OnSyncPacket -> Audio2Animation.ProcessPacket ->
    /// FakeAnimationModel.ProcessAudio -> Tick -> OnFrameReady -> EnqueueFrame ->
    /// Update -> ApplyFrame -> SetBlendShapeWeight.
    /// </para>
    /// </summary>
    public class FaceAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private PersonaSession _personaSession;
        [SerializeField] private Transform _faceRigRoot;

        [Tooltip("Pre-recorded JSON animation data files (assign animDemo1-4.json in Inspector).")]
        [SerializeField] private TextAsset[] _animationDataFiles;

        [Tooltip("Which animation data file index to use (0-based). Defaults to 0.")]
        [SerializeField] private int _activeDataIndex = 0;

        private Audio2Animation _audio2Animation;
        private FakeAnimationModel _fakeModel;
        private readonly Queue<BlendshapeFrame> _frameQueue = new Queue<BlendshapeFrame>();
        private List<RendererMapping> _rendererMappings;

        /// <summary>
        /// Cached mapping of normalized blendshape names to mesh indices for a single
        /// <see cref="SkinnedMeshRenderer"/>. Built once at initialization to avoid
        /// per-frame string allocations.
        /// </summary>
        private struct RendererMapping
        {
            public SkinnedMeshRenderer Renderer;
            public Dictionary<string, int> BlendshapeIndexMap; // normalized name -> index
        }

        private void Awake()
        {
            // Validate inspector assignments
            if (_personaSession == null)
            {
                Debug.LogError("FaceAnimationPlayer: PersonaSession is not assigned.");
                return;
            }

            if (_faceRigRoot == null)
            {
                Debug.LogError("FaceAnimationPlayer: Face rig root Transform is not assigned.");
                return;
            }

            if (_animationDataFiles == null || _animationDataFiles.Length == 0)
            {
                Debug.LogError("FaceAnimationPlayer: No animation data files assigned.");
                return;
            }

            // Clamp active data index to valid range
            _activeDataIndex = Mathf.Clamp(_activeDataIndex, 0, _animationDataFiles.Length - 1);

            // Find first non-null TextAsset if the selected index is null
            TextAsset activeAsset = _animationDataFiles[_activeDataIndex];
            if (activeAsset == null)
            {
                for (int i = 0; i < _animationDataFiles.Length; i++)
                {
                    if (_animationDataFiles[i] != null)
                    {
                        activeAsset = _animationDataFiles[i];
                        _activeDataIndex = i;
                        break;
                    }
                }
            }

            if (activeAsset == null)
            {
                Debug.LogError("FaceAnimationPlayer: All animation data file slots are null.");
                return;
            }

            // Parse animation data from JSON
            var animData = BlendshapeAnimationConverter.ParseJson(activeAsset.text);
            if (animData == null)
            {
                Debug.LogError($"FaceAnimationPlayer: Failed to parse animation data from '{activeAsset.name}'.");
                return;
            }

            // Create FakeAnimationModel and Audio2Animation orchestrator
            _fakeModel = new FakeAnimationModel(animData);
            _audio2Animation = new Audio2Animation(_fakeModel);
            _audio2Animation.OnFrameReady += EnqueueFrame;

            // Build cached blendshape index maps for all renderers
            BuildRendererMappings();
        }

        private void OnEnable()
        {
            if (_personaSession == null) return;

            _personaSession.OnSyncPacket += HandleSyncPacket;
            _personaSession.OnInterrupted += HandleInterrupted;
        }

        private void OnDisable()
        {
            if (_personaSession == null) return;

            _personaSession.OnSyncPacket -= HandleSyncPacket;
            _personaSession.OnInterrupted -= HandleInterrupted;
        }

        /// <summary>
        /// Scans all <see cref="SkinnedMeshRenderer"/>s under <see cref="_faceRigRoot"/>
        /// and builds cached blendshape name-to-index mappings. Both original and normalized
        /// names are stored for O(1) lookup during frame application.
        /// </summary>
        private void BuildRendererMappings()
        {
            _rendererMappings = new List<RendererMapping>();

            var renderers = _faceRigRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            int totalBlendshapes = 0;

            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                var map = new Dictionary<string, int>();

                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);

                    // Store original name -> index
                    map[name] = i;

                    // Store normalized name -> index (for fuzzy matching)
                    string normalized = BlendshapeAnimationConverter.NormalizeBlendshapeName(name);
                    if (!map.ContainsKey(normalized))
                    {
                        map[normalized] = i;
                    }

                    totalBlendshapes++;
                }

                _rendererMappings.Add(new RendererMapping
                {
                    Renderer = renderer,
                    BlendshapeIndexMap = map
                });
            }

            Debug.Log($"FaceAnimationPlayer: Built mappings for {_rendererMappings.Count} renderer(s), {totalBlendshapes} blendshape(s) cached.");
        }

        /// <summary>
        /// Forwards <see cref="SyncPacket"/> audio to <see cref="Audio2Animation"/> for processing.
        /// </summary>
        private void HandleSyncPacket(SyncPacket packet)
        {
            _audio2Animation.ProcessPacket(packet);
        }

        /// <summary>
        /// Handles barge-in interruption: cancels animation and clears the frame queue,
        /// resetting the face to neutral.
        /// </summary>
        private void HandleInterrupted()
        {
            _audio2Animation.Cancel();
            ClearQueue();
        }

        /// <summary>
        /// Enqueues a frame produced by <see cref="Audio2Animation.OnFrameReady"/> for
        /// application in the next <see cref="Update"/> cycle.
        /// </summary>
        private void EnqueueFrame(BlendshapeFrame frame)
        {
            _frameQueue.Enqueue(frame);
        }

        /// <summary>
        /// Clears the frame queue and resets all blendshape weights to 0 on all renderers,
        /// returning the face to a neutral pose.
        /// </summary>
        private void ClearQueue()
        {
            _frameQueue.Clear();

            if (_rendererMappings == null) return;

            foreach (var mapping in _rendererMappings)
            {
                if (mapping.Renderer == null || mapping.Renderer.sharedMesh == null) continue;

                for (int i = 0; i < mapping.Renderer.sharedMesh.blendShapeCount; i++)
                {
                    mapping.Renderer.SetBlendShapeWeight(i, 0f);
                }
            }
        }

        private void Update()
        {
            // Drive FakeAnimationModel tick BEFORE draining queue so newly produced
            // frames are immediately available this frame
            _fakeModel?.Tick(Time.deltaTime);

            // Drain up to a reasonable number of frames per Update to prevent queue buildup
            // but typically only 1 frame is ready per Update at 30fps animation / 60+ fps display
            int maxFramesPerUpdate = 3;
            int framesApplied = 0;
            while (_frameQueue.Count > 0 && framesApplied < maxFramesPerUpdate)
            {
                var frame = _frameQueue.Dequeue();
                ApplyFrame(frame);
                framesApplied++;
            }
        }

        /// <summary>
        /// Applies a single <see cref="BlendshapeFrame"/> to all cached renderers.
        /// Converts weight values from 0-1 (JSON) to 0-100 (Unity) scale.
        /// Uses normalized name lookup for fuzzy matching.
        /// </summary>
        private void ApplyFrame(BlendshapeFrame frame)
        {
            if (frame.blendshapes == null || _rendererMappings == null) return;

            foreach (var mapping in _rendererMappings)
            {
                if (mapping.Renderer == null) continue;

                foreach (var kvp in frame.blendshapes)
                {
                    string normalized = BlendshapeAnimationConverter.NormalizeBlendshapeName(kvp.Key);

                    if (mapping.BlendshapeIndexMap.TryGetValue(normalized, out int index))
                    {
                        // Convert 0-1 (JSON) to 0-100 (Unity SetBlendShapeWeight) -- Pitfall 3
                        mapping.Renderer.SetBlendShapeWeight(index, kvp.Value * 100f);
                    }
                }
            }
        }
    }
}
