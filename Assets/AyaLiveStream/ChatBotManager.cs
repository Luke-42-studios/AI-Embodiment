using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Central orchestrator for scripted chat bot activity in the livestream sample.
    /// Posts scripted messages in organic burst-and-lull patterns using randomized timing,
    /// Fisher-Yates shuffled bot selection, and per-bot personality transforms.
    ///
    /// Each burst selects 1-4 random bots, picks non-repeating messages from their
    /// combined <see cref="ChatBotConfig.scriptedMessages"/> and
    /// <see cref="ChatBotConfig.messageAlternatives"/> pools, applies personality
    /// transforms (caps, emoji), and posts to <see cref="LivestreamUI"/> with
    /// staggered delays.
    ///
    /// All posted messages are wrapped in <see cref="TrackedChatMessage"/> for
    /// downstream consumption by the narrative director (Phase 14/16).
    ///
    /// Dynamic Gemini-powered responses are NOT handled here -- see Plan 02.
    /// </summary>
    public class ChatBotManager : MonoBehaviour
    {
        [SerializeField] private LivestreamUI _livestreamUI;
        [SerializeField] private ChatBotConfig[] _bots;

        [Header("Burst Timing")]
        [SerializeField] private float _burstIntervalMin = 8f;
        [SerializeField] private float _burstIntervalMax = 18f;
        [SerializeField] private float _messageDelayMin = 0.8f;
        [SerializeField] private float _messageDelayMax = 3.0f;
        [SerializeField] private int _maxBotsPerBurst = 4;

        private readonly List<TrackedChatMessage> _trackedMessages = new();
        private readonly Dictionary<ChatBotConfig, List<int>> _usedMessageIndices = new();
        private bool _running;

        private static readonly string[] Emojis = { "\ud83d\udd25", "\u2764\ufe0f", "\u2b50", "\u2728", "\ud83d\ude4c", "\ud83d\ude0d", "\ud83d\udcaf", "\ud83c\udf89" };

        /// <summary>
        /// Read-only access to all tracked messages posted by bots.
        /// </summary>
        public IReadOnlyList<TrackedChatMessage> AllTrackedMessages => _trackedMessages;

        /// <summary>
        /// Returns tracked messages where Aya has not yet responded.
        /// Used by the narrative director (Phase 14/16) to build Aya's context.
        /// </summary>
        public IReadOnlyList<TrackedChatMessage> GetUnrespondedMessages()
        {
            return _trackedMessages.Where(m => !m.AyaHasResponded).ToList();
        }

        /// <summary>
        /// Starts the scripted burst loop. Call when the session goes live.
        /// </summary>
        public void StartBursts()
        {
            _running = true;
            _ = ScriptedBurstLoop();
        }

        /// <summary>
        /// Stops the scripted burst loop. The current burst (if any) will
        /// complete its in-progress message but no new bursts will start.
        /// </summary>
        public void StopBursts()
        {
            _running = false;
        }

        private void OnDestroy()
        {
            _running = false;
        }

        /// <summary>
        /// Periodic loop that fires scripted message bursts with organic timing.
        /// Translated from nevatars ChatBurstController coroutine to async Awaitable.
        /// </summary>
        private async Awaitable ScriptedBurstLoop()
        {
            try
            {
                while (_running)
                {
                    // Lull period between bursts
                    float lullDuration = Random.Range(_burstIntervalMin, _burstIntervalMax);
                    await Awaitable.WaitForSecondsAsync(lullDuration, destroyCancellationToken);

                    if (!_running) break;

                    // Select 1 to maxBotsPerBurst bots for this burst
                    int botCount = Random.Range(1, Mathf.Min(_maxBotsPerBurst, _bots.Length) + 1);
                    var shuffledBots = ShuffleCopy(_bots);

                    // Post messages with staggered delays
                    for (int i = 0; i < botCount; i++)
                    {
                        if (!_running) break;

                        ChatBotConfig bot = shuffledBots[i];
                        string message = PickMessage(bot);

                        if (message == null) continue;

                        string transformed = ApplyPersonality(bot, message);
                        var chatMsg = new ChatMessage(bot, transformed);
                        _livestreamUI.AddMessage(chatMsg);
                        _trackedMessages.Add(new TrackedChatMessage(chatMsg));

                        // Staggered delay between messages within the burst
                        float delay = Random.Range(_messageDelayMin, _messageDelayMax);
                        await Awaitable.WaitForSecondsAsync(delay, destroyCancellationToken);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                // Unity cancels Awaitables when the MonoBehaviour is destroyed.
                // This is expected and not an error.
            }
        }

        /// <summary>
        /// Picks a non-repeating message from the bot's combined scripted and
        /// alternative message pool. Tracks used indices per bot and resets
        /// when all messages have been used (full cycle before any repeat).
        /// </summary>
        private string PickMessage(ChatBotConfig bot)
        {
            // Build combined pool: scriptedMessages + messageAlternatives
            int scriptedCount = bot.scriptedMessages?.Length ?? 0;
            int altCount = bot.messageAlternatives?.Length ?? 0;
            int totalCount = scriptedCount + altCount;

            if (totalCount == 0) return null;

            // Initialize tracking for this bot if needed
            if (!_usedMessageIndices.TryGetValue(bot, out List<int> usedIndices))
            {
                usedIndices = new List<int>();
                _usedMessageIndices[bot] = usedIndices;
            }

            // Reset if all messages have been used
            if (usedIndices.Count >= totalCount)
            {
                usedIndices.Clear();
            }

            // Build list of available indices
            var available = new List<int>();
            for (int i = 0; i < totalCount; i++)
            {
                if (!usedIndices.Contains(i))
                {
                    available.Add(i);
                }
            }

            // Pick a random available index
            int selectedIndex = available[Random.Range(0, available.Count)];
            usedIndices.Add(selectedIndex);

            // Map index to the correct pool
            if (selectedIndex < scriptedCount)
            {
                return bot.scriptedMessages[selectedIndex];
            }
            else
            {
                return bot.messageAlternatives[selectedIndex - scriptedCount];
            }
        }

        /// <summary>
        /// Applies per-bot personality transforms based on ChatBotConfig behavior fields.
        /// - capsFrequency: chance to convert message to UPPERCASE
        /// - emojiFrequency: chance to append a random emoji
        /// - typingSpeed: stored for downstream use but does not affect text
        /// </summary>
        private string ApplyPersonality(ChatBotConfig bot, string message)
        {
            string result = message;

            // Caps transform
            if (Random.value < bot.capsFrequency)
            {
                result = result.ToUpper();
            }

            // Emoji transform
            if (Random.value < bot.emojiFrequency)
            {
                string emoji = Emojis[Random.Range(0, Emojis.Length)];
                result = $"{result} {emoji}";
            }

            return result;
        }

        /// <summary>
        /// Fisher-Yates shuffle on a copy of the array. Does NOT mutate the original.
        /// Translated from nevatars ChatBurstController.ShuffleList.
        /// </summary>
        private T[] ShuffleCopy<T>(T[] source)
        {
            T[] copy = (T[])source.Clone();
            for (int i = copy.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }
    }
}
