using System;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Lightweight runtime data container for a single chat message.
    /// Flows between the chat bot system and the livestream UI.
    /// Not a MonoBehaviour or ScriptableObject -- this is a plain data class.
    /// </summary>
    public class ChatMessage
    {
        public string BotName;
        public Color BotColor;
        public string Text;
        public string Timestamp;
        public bool IsFromUser;
        public ChatBotConfig Source;

        /// <summary>
        /// Creates a chat message from a bot config with the given text.
        /// </summary>
        public ChatMessage(ChatBotConfig source, string text)
        {
            BotName = source.botName;
            BotColor = source.chatColor;
            Text = text;
            Timestamp = DateTime.Now.ToString("HH:mm");
            IsFromUser = false;
            Source = source;
        }

        /// <summary>
        /// Default constructor for manual initialization.
        /// </summary>
        public ChatMessage() { }

        /// <summary>
        /// Creates a user message with a light blue color.
        /// </summary>
        public static ChatMessage FromUser(string text)
        {
            return new ChatMessage
            {
                BotName = "You",
                BotColor = new Color(0.67f, 0.86f, 1f),
                Text = text,
                Timestamp = DateTime.Now.ToString("HH:mm"),
                IsFromUser = true,
                Source = null
            };
        }
    }
}
