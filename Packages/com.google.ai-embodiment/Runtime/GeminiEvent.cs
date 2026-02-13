namespace AIEmbodiment
{
    /// <summary>Event types produced by GeminiLiveClient.</summary>
    public enum GeminiEventType
    {
        Audio,
        OutputTranscription,
        InputTranscription,
        TurnComplete,
        Interrupted,
        FunctionCall,
        Connected,
        Disconnected,
        Error
    }

    /// <summary>A single event from the Gemini Live session.</summary>
    public struct GeminiEvent
    {
        public GeminiEventType Type;
        public string Text;
        public float[] AudioData;
        public int AudioSampleRate;
        public string FunctionName;
        public string FunctionArgsJson;
    }
}
