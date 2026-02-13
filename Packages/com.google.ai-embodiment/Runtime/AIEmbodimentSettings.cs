using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Project-wide settings for the AI Embodiment package.
    /// Create via Assets > Create > AI Embodiment > Settings, then place in a Resources folder.
    /// Provides the Google AI API key used by GeminiLiveClient and ChirpTTSClient.
    /// </summary>
    [CreateAssetMenu(fileName = "AIEmbodimentSettings", menuName = "AI Embodiment/Settings")]
    public class AIEmbodimentSettings : ScriptableObject
    {
        private const string ResourcePath = "AIEmbodimentSettings";

        [SerializeField] private string _apiKey = "";

        /// <summary>Google AI API key for Gemini Live and Cloud TTS.</summary>
        public string ApiKey => _apiKey;

        private static AIEmbodimentSettings _instance;

        /// <summary>
        /// Loads the singleton settings asset from Resources.
        /// Returns null if no asset exists (caller should log a helpful error).
        /// </summary>
        public static AIEmbodimentSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<AIEmbodimentSettings>(ResourcePath);
                }
                return _instance;
            }
        }
    }
}
