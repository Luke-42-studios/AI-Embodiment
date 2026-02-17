using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AIEmbodiment;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Central orchestrator for all chat bot activity in the livestream sample.
    /// Runs two independent operational modes simultaneously:
    ///
    /// 1. <b>Scripted burst loop</b> -- periodic bursts of pre-authored messages
    ///    with organic timing (8-18s lulls, 0.8-3.0s staggered delays, 1-4 random
    ///    bots per burst, Fisher-Yates shuffled selection, personality transforms).
    ///
    /// 2. <b>Dynamic Gemini responses</b> -- event-driven reactions triggered by
    ///    user push-to-talk speech via <see cref="AIEmbodiment.PersonaSession"/>
    ///    events. A single batched Gemini structured output call returns 1-3
    ///    <see cref="BotReaction"/> objects with personality-matched messages and
    ///    staggered timing.
    ///
    /// All posted messages are wrapped in <see cref="TrackedChatMessage"/> for
    /// downstream consumption by the narrative director (Phase 14/16).
    /// </summary>
    public class ChatBotManager : MonoBehaviour
    {
        [SerializeField] private LivestreamUI _livestreamUI;
        [SerializeField] private PersonaSession _session;
        [SerializeField] private ChatBotConfig[] _bots;

        [Header("Narrative Pacing")]
        [SerializeField] private NarrativeDirector _narrativeDirector;

        [Header("Burst Timing")]
        [SerializeField] private float _burstIntervalMin = 8f;
        [SerializeField] private float _burstIntervalMax = 18f;
        [SerializeField] private float _messageDelayMin = 0.8f;
        [SerializeField] private float _messageDelayMax = 3.0f;
        [SerializeField] private int _maxBotsPerBurst = 4;

        private readonly List<TrackedChatMessage> _trackedMessages = new();
        private readonly Dictionary<ChatBotConfig, List<int>> _usedMessageIndices = new();
        private bool _running;
        private bool _pausedForTransition;

        // Dynamic response state
        private GeminiTextClient _textClient;
        private string _accumulatedTranscript = "";
        private bool _dynamicResponseInFlight;
        private string _queuedTranscript;

        private static readonly string[] Emojis = { "\ud83d\udd25", "\u2764\ufe0f", "\u2b50", "\u2728", "\ud83d\ude4c", "\ud83d\ude0d", "\ud83d\udcaf", "\ud83c\udf89" };

        /// <summary>
        /// Gemini structured output schema for dynamic bot reactions.
        /// Uses UPPERCASE type constants (STRING, NUMBER, OBJECT, ARRAY) as required
        /// by the Gemini v1beta <c>responseSchema</c> field.
        /// </summary>
        private static readonly JObject DynamicResponseSchema = new JObject
        {
            ["type"] = "ARRAY",
            ["items"] = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["botName"] = new JObject { ["type"] = "STRING", ["description"] = "Exact bot name from the list provided" },
                    ["message"] = new JObject { ["type"] = "STRING", ["description"] = "The bot's chat message" },
                    ["delay"] = new JObject { ["type"] = "NUMBER", ["description"] = "Seconds to wait before posting (0.5-3.0)" }
                },
                ["required"] = new JArray("botName", "message", "delay")
            }
        };

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
        /// Starts the scripted burst loop and wires up dynamic response events.
        /// Call when the session goes live.
        /// </summary>
        public void StartBursts()
        {
            _running = true;

            // Create GeminiTextClient for dynamic responses
            _textClient = new GeminiTextClient(AIEmbodimentSettings.Instance.ApiKey);

            // Subscribe to PersonaSession events for dynamic response triggering
            if (_session != null)
            {
                _session.OnInputTranscription += HandleTranscription;
                _session.OnUserSpeakingStopped += HandleUserSpeakingStopped;
            }

            // Subscribe to narrative director beat transitions for pacing
            if (_narrativeDirector != null)
            {
                _narrativeDirector.OnBeatTransition += HandleBeatTransition;
            }

            _ = ScriptedBurstLoop();
        }

        /// <summary>
        /// Stops the scripted burst loop and unsubscribes from dynamic response events.
        /// The current burst (if any) will complete its in-progress message but no
        /// new bursts will start.
        /// </summary>
        public void StopBursts()
        {
            _running = false;
            UnsubscribeNarrativeDirector();
            UnsubscribeEvents();
            DisposeTextClient();
        }

        private void OnDestroy()
        {
            _running = false;
            UnsubscribeNarrativeDirector();
            UnsubscribeEvents();
            DisposeTextClient();
        }

        /// <summary>
        /// Unsubscribes from PersonaSession events to prevent memory leaks
        /// and errors after scene unload.
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (_session != null)
            {
                _session.OnInputTranscription -= HandleTranscription;
                _session.OnUserSpeakingStopped -= HandleUserSpeakingStopped;
            }
        }

        /// <summary>
        /// Disposes the GeminiTextClient if it exists.
        /// </summary>
        private void DisposeTextClient()
        {
            _textClient?.Dispose();
            _textClient = null;
        }

        /// <summary>
        /// Unsubscribes from NarrativeDirector beat transition events.
        /// </summary>
        private void UnsubscribeNarrativeDirector()
        {
            if (_narrativeDirector != null)
            {
                _narrativeDirector.OnBeatTransition -= HandleBeatTransition;
            }
        }

        /// <summary>
        /// Pauses the burst loop during beat transitions to prevent stale-context
        /// chat messages (Pitfall 7). Resumes after a brief delay to let the
        /// director note response settle.
        /// </summary>
        private void HandleBeatTransition()
        {
            _pausedForTransition = true;
            _ = ResumeAfterTransition();
        }

        /// <summary>
        /// Waits for the director note to trigger Aya's response and complete
        /// before resuming the burst loop.
        /// </summary>
        private async Awaitable ResumeAfterTransition()
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(5f, destroyCancellationToken);
                _pausedForTransition = false;
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Returns the burst lull duration, doubled when Aya is speaking
        /// (per CONTEXT.md: "chat bursts slow down when Aya is talking").
        /// </summary>
        private float GetBurstLullDuration()
        {
            bool ayaSpeaking = _narrativeDirector != null && _narrativeDirector.IsAyaSpeaking;
            float min = ayaSpeaking ? _burstIntervalMin * 2f : _burstIntervalMin;
            float max = ayaSpeaking ? _burstIntervalMax * 1.5f : _burstIntervalMax;
            return UnityEngine.Random.Range(min, max);
        }

        /// <summary>
        /// Returns the maximum number of bots per burst, halved when Aya is
        /// speaking to reduce chat noise during her dialogue.
        /// </summary>
        private int GetMaxBotsForBurst()
        {
            bool ayaSpeaking = _narrativeDirector != null && _narrativeDirector.IsAyaSpeaking;
            return ayaSpeaking ? Mathf.Max(1, _maxBotsPerBurst / 2) : _maxBotsPerBurst;
        }

        #region Scripted Burst Loop

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
                    // Lull period between bursts (pacing-aware: longer when Aya speaks)
                    float lullDuration = GetBurstLullDuration();
                    await Awaitable.WaitForSecondsAsync(lullDuration, destroyCancellationToken);

                    if (!_running) break;

                    // Pause during beat transitions (Pitfall 7: stale-context bursts)
                    while (_pausedForTransition)
                    {
                        await Awaitable.WaitForSecondsAsync(0.5f, destroyCancellationToken);
                    }

                    // Select 1 to maxBotsPerBurst bots (fewer when Aya speaks)
                    int botCount = UnityEngine.Random.Range(1, Mathf.Min(GetMaxBotsForBurst(), _bots.Length) + 1);
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
                        float delay = UnityEngine.Random.Range(_messageDelayMin, _messageDelayMax);
                        await Awaitable.WaitForSecondsAsync(delay, destroyCancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
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
            int selectedIndex = available[UnityEngine.Random.Range(0, available.Count)];
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
            if (UnityEngine.Random.value < bot.capsFrequency)
            {
                result = result.ToUpper();
            }

            // Emoji transform
            if (UnityEngine.Random.value < bot.emojiFrequency)
            {
                string emoji = Emojis[UnityEngine.Random.Range(0, Emojis.Length)];
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
                int j = UnityEngine.Random.Range(0, i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }

        #endregion

        #region Dynamic Gemini Responses

        /// <summary>
        /// Accumulates the user's streaming transcription during push-to-talk.
        /// <see cref="PersonaSession.OnInputTranscription"/> provides the full
        /// accumulated text each invocation (not incremental deltas), so we
        /// assign directly rather than concatenating.
        /// </summary>
        private void HandleTranscription(string text)
        {
            _accumulatedTranscript = text;
        }

        /// <summary>
        /// Triggered when the user releases push-to-talk. Captures the accumulated
        /// transcript and fires a dynamic Gemini response request. If a request is
        /// already in flight, queues the transcript for processing after the current
        /// request completes (Pitfall 6 guard).
        /// </summary>
        private void HandleUserSpeakingStopped()
        {
            string transcript = _accumulatedTranscript;
            _accumulatedTranscript = "";

            if (string.IsNullOrWhiteSpace(transcript))
                return;

            if (_dynamicResponseInFlight)
            {
                _queuedTranscript = transcript;
                return;
            }

            _ = HandleUserSpeechAsync(transcript);
        }

        /// <summary>
        /// Sends a single batched Gemini structured output call with the user's
        /// speech transcript, then posts 1-3 bot reactions with staggered timing.
        /// Guards against concurrent calls via <see cref="_dynamicResponseInFlight"/>
        /// and processes queued transcripts recursively.
        /// </summary>
        private async Awaitable HandleUserSpeechAsync(string userTranscript)
        {
            _dynamicResponseInFlight = true;

            try
            {
                string prompt = BuildDynamicPrompt(userTranscript);

                BotReaction[] reactions = await _textClient.GenerateAsync<BotReaction[]>(
                    prompt, DynamicResponseSchema);

                // GeminiTextClient returns default if disposed during request (Pitfall 2)
                if (reactions == null)
                    return;

                foreach (var reaction in reactions)
                {
                    await Awaitable.WaitForSecondsAsync(reaction.delay, destroyCancellationToken);

                    ChatBotConfig bot = FindBotByName(reaction.botName);
                    if (bot == null)
                    {
                        Debug.LogWarning($"[ChatBotManager] Dynamic response: bot '{reaction.botName}' not found, skipping.");
                        continue;
                    }

                    var chatMsg = new ChatMessage(bot, reaction.message);
                    _livestreamUI.AddMessage(chatMsg);
                    _trackedMessages.Add(new TrackedChatMessage(chatMsg));
                }
            }
            catch (OperationCanceledException)
            {
                // MonoBehaviour destroyed during async operation -- expected on scene unload.
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatBotManager] Dynamic response error: {ex.Message}");
            }
            finally
            {
                _dynamicResponseInFlight = false;
            }

            // Process queued transcript if user spoke again while we were in flight
            if (_queuedTranscript != null)
            {
                string queued = _queuedTranscript;
                _queuedTranscript = null;
                _ = HandleUserSpeechAsync(queued);
            }
        }

        /// <summary>
        /// Builds the prompt for the Gemini structured output call, including
        /// the user's speech transcript and all 6 bot personalities so Gemini
        /// can pick the most natural responders.
        /// </summary>
        private string BuildDynamicPrompt(string userTranscript)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are generating chat bot reactions for a livestream.");
            sb.AppendLine("The viewer just said via push-to-talk:");
            sb.AppendLine($"\"{userTranscript}\"");
            sb.AppendLine();
            sb.AppendLine("Available chat bots (return 1-3 reactions from the most natural responders):");

            foreach (var bot in _bots)
            {
                sb.AppendLine($"- {bot.botName}: {bot.personality}");
            }

            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Pick 1-3 bots who would naturally react to what the viewer said");
            sb.AppendLine("- Match each bot's personality and speech style exactly");
            sb.AppendLine("- Messages should be chat-length (5-30 words max)");
            sb.AppendLine("- Stagger delays: first bot 0.5-1.0s, subsequent bots 1.0-3.0s apart");
            sb.AppendLine("- Ghost404 (lurker) should be selected rarely");

            return sb.ToString();
        }

        /// <summary>
        /// Finds a bot by name with case-insensitive matching and underscore/space
        /// normalization. Handles Gemini returning slight name variations like
        /// "Dad John" instead of "Dad_John" (Pitfall 4).
        /// </summary>
        private ChatBotConfig FindBotByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            string normalized = name.Replace("_", " ").ToLowerInvariant();

            foreach (var bot in _bots)
            {
                string botNormalized = bot.botName.Replace("_", " ").ToLowerInvariant();
                if (botNormalized == normalized)
                    return bot;
            }

            return null;
        }

        #endregion
    }
}
