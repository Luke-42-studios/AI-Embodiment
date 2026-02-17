using System;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// ScriptableObject defining available character animations that Gemini can trigger
    /// via function calls. Each entry has a name (used as the enum value) and a description
    /// (helps Gemini understand when to use the animation).
    /// Create via Assets > Create > AI Embodiment > Samples > Animation Config.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationConfig", menuName = "AI Embodiment/Samples/Animation Config")]
    public class AnimationConfig : ScriptableObject
    {
        [Serializable]
        public class AnimationEntry
        {
            [Tooltip("Animation name passed to function call (e.g. 'wave').")]
            public string name;

            [Tooltip("Description for Gemini to understand when to use this animation.")]
            [TextArea(1, 2)]
            public string description;
        }

        [Header("Available Animations")]
        [Tooltip("Animations Aya can trigger via function calls.")]
        public AnimationEntry[] animations;

        /// <summary>
        /// Returns an array of animation names for use with FunctionDeclaration.AddEnum.
        /// </summary>
        public string[] GetAnimationNames()
        {
            if (animations == null) return Array.Empty<string>();
            var names = new string[animations.Length];
            for (int i = 0; i < animations.Length; i++)
                names[i] = animations[i].name;
            return names;
        }
    }
}
