using System.Collections.Generic;

namespace AIEmbodiment
{
    /// <summary>
    /// Discriminates the type of content in a <see cref="SyncPacket"/>.
    /// </summary>
    public enum SyncPacketType
    {
        /// <summary>Contains subtitle text and/or PCM audio data.</summary>
        TextAudio,

        /// <summary>Contains a function call event from the AI.</summary>
        FunctionCall
    }

    /// <summary>
    /// Unified packet correlating text, audio, and events from an AI response turn.
    /// Subscribe to <c>PersonaSession.OnSyncPacket</c> to receive these.
    /// Each packet carries a turn ID for grouping and a sequence number for ordering.
    /// </summary>
    public readonly struct SyncPacket
    {
        /// <summary>Packet type discriminator.</summary>
        public SyncPacketType Type { get; }

        /// <summary>Turn identifier for grouping packets within one AI response.</summary>
        public int TurnId { get; }

        /// <summary>Sequence number within the turn (0-based, ascending).</summary>
        public int Sequence { get; }

        /// <summary>
        /// Subtitle text for this packet segment. Empty for FunctionCall packets.
        /// Derived from OutputTranscription (synchronized with audio).
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// PCM audio data at 24kHz mono. Null for FunctionCall packets.
        /// Developer routes this to <c>AudioPlayback.EnqueueAudio</c>.
        /// </summary>
        public float[] Audio { get; }

        /// <summary>Function call name. Empty for TextAudio packets.</summary>
        public string FunctionName { get; }

        /// <summary>Function call arguments. Null for TextAudio packets.</summary>
        public IReadOnlyDictionary<string, object> FunctionArgs { get; }

        /// <summary>Function call ID for response correlation. Null for TextAudio packets.</summary>
        public string FunctionId { get; }

        /// <summary>True if this is the last packet in the turn.</summary>
        public bool IsTurnEnd { get; }

        /// <summary>
        /// Creates a new SyncPacket with the specified field values.
        /// </summary>
        /// <param name="type">Packet type discriminator.</param>
        /// <param name="turnId">Turn identifier for grouping.</param>
        /// <param name="sequence">Sequence number within the turn.</param>
        /// <param name="text">Subtitle text (empty string for FunctionCall).</param>
        /// <param name="audio">PCM audio at 24kHz mono (null for FunctionCall).</param>
        /// <param name="functionName">Function name (empty string for TextAudio).</param>
        /// <param name="functionArgs">Function arguments (null for TextAudio).</param>
        /// <param name="functionId">Function call ID (null for TextAudio).</param>
        /// <param name="isTurnEnd">True if this is the last packet in the turn.</param>
        public SyncPacket(
            SyncPacketType type,
            int turnId,
            int sequence,
            string text,
            float[] audio,
            string functionName,
            IReadOnlyDictionary<string, object> functionArgs,
            string functionId,
            bool isTurnEnd)
        {
            Type = type;
            TurnId = turnId;
            Sequence = sequence;
            Text = text;
            Audio = audio;
            FunctionName = functionName;
            FunctionArgs = functionArgs;
            FunctionId = functionId;
            IsTurnEnd = isTurnEnd;
        }
    }
}
