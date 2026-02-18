using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Developer-facing configuration for an AI persona.
    /// Create via Assets > Create > AI Embodiment > Persona Config.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPersonaConfig", menuName = "AI Embodiment/Persona Config")]
    public class PersonaConfig : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "New Persona";
        public string archetype = "companion";

        [TextArea(3, 10)]
        public string backstory;

        [Header("Personality")]
        public string[] personalityTraits;

        [TextArea(2, 5)]
        public string speechPatterns;

        [Header("Model")]
        public string modelName = "gemini-2.5-flash-native-audio-preview-12-2025";

        [Range(0f, 2f)]
        public float temperature = 0.7f;

        [Header("Voice")]
        public VoiceBackend voiceBackend = VoiceBackend.GeminiNative;
        public string geminiVoiceName = "Puck";

        // Chirp TTS configuration
        public string chirpLanguageCode = "en-US";
        public string chirpVoiceShortName = "Achernar";
        public string chirpVoiceName = "en-US-Chirp3-HD-Achernar";
        public TTSSynthesisMode synthesisMode = TTSSynthesisMode.SentenceBySentence;
        public string customVoiceName = "";
        public string voiceCloningKey = "";

        /// <summary>True when Chirp backend with a custom (cloned) voice is configured.</summary>
        public bool IsCustomChirpVoice => voiceBackend == VoiceBackend.ChirpTTS
            && chirpVoiceShortName == ChirpVoiceList.CustomVoice;

        [SerializeField] private MonoBehaviour _customTTSProvider;

        /// <summary>
        /// Returns the custom TTS provider if assigned and valid (implements ITTSProvider).
        /// Null otherwise.
        /// </summary>
        public ITTSProvider CustomTTSProvider => _customTTSProvider as ITTSProvider;
    }
}
