using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Root container for blendshape animation data deserialized from JSON.
    /// Contains an array of frames with timing and blendshape weights,
    /// plus bone animations with rotation data.
    /// </summary>
    [Serializable]
    public class BlendshapeAnimationData
    {
        public List<BlendshapeFrame> frames = new List<BlendshapeFrame>();
        public Dictionary<string, BoneAnimation> boneAnimations = new Dictionary<string, BoneAnimation>();

        /// <summary>
        /// Gets the total duration of the animation in seconds.
        /// Calculates from both blendshape frames and bone animation frames.
        /// Note: timeSeconds is frame duration (e.g., 0.0333s = 30fps), not cumulative time.
        /// </summary>
        public float GetDuration()
        {
            float maxDuration = 0f;

            // Check blendshape frames
            if (frames != null && frames.Count > 0)
            {
                float fps = frames[0].timeSeconds > 0 ? 1f / frames[0].timeSeconds : 30f;
                maxDuration = frames.Count / fps;
            }

            // Check bone animation frames
            if (boneAnimations != null)
            {
                foreach (var boneAnim in boneAnimations.Values)
                {
                    if (boneAnim.rotationFrames != null && boneAnim.rotationFrames.Count > 0)
                    {
                        float fps = boneAnim.rotationFrames[0].timeSeconds > 0 ? 1f / boneAnim.rotationFrames[0].timeSeconds : 30f;
                        float duration = boneAnim.rotationFrames.Count / fps;
                        if (duration > maxDuration)
                            maxDuration = duration;
                    }
                }
            }

            return maxDuration;
        }

        /// <summary>
        /// Gets all unique blendshape names across all frames.
        /// </summary>
        public HashSet<string> GetAllBlendshapeNames()
        {
            HashSet<string> names = new HashSet<string>();
            if (frames == null)
                return names;

            foreach (var frame in frames)
            {
                if (frame.blendshapes != null)
                {
                    foreach (var key in frame.blendshapes.Keys)
                    {
                        names.Add(key);
                    }
                }
            }
            return names;
        }

        /// <summary>
        /// Gets all unique bone names from bone animations.
        /// </summary>
        public HashSet<string> GetAllBoneNames()
        {
            HashSet<string> names = new HashSet<string>();
            if (boneAnimations != null)
            {
                foreach (var boneName in boneAnimations.Keys)
                {
                    names.Add(boneName);
                }
            }
            return names;
        }
    }

    /// <summary>
    /// Single frame of blendshape animation data.
    /// Contains timestamp and dictionary of blendshape name/weight pairs.
    /// </summary>
    [Serializable]
    public class BlendshapeFrame
    {
        public float timeSeconds;
        public Dictionary<string, float> blendshapes = new Dictionary<string, float>();
    }

    /// <summary>
    /// Bone animation data containing rotation keyframes for a single bone.
    /// </summary>
    [Serializable]
    public class BoneAnimation
    {
        public List<RotationFrame> rotationFrames = new List<RotationFrame>();
    }

    /// <summary>
    /// Single rotation keyframe for bone animation.
    /// Contains timestamp and quaternion rotation (as 4-element array: x, y, z, w).
    /// </summary>
    [Serializable]
    public class RotationFrame
    {
        public float timeSeconds;
        public float[] transform = new float[4]; // Quaternion: [x, y, z, w]

        /// <summary>
        /// Converts the transform array to a Unity Quaternion.
        /// </summary>
        public Quaternion ToQuaternion()
        {
            if (transform == null || transform.Length < 4)
                return Quaternion.identity;

            return new Quaternion(transform[0], transform[1], transform[2], transform[3]);
        }
    }
}
