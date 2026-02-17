using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Thin wrapper around <see cref="ChatMessage"/> that tracks whether Aya
    /// has responded to this particular message. Used by the narrative director
    /// (Phase 14/16) to prevent Aya from acknowledging the same bot message twice.
    /// </summary>
    public class TrackedChatMessage
    {
        /// <summary>
        /// The underlying chat message.
        /// </summary>
        public ChatMessage Message { get; }

        /// <summary>
        /// Whether Aya has already acknowledged/responded to this message.
        /// Set to true by the narrative director after injecting the message
        /// into Aya's context.
        /// </summary>
        public bool AyaHasResponded { get; set; }

        /// <summary>
        /// The <see cref="Time.time"/> value when this message was posted
        /// to the chat feed. Used for recency-based filtering.
        /// </summary>
        public float PostedAtTime { get; }

        /// <summary>
        /// Creates a new tracked message wrapper.
        /// </summary>
        /// <param name="message">The chat message to track.</param>
        public TrackedChatMessage(ChatMessage message)
        {
            Message = message;
            AyaHasResponded = false;
            PostedAtTime = Time.time;
        }
    }
}
