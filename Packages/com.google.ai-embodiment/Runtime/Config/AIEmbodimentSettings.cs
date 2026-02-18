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
        [SerializeField] private string _serviceAccountJsonPath = "";

        /// <summary>Google AI API key for Gemini Live and Cloud TTS.</summary>
        public string ApiKey => _apiKey;

        /// <summary>
        /// File path to a Google service account JSON key file.
        /// Used for OAuth2 bearer token auth with Chirp custom voice cloning (v1beta1).
        /// </summary>
        public string ServiceAccountJsonPath => _serviceAccountJsonPath;

        /// <summary>
        /// Reads the service account JSON from the configured file path.
        /// Returns null if the path is empty or the file does not exist.
        /// Editor-only: service account files should never be in Resources or StreamingAssets.
        /// </summary>
        public string LoadServiceAccountJson()
        {
            if (string.IsNullOrEmpty(_serviceAccountJsonPath))
                return null;

            if (!System.IO.File.Exists(_serviceAccountJsonPath))
            {
                Debug.LogError($"Service account JSON not found: {_serviceAccountJsonPath}");
                return null;
            }

            return System.IO.File.ReadAllText(_serviceAccountJsonPath);
        }

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
