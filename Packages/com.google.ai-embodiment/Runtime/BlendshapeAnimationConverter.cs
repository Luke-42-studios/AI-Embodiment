using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AIEmbodiment
{
    /// <summary>
    /// Converts BlendshapeAnimationData into Unity AnimationClips.
    /// Supports both editor-time and runtime conversion.
    ///
    /// KEY METHOD FOR MULTI-MESH: CreateAnimationClipForHierarchy()
    /// - Auto-discovers all SkinnedMeshRenderers in children
    /// - Fuzzy matches blendshape names (handles JAW_DROP vs jawdrop vs Body_geo.JAW_DROP)
    /// - Animates same blendshape across multiple meshes in unison (Body + Teeth + Tongue)
    ///
    /// FUTURE: Google Lipsync Plugin
    /// If Google's lipsync outputs JSON with blendshape keyframes:
    /// 1. Ensure JSON parses to BlendshapeAnimationData format (or create adapter)
    /// 2. Call CreateAnimationClipForHierarchy(data, characterRoot) for multi-mesh support
    /// 3. Play via BlendshapeAnimationPlayer or direct AnimatorOverrideController
    ///
    /// See also: ULipSyncAndroidXR.cs for real-time phoneme approach (temp solution)
    /// </summary>
    public static class BlendshapeAnimationConverter
    {
        /// <summary>
        /// Attempts to find a matching blendshape name using fuzzy matching.
        /// Handles case differences, underscores, hyphens, and dots.
        /// </summary>
        /// <param name="sourceName">Name from animation data (e.g., "JawDrop")</param>
        /// <param name="availableNames">Available blendshape names on the mesh</param>
        /// <returns>Best matching name from available names, or null if no match found</returns>
        public static string FindFuzzyBlendshapeMatch(string sourceName, HashSet<string> availableNames)
        {
            // First try exact match
            if (availableNames.Contains(sourceName))
                return sourceName;

            // Normalize the source name (lowercase, remove separators)
            string normalizedSource = NormalizeBlendshapeName(sourceName);

            // Try to find a match in available names
            foreach (string availableName in availableNames)
            {
                string normalizedAvailable = NormalizeBlendshapeName(availableName);
                if (normalizedSource == normalizedAvailable)
                {
                    return availableName; // Return the actual mesh blendshape name
                }
            }

            return null; // No match found
        }

        /// <summary>
        /// Normalizes blendshape name for fuzzy matching.
        /// Strips namespace prefixes (e.g., "Body_geo_blendshape.JAW_DROP" → "JAW_DROP")
        /// Converts to lowercase and removes separators (_, -, ., spaces)
        /// </summary>
        public static string NormalizeBlendshapeName(string name)
        {
            // Strip namespace/prefix (everything before last dot)
            // This handles names like "Body_geo_blendshape.JAW_DROP" → "JAW_DROP"
            int lastDotIndex = name.LastIndexOf('.');
            if (lastDotIndex >= 0 && lastDotIndex < name.Length - 1)
            {
                name = name.Substring(lastDotIndex + 1);
            }

            return name.ToLowerInvariant()
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "");
        }

        /// <summary>
        /// Creates a generic AnimationClip from blendshape data.
        /// The clip uses generic property paths like "blendShape.ShapeName".
        /// For SkinnedMeshRenderer-specific clips, use CreateAnimationClipForTarget.
        /// </summary>
        public static AnimationClip CreateGenericAnimationClip(BlendshapeAnimationData data, string clipName = "BlendshapeAnimation")
        {
            if (data == null || data.frames == null || data.frames.Count == 0)
            {
                Debug.LogWarning("Cannot create animation clip from null or empty data.");
                return null;
            }

            // Use fixed 25fps (PAL standard) - JSON frame count indicates 25fps timing
            const float fps = 25f;

            AnimationClip clip = new AnimationClip
            {
                name = clipName,
                frameRate = fps,
                legacy = false
            };

#if UNITY_EDITOR
            // Set sample rate for proper frame alignment
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
#endif

            // Get all unique blendshape names
            HashSet<string> blendshapeNames = data.GetAllBlendshapeNames();

            // Create animation curve for each blendshape
            Dictionary<string, AnimationCurve> curves = new Dictionary<string, AnimationCurve>();
            foreach (string shapeName in blendshapeNames)
            {
                curves[shapeName] = new AnimationCurve();
            }

            // Add keyframes from each frame using frame index
            for (int i = 0; i < data.frames.Count; i++)
            {
                float time = i / fps;
                var frame = data.frames[i];

                foreach (var kvp in frame.blendshapes)
                {
                    string shapeName = kvp.Key;
                    float weight = kvp.Value * 100f; // Unity blendshapes are 0-100, JSON appears to be 0-1

                    if (curves.ContainsKey(shapeName))
                    {
                        curves[shapeName].AddKey(time, weight);
                    }
                }
            }

            // Set all curves to the clip using generic blendShape property binding
            foreach (var kvp in curves)
            {
                string shapeName = kvp.Key;
                AnimationCurve curve = kvp.Value;

#if UNITY_EDITOR
                // Smooth the curve tangents (Editor-only for better interpolation)
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
#endif

                // Generic binding path for blendshapes (will need to be remapped for specific hierarchy)
                string propertyPath = $"blendShape.{shapeName}";
                clip.SetCurve("", typeof(SkinnedMeshRenderer), propertyPath, curve);
            }

            // Add bone rotation curves
            int boneCount = AddBoneRotationCurves(clip, data, "");

            Debug.Log($"Created animation clip '{clipName}' with {blendshapeNames.Count} blendshapes and {boneCount} bones over {data.GetDuration():F2}s");
            return clip;
        }

        /// <summary>
        /// Creates an AnimationClip targeted for a specific SkinnedMeshRenderer hierarchy.
        /// This version maps blendshapes to the actual transform path in the scene.
        /// </summary>
        /// <param name="data">Animation data</param>
        /// <param name="targetRenderer">Target SkinnedMeshRenderer (must be in scene or prefab)</param>
        /// <param name="clipName">Name for the clip</param>
        public static AnimationClip CreateAnimationClipForTarget(
            BlendshapeAnimationData data,
            SkinnedMeshRenderer targetRenderer,
            string clipName = "BlendshapeAnimation")
        {
            if (data == null || data.frames == null || data.frames.Count == 0)
            {
                Debug.LogWarning("Cannot create animation clip from null or empty data.");
                return null;
            }

            if (targetRenderer == null)
            {
                Debug.LogWarning("Target SkinnedMeshRenderer is null. Use CreateGenericAnimationClip for non-targeted clips.");
                return null;
            }

            // Use fixed 25fps (PAL standard) - JSON frame count indicates 25fps timing
            const float fps = 25f;

            AnimationClip clip = new AnimationClip
            {
                name = clipName,
                frameRate = fps,
                legacy = false
            };

#if UNITY_EDITOR
            // Set sample rate for proper frame alignment
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
#endif

            // Get the hierarchy path from the root to the SkinnedMeshRenderer
            string targetPath = GetHierarchyPath(targetRenderer.transform);

            // Build a map of available blendshape names on the target mesh
            Mesh mesh = targetRenderer.sharedMesh;
            if (mesh == null)
            {
                Debug.LogWarning($"SkinnedMeshRenderer {targetRenderer.name} has no shared mesh.");
                return null;
            }

            HashSet<string> availableBlendshapes = new HashSet<string>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                availableBlendshapes.Add(mesh.GetBlendShapeName(i));
            }

            // Get all blendshapes from animation data
            HashSet<string> animationBlendshapes = data.GetAllBlendshapeNames();

            // Create curves only for blendshapes that exist on the target mesh
            // Use fuzzy matching to handle naming variations
            Dictionary<string, AnimationCurve> curves = new Dictionary<string, AnimationCurve>();
            Dictionary<string, string> nameMapping = new Dictionary<string, string>(); // animData name -> mesh name
            int matchedCount = 0;
            int skippedCount = 0;

            foreach (string shapeName in animationBlendshapes)
            {
                string matchedName = FindFuzzyBlendshapeMatch(shapeName, availableBlendshapes);

                if (matchedName != null)
                {
                    curves[matchedName] = new AnimationCurve();
                    nameMapping[shapeName] = matchedName;
                    matchedCount++;

                    // Log if fuzzy match was used (names differ)
                    if (shapeName != matchedName)
                    {
                        Debug.Log($"<color=cyan>[Blendshape Mapping]</color> '{shapeName}' → '{matchedName}' (fuzzy match)");
                    }
                }
                else
                {
                    skippedCount++;
                    Debug.LogWarning($"<color=yellow>[Blendshape Skip]</color> '{shapeName}' from animation data not found on mesh '{mesh.name}'. No matching blendshape available.");
                }
            }

            Debug.Log($"<color=green>[Blendshape Match Summary]</color> Mesh: '{mesh.name}' | Matched: {matchedCount}/{animationBlendshapes.Count} | Skipped: {skippedCount}");

            // Add keyframes using frame index and name mapping
            for (int i = 0; i < data.frames.Count; i++)
            {
                float time = i / fps;
                var frame = data.frames[i];

                foreach (var kvp in frame.blendshapes)
                {
                    string animDataName = kvp.Key;
                    float weight = kvp.Value * 100f;

                    // Use the mapped name (from mesh) if available
                    if (nameMapping.TryGetValue(animDataName, out string meshName))
                    {
                        if (curves.ContainsKey(meshName))
                        {
                            curves[meshName].AddKey(time, weight);
                        }
                    }
                }
            }

            // Set curves to clip with proper hierarchy path
            foreach (var kvp in curves)
            {
                string shapeName = kvp.Key;
                AnimationCurve curve = kvp.Value;

#if UNITY_EDITOR
                // Smooth tangents (Editor-only for better interpolation)
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
#endif

                // Use the actual hierarchy path to the SkinnedMeshRenderer
                string propertyPath = $"blendShape.{shapeName}";
                clip.SetCurve(targetPath, typeof(SkinnedMeshRenderer), propertyPath, curve);
            }

            // Add bone rotation curves (relative to targetRenderer's root)
            Transform rootTransform = targetRenderer.transform;
            while (rootTransform.parent != null)
                rootTransform = rootTransform.parent;
            int boneCount = AddBoneRotationCurvesForHierarchy(clip, data, rootTransform);

            Debug.Log($"Created targeted animation clip '{clipName}' for '{targetRenderer.name}' with {curves.Count}/{animationBlendshapes.Count} blendshapes and {boneCount} bones over {data.GetDuration():F2}s");
            return clip;
        }

        /// <summary>
        /// Creates an AnimationClip by scanning all SkinnedMeshRenderers in a hierarchy.
        /// Handles multiple meshes with the same blendshape names (creates multiple tracks).
        /// Animator should be on the parent transform.
        /// </summary>
        /// <param name="data">Animation data</param>
        /// <param name="parentTransform">Parent transform containing Animator and child SkinnedMeshRenderers</param>
        /// <param name="clipName">Name for the clip</param>
        public static AnimationClip CreateAnimationClipForHierarchy(
            BlendshapeAnimationData data,
            Transform parentTransform,
            string clipName = "BlendshapeAnimation")
        {
            if (data == null || data.frames == null || data.frames.Count == 0)
            {
                Debug.LogWarning("Cannot create animation clip from null or empty data.");
                return null;
            }

            if (parentTransform == null)
            {
                Debug.LogWarning("Parent transform is null.");
                return null;
            }

            // Use fixed 25fps (PAL standard) - JSON frame count indicates 25fps timing
            const float fps = 25f;

            AnimationClip clip = new AnimationClip
            {
                name = clipName,
                frameRate = fps,
                legacy = false
            };

#if UNITY_EDITOR
            // Set sample rate for proper frame alignment
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
#endif

            // Find all SkinnedMeshRenderers in children
            SkinnedMeshRenderer[] renderers = parentTransform.GetComponentsInChildren<SkinnedMeshRenderer>();

            if (renderers.Length == 0)
            {
                Debug.LogWarning($"No SkinnedMeshRenderers found under '{parentTransform.name}'");
                return null;
            }

            Debug.Log($"<color=cyan>[Hierarchy Scan]</color> Found {renderers.Length} SkinnedMeshRenderer(s) under '{parentTransform.name}'");

            // Get all blendshapes from animation data
            HashSet<string> animationBlendshapes = data.GetAllBlendshapeNames();

            // Build mapping: animData blendshape name -> List of (renderer, meshBlendshapeName, hierarchyPath)
            Dictionary<string, List<(SkinnedMeshRenderer renderer, string meshName, string path)>> blendshapeMapping
                = new Dictionary<string, List<(SkinnedMeshRenderer, string, string)>>();

            int totalMatched = 0;
            int totalSkipped = 0;

            // Scan all renderers and build mapping
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMesh == null)
                    continue;

                Mesh mesh = renderer.sharedMesh;
                string hierarchyPath = GetHierarchyPath(renderer.transform, parentTransform);

                // Get available blendshapes from this mesh
                HashSet<string> availableBlendshapes = new HashSet<string>();
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    availableBlendshapes.Add(mesh.GetBlendShapeName(i));
                }

                Debug.Log($"  <color=gray>[Mesh]</color> '{renderer.name}' at '{hierarchyPath}' has {mesh.blendShapeCount} blendshape(s)");

                // Try to match each animation blendshape to this mesh
                foreach (string animBsName in animationBlendshapes)
                {
                    string matchedName = FindFuzzyBlendshapeMatch(animBsName, availableBlendshapes);

                    if (matchedName != null)
                    {
                        if (!blendshapeMapping.ContainsKey(animBsName))
                            blendshapeMapping[animBsName] = new List<(SkinnedMeshRenderer, string, string)>();

                        blendshapeMapping[animBsName].Add((renderer, matchedName, hierarchyPath));
                        totalMatched++;

                        // Log fuzzy matches
                        if (animBsName != matchedName)
                        {
                            Debug.Log($"    <color=cyan>[Match]</color> '{animBsName}' → '{matchedName}' on '{renderer.name}' (fuzzy)");
                        }
                    }
                }
            }

            // Report unmatched blendshapes
            foreach (string animBsName in animationBlendshapes)
            {
                if (!blendshapeMapping.ContainsKey(animBsName))
                {
                    totalSkipped++;
                    Debug.LogWarning($"  <color=yellow>[Skip]</color> '{animBsName}' not found on any mesh under '{parentTransform.name}'");
                }
            }

            Debug.Log($"<color=green>[Match Summary]</color> Total matches: {totalMatched} | Skipped: {totalSkipped}/{animationBlendshapes.Count}");

            // Create animation curves for all matched blendshapes
            // Key: (hierarchyPath, meshBlendshapeName), Value: AnimationCurve
            Dictionary<(string, string), AnimationCurve> curves = new Dictionary<(string, string), AnimationCurve>();

            foreach (var kvp in blendshapeMapping)
            {
                string animBsName = kvp.Key;
                var targets = kvp.Value;

                // Create curves for each target (handles duplicates across meshes)
                foreach (var (renderer, meshName, path) in targets)
                {
                    var key = (path, meshName);
                    if (!curves.ContainsKey(key))
                    {
                        curves[key] = new AnimationCurve();
                    }
                }

                // Log if same blendshape appears on multiple meshes
                if (targets.Count > 1)
                {
                    Debug.Log($"  <color=magenta>[Multi-Mesh]</color> '{animBsName}' will animate {targets.Count} mesh(es) in unison");
                }
            }

            // Add keyframes using name mapping
            for (int i = 0; i < data.frames.Count; i++)
            {
                float time = i / fps;
                var frame = data.frames[i];

                foreach (var kvp in frame.blendshapes)
                {
                    string animDataName = kvp.Key;
                    float weight = kvp.Value * 100f;

                    // Check if this blendshape has any mappings
                    if (blendshapeMapping.TryGetValue(animDataName, out var targets))
                    {
                        // Add keyframe to all target meshes
                        foreach (var (renderer, meshName, path) in targets)
                        {
                            var key = (path, meshName);
                            if (curves.ContainsKey(key))
                            {
                                curves[key].AddKey(time, weight);
                            }
                        }
                    }
                }
            }

            // Set curves on clip
            foreach (var kvp in curves)
            {
                var (path, meshName) = kvp.Key;
                AnimationCurve curve = kvp.Value;

#if UNITY_EDITOR
                // Smooth tangents
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
#endif

                string propertyPath = $"blendShape.{meshName}";
                clip.SetCurve(path, typeof(SkinnedMeshRenderer), propertyPath, curve);
            }

            // Add bone rotation curves with duplicate detection
            int boneCount = AddBoneRotationCurvesForHierarchy(clip, data, parentTransform);

            Debug.Log($"<color=green>[Complete]</color> Created animation clip '{clipName}' with {curves.Count} blendshape track(s) and {boneCount} bone(s) over {data.GetDuration():F2}s");
            return clip;
        }

        /// <summary>
        /// Gets the hierarchy path from parent to target transform.
        /// Returns empty string if transform is the parent itself.
        /// </summary>
        private static string GetHierarchyPath(Transform transform, Transform parent)
        {
            if (transform == parent)
                return "";

            string path = transform.name;
            Transform current = transform.parent;

            while (current != null && current != parent)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// Gets the hierarchy path from the root to the given transform.
        /// Returns empty string if transform is root.
        /// </summary>
        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            // Remove the root name (we want path relative to animated GameObject root)
            int firstSlash = path.IndexOf('/');
            if (firstSlash >= 0)
            {
                path = path.Substring(firstSlash + 1);
            }
            else
            {
                path = ""; // This is the root
            }

            return path;
        }

        /// <summary>
        /// Parses JSON text into BlendshapeAnimationData.
        /// Uses Newtonsoft.Json for proper Dictionary support.
        /// </summary>
        public static BlendshapeAnimationData ParseJson(string jsonText)
        {
            try
            {
                return JsonConvert.DeserializeObject<BlendshapeAnimationData>(jsonText);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse blendshape animation JSON: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Adds bone rotation curves to an AnimationClip from the data.
        /// Returns the number of bones added.
        /// </summary>
        private static int AddBoneRotationCurves(AnimationClip clip, BlendshapeAnimationData data, string basePath)
        {
            if (data.boneAnimations == null || data.boneAnimations.Count == 0)
                return 0;

            int boneCount = 0;

            foreach (var boneEntry in data.boneAnimations)
            {
                string boneName = boneEntry.Key;
                BoneAnimation boneAnim = boneEntry.Value;

                if (boneAnim.rotationFrames == null || boneAnim.rotationFrames.Count == 0)
                    continue;

                // Calculate FPS from first frame
                float fps = 30f;
                if (boneAnim.rotationFrames.Count > 0 && boneAnim.rotationFrames[0].timeSeconds > 0)
                {
                    fps = 1f / boneAnim.rotationFrames[0].timeSeconds;
                }

                // Create curves for each quaternion component (x, y, z, w)
                AnimationCurve curveX = new AnimationCurve();
                AnimationCurve curveY = new AnimationCurve();
                AnimationCurve curveZ = new AnimationCurve();
                AnimationCurve curveW = new AnimationCurve();

                // Add keyframes using frame index
                for (int i = 0; i < boneAnim.rotationFrames.Count; i++)
                {
                    float time = i / fps;
                    Quaternion rotation = boneAnim.rotationFrames[i].ToQuaternion();
                    curveX.AddKey(time, rotation.x);
                    curveY.AddKey(time, rotation.y);
                    curveZ.AddKey(time, rotation.z);
                    curveW.AddKey(time, rotation.w);
                }

#if UNITY_EDITOR
                // Smooth tangents for better interpolation
                SmoothCurveTangents(curveX);
                SmoothCurveTangents(curveY);
                SmoothCurveTangents(curveZ);
                SmoothCurveTangents(curveW);
#endif

                // Set curves to clip with proper bone path
                string bonePath = string.IsNullOrEmpty(basePath) ? boneName : $"{basePath}/{boneName}";
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", curveX);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", curveY);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", curveZ);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.w", curveW);

                boneCount++;
            }

            return boneCount;
        }

        /// <summary>
        /// Adds bone rotation curves for a specific hierarchy root.
        /// Finds bones in the hierarchy and maps them to the animation data.
        /// </summary>
        private static int AddBoneRotationCurvesForHierarchy(AnimationClip clip, BlendshapeAnimationData data, Transform root)
        {
            if (data.boneAnimations == null || data.boneAnimations.Count == 0)
                return 0;

            int boneCount = 0;

            // Build a dictionary of bone names to their hierarchy paths
            Dictionary<string, string> boneNameToPath = new Dictionary<string, string>();
            FindAllBonesInHierarchy(root, "", boneNameToPath);

            foreach (var boneEntry in data.boneAnimations)
            {
                string boneName = boneEntry.Key;
                BoneAnimation boneAnim = boneEntry.Value;

                if (boneAnim.rotationFrames == null || boneAnim.rotationFrames.Count == 0)
                    continue;

                // Find the bone in the hierarchy
                if (!boneNameToPath.ContainsKey(boneName))
                {
                    Debug.LogWarning($"Bone '{boneName}' from animation data not found in hierarchy. Skipping.");
                    continue;
                }

                string bonePath = boneNameToPath[boneName];

                // Calculate FPS from first frame
                float fps = 30f;
                if (boneAnim.rotationFrames.Count > 0 && boneAnim.rotationFrames[0].timeSeconds > 0)
                {
                    fps = 1f / boneAnim.rotationFrames[0].timeSeconds;
                }

                // Create curves for each quaternion component
                AnimationCurve curveX = new AnimationCurve();
                AnimationCurve curveY = new AnimationCurve();
                AnimationCurve curveZ = new AnimationCurve();
                AnimationCurve curveW = new AnimationCurve();

                // Add keyframes using frame index
                for (int i = 0; i < boneAnim.rotationFrames.Count; i++)
                {
                    float time = i / fps;
                    Quaternion rotation = boneAnim.rotationFrames[i].ToQuaternion();
                    curveX.AddKey(time, rotation.x);
                    curveY.AddKey(time, rotation.y);
                    curveZ.AddKey(time, rotation.z);
                    curveW.AddKey(time, rotation.w);
                }

#if UNITY_EDITOR
                // Smooth tangents
                SmoothCurveTangents(curveX);
                SmoothCurveTangents(curveY);
                SmoothCurveTangents(curveZ);
                SmoothCurveTangents(curveW);
#endif

                // Set curves
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", curveX);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", curveY);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", curveZ);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.w", curveW);

                boneCount++;
            }

            return boneCount;
        }

        /// <summary>
        /// Recursively finds all bones in a hierarchy and builds a map of bone names to their paths.
        /// Errors if duplicate bone names are found (unlikely but unwanted for joints).
        /// </summary>
        private static void FindAllBonesInHierarchy(Transform current, string currentPath, Dictionary<string, string> boneMap)
        {
            // Check for duplicate bone names
            if (boneMap.ContainsKey(current.name))
            {
                Debug.LogError($"<color=red>[Duplicate Joint Error]</color> Joint name '{current.name}' appears multiple times in hierarchy! " +
                    $"Existing path: '{boneMap[current.name]}', New path: '{currentPath}'. " +
                    $"This is unwanted for bone animations. Please rename one of these joints.");
                // Don't overwrite - keep first occurrence
                return;
            }

            // Add current bone
            boneMap[current.name] = currentPath;

            // Recurse to children
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                string childPath = string.IsNullOrEmpty(currentPath) ? child.name : $"{currentPath}/{child.name}";
                FindAllBonesInHierarchy(child, childPath, boneMap);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Smooths tangents for an animation curve using ClampedAuto mode.
        /// Editor-only helper method.
        /// </summary>
        private static void SmoothCurveTangents(AnimationCurve curve)
        {
            for (int i = 0; i < curve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }
#endif
    }
}
