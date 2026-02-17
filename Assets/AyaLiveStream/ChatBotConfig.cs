using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Defines a chat bot persona for the livestream sample.
    /// Each bot has a unique identity, personality, pool of scripted messages,
    /// and behavior settings that control typing cadence, capitalization, and emoji usage.
    /// Create via Assets > Create > AI Embodiment > Samples > Chat Bot Config.
    /// </summary>
    [CreateAssetMenu(fileName = "NewChatBotConfig", menuName = "AI Embodiment/Samples/Chat Bot Config")]
    public class ChatBotConfig : ScriptableObject
    {
        [Header("Identity")]
        public string botName = "New Bot";
        public Color chatColor = Color.white;

        [Header("Personality")]
        [TextArea(3, 8)]
        public string personality;
        public string[] speechTraits;

        [Header("Scripted Messages")]
        [TextArea(1, 3)]
        public string[] scriptedMessages;

        [Header("Message Alternatives")]
        [TextArea(1, 3)]
        public string[] messageAlternatives;

        [Header("Behavior")]
        [Range(0.5f, 5f)]
        public float typingSpeed = 1.5f;

        [Range(0f, 1f)]
        public float capsFrequency = 0.1f;

        [Range(0f, 1f)]
        public float emojiFrequency = 0.2f;
    }
}
