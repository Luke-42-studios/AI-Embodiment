using Newtonsoft.Json.Linq;

namespace AIEmbodiment
{
    /// <summary>Configuration for a GeminiLiveClient connection.</summary>
    public class GeminiLiveConfig
    {
        public string ApiKey;
        public string Model = "gemini-2.5-flash-native-audio-preview-12-2025";
        public string SystemInstruction;
        public string VoiceName = "Puck";
        public int AudioInputSampleRate = 16000;
        public int AudioOutputSampleRate = 24000;

        /// <summary>
        /// Tool declarations for native function calling. Set by PersonaSession from
        /// <see cref="FunctionRegistry.BuildToolsJson()"/>. Null means no tools.
        /// </summary>
        public JArray ToolsJson;
    }
}
