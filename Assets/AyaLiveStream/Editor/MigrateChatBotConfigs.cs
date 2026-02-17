using UnityEngine;
using UnityEditor;
using AIEmbodiment.Samples;

namespace AIEmbodiment.Samples.Editor
{
    /// <summary>
    /// One-time editor menu script that creates ChatBotConfig .asset files
    /// with hardcoded character data for the livestream sample.
    /// Run via: AI Embodiment > Samples > Migrate Chat Bot Configs
    /// </summary>
    public static class MigrateChatBotConfigs
    {
        private struct BotData
        {
            public string FileName;
            public string BotName;
            public Color ChatColor;
            public string Personality;
            public string[] SpeechTraits;
            public string[] ScriptedMessages;
            public string[] MessageAlternatives;
            public float TypingSpeed;
            public float CapsFrequency;
            public float EmojiFrequency;
        }

        [MenuItem("AI Embodiment/Samples/Migrate Chat Bot Configs")]
        public static void Migrate()
        {
            var bots = new BotData[]
            {
                // 1. Dad_John -- Supportive dad energy
                new BotData
                {
                    FileName = "Dad_JohnConfig",
                    BotName = "Dad_John",
                    ChatColor = new Color(0.4f, 0.7f, 0.4f),
                    Personality = "Supportive dad who watches his daughter's art streams. Encouraging but not tech-savvy. Uses phrases like 'that's my girl' and 'wow kiddo'. Occasionally confused by chat terminology.",
                    SpeechTraits = new[] { "short encouraging phrases", "occasional dad jokes", "no emojis" },
                    ScriptedMessages = new[]
                    {
                        "Wow that's looking great!",
                        "Keep it up kiddo!",
                        "Your mom would be so proud",
                        "How long did that take you?",
                        "That's my girl!",
                        "I don't know how you do it",
                        "Can you show the one with the dog again?"
                    },
                    MessageAlternatives = new string[0],
                    TypingSpeed = 2.0f,
                    CapsFrequency = 0.0f,
                    EmojiFrequency = 0.0f
                },

                // 2. TeenFan_Miko -- Excitable teenage fan
                new BotData
                {
                    FileName = "TeenFan_MikoConfig",
                    BotName = "TeenFan_Miko",
                    ChatColor = new Color(1f, 0.6f, 0.8f),
                    Personality = "Excitable teenage fan who idolizes Aya. Types fast with lots of caps and emojis. Gets genuinely emotional about the art. Uses internet slang naturally.",
                    SpeechTraits = new[] { "frequent caps", "lots of emojis", "internet slang", "very short messages" },
                    ScriptedMessages = new[]
                    {
                        "OMG HIIII",
                        "ur art is SO good",
                        "im literally crying rn",
                        "YESSS",
                        "wait thats so pretty",
                        "can you draw me next lol",
                        "i showed my friends and they all followed!!"
                    },
                    MessageAlternatives = new string[0],
                    TypingSpeed = 0.8f,
                    CapsFrequency = 0.4f,
                    EmojiFrequency = 0.7f
                },

                // 3. ArtStudent_Priya -- Thoughtful art student
                new BotData
                {
                    FileName = "ArtStudent_PriyaConfig",
                    BotName = "ArtStudent_Priya",
                    ChatColor = new Color(0.6f, 0.8f, 1f),
                    Personality = "Second-year art student studying digital illustration. Asks technical questions about Aya's process. Respectful but curious. Uses proper grammar.",
                    SpeechTraits = new[] { "technical art vocabulary", "asks questions", "proper grammar", "medium length messages" },
                    ScriptedMessages = new[]
                    {
                        "What brush are you using for that texture?",
                        "The color palette reminds me of Monet's water lilies",
                        "Do you sketch traditionally first or go straight digital?",
                        "I love how you handle the light source",
                        "How long have you been drawing?",
                        "That layering technique is really interesting"
                    },
                    MessageAlternatives = new string[0],
                    TypingSpeed = 1.8f,
                    CapsFrequency = 0.0f,
                    EmojiFrequency = 0.1f
                },

                // 4. Lurker_Ghost404 -- Quiet first-time viewer
                new BotData
                {
                    FileName = "Lurker_Ghost404Config",
                    BotName = "Lurker_Ghost404",
                    ChatColor = new Color(0.6f, 0.6f, 0.7f),
                    Personality = "First-time viewer who rarely chats. When they do speak, it is brief and understated. Might be socially anxious. Uses lowercase everything.",
                    SpeechTraits = new[] { "all lowercase", "very short", "no punctuation", "rare messages" },
                    ScriptedMessages = new[]
                    {
                        "hi",
                        "nice",
                        "wow",
                        "cool",
                        "first time here",
                        "how do you do that"
                    },
                    MessageAlternatives = new string[0],
                    TypingSpeed = 3.0f,
                    CapsFrequency = 0.0f,
                    EmojiFrequency = 0.0f
                },

                // 5. Regular_TechBro42 -- Long-time regular
                new BotData
                {
                    FileName = "Regular_TechBro42Config",
                    BotName = "Regular_TechBro42",
                    ChatColor = new Color(1f, 0.8f, 0.3f),
                    Personality = "Long-time viewer and self-proclaimed 'day one fan'. Works in tech, watches streams to relax. Knows Aya's lore and references old streams. Slightly gatekeep-y about being an OG fan.",
                    SpeechTraits = new[] { "references past streams", "tech metaphors", "medium messages", "some emojis" },
                    ScriptedMessages = new[]
                    {
                        "Day one fan checking in!",
                        "This reminds me of that piece you did last month",
                        "Chat you don't even know, her old stuff was insane too",
                        "Aya's improvement arc is literally exponential",
                        "New viewers, trust me, sub now",
                        "I remember when this stream had like 5 people"
                    },
                    MessageAlternatives = new string[0],
                    TypingSpeed = 1.2f,
                    CapsFrequency = 0.05f,
                    EmojiFrequency = 0.3f
                },

                // 6. Troll_xXShadowXx -- Mild troll / contrarian
                new BotData
                {
                    FileName = "Troll_xXShadowXxConfig",
                    BotName = "Troll_xXShadowXx",
                    ChatColor = new Color(0.9f, 0.3f, 0.3f),
                    Personality = "Not malicious but likes to stir things up. Asks provocative questions and plays devil's advocate. Occasionally compliments when genuinely impressed, which makes it meaningful.",
                    SpeechTraits = new[] { "provocative questions", "devil's advocate", "occasional genuine compliment", "medium messages" },
                    ScriptedMessages = new[]
                    {
                        "is this AI generated lol",
                        "I've seen better tbh",
                        "ok that's actually kinda fire",
                        "why does everyone like this",
                        "how is this different from the last one",
                        "wait no actually that's really good"
                    },
                    MessageAlternatives = new string[0],
                    TypingSpeed = 1.0f,
                    CapsFrequency = 0.1f,
                    EmojiFrequency = 0.15f
                }
            };

            string folderPath = "Assets/AyaLiveStream/ChatBotConfigs";

            // Create directory if it does not exist
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/AyaLiveStream"))
                {
                    AssetDatabase.CreateFolder("Assets", "AyaLiveStream");
                }
                AssetDatabase.CreateFolder("Assets/AyaLiveStream", "ChatBotConfigs");
            }

            int count = 0;

            foreach (var bot in bots)
            {
                var config = ScriptableObject.CreateInstance<ChatBotConfig>();
                config.botName = bot.BotName;
                config.chatColor = bot.ChatColor;
                config.personality = bot.Personality;
                config.speechTraits = bot.SpeechTraits;
                config.scriptedMessages = bot.ScriptedMessages;
                config.messageAlternatives = bot.MessageAlternatives;
                config.typingSpeed = bot.TypingSpeed;
                config.capsFrequency = bot.CapsFrequency;
                config.emojiFrequency = bot.EmojiFrequency;

                string assetPath = $"{folderPath}/{bot.FileName}.asset";
                AssetDatabase.CreateAsset(config, assetPath);
                EditorUtility.SetDirty(config);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created {count} ChatBotConfig assets in {folderPath}/");
        }
    }
}
