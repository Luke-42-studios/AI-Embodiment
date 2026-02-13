namespace AIEmbodiment
{
    /// <summary>
    /// Selects which voice synthesis backend a persona uses.
    /// </summary>
    public enum VoiceBackend
    {
        /// <summary>Uses Gemini Live API's built-in voice synthesis.</summary>
        GeminiNative,

        /// <summary>Routes text to Cloud TTS Chirp 3 HD (implemented in Phase 5).</summary>
        ChirpTTS,

        /// <summary>Routes text to a developer-supplied ITTSProvider MonoBehaviour.</summary>
        Custom
    }
}
