using System;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Deserialization target for Gemini structured output when generating dynamic
    /// bot responses to user push-to-talk speech. The Gemini REST API returns an array
    /// of <see cref="BotReaction"/> objects, each specifying which bot should respond,
    /// what they say, and how long to wait before posting.
    ///
    /// Field names are lowercase to match the Gemini <c>responseSchema</c> property
    /// names used in <see cref="ChatBotManager.DynamicResponseSchema"/>.
    ///
    /// Deserialized via <c>JsonConvert.DeserializeObject&lt;BotReaction[]&gt;()</c>
    /// inside <see cref="GeminiTextClient.GenerateAsync{T}"/>.
    /// </summary>
    [Serializable]
    public class BotReaction
    {
        /// <summary>
        /// Exact bot name matching <see cref="ChatBotConfig.botName"/>.
        /// Looked up via <see cref="ChatBotManager.FindBotByName"/> with
        /// case-insensitive, underscore-normalized matching.
        /// </summary>
        public string botName;

        /// <summary>
        /// The bot's chat message text, personality-matched by Gemini.
        /// Typically 5-30 words in chat-length style.
        /// </summary>
        public string message;

        /// <summary>
        /// Seconds to wait before posting this reaction (0.5-3.0 range).
        /// First bot gets 0.5-1.0s, subsequent bots get 1.0-3.0s for
        /// staggered timing that feels organic.
        /// </summary>
        public float delay;
    }
}
