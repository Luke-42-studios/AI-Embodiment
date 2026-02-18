using System;
using System.Collections.Generic;
using System.Text;
using AIEmbodiment;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Core orchestrator for the narrative arc. Drives beat progression through a
    /// time-based lifecycle, steers Aya via SendText director notes at beat transitions,
    /// and provides coordination signals (IsAyaSpeaking, OnBeatTransition) for
    /// downstream systems (ChatBotManager pacing, PTT controller, scene execution).
    ///
    /// Beat progression: linear (1->2->3) with early-exit when <see cref="MarkGoalMet"/>
    /// is called or skip keywords are detected in user speech. Director notes fire
    /// ONLY when Aya is not speaking -- queued for next OnTurnComplete if she is.
    ///
    /// CRITICAL ANTI-PATTERNS (from 14-RESEARCH.md):
    /// - NEVER use GoalManager for mid-session steering (goals only apply at next Connect())
    /// - NEVER send SendText while Aya is speaking (triggers interruption, clears audio buffer)
    /// - NEVER block the main thread -- all timing uses Awaitable
    /// - NEVER manipulate ChatBotManager._running directly -- use events only
    /// </summary>
    public class NarrativeDirector : MonoBehaviour
    {
        [SerializeField] private PersonaSession _session;
        [SerializeField] private NarrativeBeatConfig[] _beats;
        [SerializeField] private LivestreamUI _livestreamUI;
        [SerializeField] private ChatBotManager _chatBotManager;

        // --- Public API (for other systems to consume) ---

        /// <summary>
        /// True between OnAISpeakingStarted and OnAISpeakingStopped.
        /// ChatBotManager reads this to slow burst pacing when Aya is speaking.
        /// </summary>
        public bool IsAyaSpeaking => _isAyaSpeaking;

        /// <summary>
        /// The currently active beat, or null before StartNarrative / after all beats complete.
        /// Downstream systems access CurrentBeat.urgency for pacing decisions.
        /// </summary>
        public NarrativeBeatConfig CurrentBeat { get; private set; }

        /// <summary>
        /// Index into the _beats array (-1 if not started).
        /// </summary>
        public int CurrentBeatIndex => _currentBeatIndex;

        /// <summary>Fires when a beat begins (after director note is sent).</summary>
        public event Action<NarrativeBeatConfig> OnBeatStarted;

        /// <summary>Fires when a beat completes (time expired or goal met).</summary>
        public event Action<NarrativeBeatConfig> OnBeatEnded;

        /// <summary>
        /// Fires between beats as a sync point. ChatBotManager subscribes to pause
        /// burst loop during transitions (Pitfall 7: stale-context bursts).
        /// </summary>
        public event Action OnBeatTransition;

        /// <summary>
        /// Fires when the narrative arc finishes (all beats complete).
        /// Phase 15/16 listens for reveal trigger.
        /// </summary>
        public event Action OnAllBeatsComplete;

        // --- Private state ---

        private bool _isAyaSpeaking;
        private bool _beatGoalMet;
        private int _currentBeatIndex = -1;
        private NarrativeBeatConfig _pendingBeatTransition;
        private bool _narrativeRunning;
        private bool _turnComplete;
        private int _questionsAnsweredCount;
        private FactTracker _factTracker;

        // Event handler references for clean unsubscription
        private Action _onAISpeakingStarted;
        private Action _onAISpeakingStopped;
        private Action _onTurnComplete;
        private Action<string> _onInputTranscription;

        /// <summary>
        /// Called by AyaSampleController when session connects.
        /// CRITICAL: Sets _narrativeRunning = true before subscribing to events
        /// and entering the beat loop. This flag gates the beat timer.
        /// </summary>
        public void StartNarrative()
        {
            _narrativeRunning = true;

            // Create handler references for clean unsubscription
            _onAISpeakingStarted = () =>
            {
                _isAyaSpeaking = true;
                _livestreamUI?.SetAyaSpeaking(true);
            };

            _onAISpeakingStopped = () =>
            {
                _isAyaSpeaking = false;
                _livestreamUI?.SetAyaSpeaking(false);
            };

            _onTurnComplete = HandleTurnComplete;
            _onInputTranscription = CheckSkipKeywords;

            // Subscribe to PersonaSession events
            if (_session != null)
            {
                _session.OnAISpeakingStarted += _onAISpeakingStarted;
                _session.OnAISpeakingStopped += _onAISpeakingStopped;
                _session.OnTurnComplete += _onTurnComplete;
                _session.OnInputTranscription += _onInputTranscription;
            }

            // Kick off the beat loop
            _ = RunBeatLoop();
        }

        /// <summary>
        /// External signal that the current beat's goal is met.
        /// Triggers early exit from the current beat's time loop.
        /// </summary>
        public void MarkGoalMet()
        {
            _beatGoalMet = true;
        }

        /// <summary>
        /// Injects the shared FactTracker for beat-level fact recording.
        /// Called by LivestreamController in Start().
        /// </summary>
        public void SetFactTracker(FactTracker factTracker)
        {
            _factTracker = factTracker;
        }

        /// <summary>
        /// Signals scene execution that Aya finished her current turn, and if a
        /// beat transition was queued because Aya was speaking, executes it now
        /// (Pitfall 3: never send while Aya speaks).
        /// </summary>
        private void HandleTurnComplete()
        {
            _turnComplete = true;

            if (_pendingBeatTransition != null)
            {
                ExecuteBeatTransition(_pendingBeatTransition);
            }
        }

        /// <summary>
        /// Checks user speech for topic keywords (skip-ahead to specific future beats)
        /// and skip keywords (fast-forward to the final beat). Topic keywords are checked
        /// first: if the user mentions a future beat's topic, the narrative jumps to that
        /// beat. Skip keywords remain the "fast forward to finale" mechanism.
        /// </summary>
        private void CheckSkipKeywords(string transcript)
        {
            if (_currentBeatIndex < 0 || _currentBeatIndex >= _beats.Length)
                return;

            // Don't skip if we're already on the last beat
            if (_currentBeatIndex >= _beats.Length - 1)
                return;

            // --- Topic keyword check: jump to a specific future beat ---
            // Check all future beats (exclusive of current and final) for topic keyword matches.
            // Final beat skip is handled by the existing skipKeywords mechanism below.
            for (int i = _currentBeatIndex + 1; i < _beats.Length - 1; i++)
            {
                var futureBeat = _beats[i];
                if (futureBeat.topicKeywords == null || futureBeat.topicKeywords.Length == 0)
                    continue;

                foreach (string keyword in futureBeat.topicKeywords)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    if (transcript.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Set index so the loop advances to this specific beat
                        _currentBeatIndex = i - 1;
                        _beatGoalMet = true;
                        Debug.Log($"[NarrativeDirector] Topic keyword match for beat '{futureBeat.beatId}', advancing.");
                        return;
                    }
                }
            }

            // --- Skip keyword check: fast-forward to final beat ---
            var beat = _beats[_currentBeatIndex];
            if (beat.skipKeywords == null || beat.skipKeywords.Length == 0)
                return;

            foreach (string keyword in beat.skipKeywords)
            {
                if (string.IsNullOrEmpty(keyword))
                    continue;

                if (transcript.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _beatGoalMet = true;
                    // Skip to the last beat: set index so the loop advances to final beat
                    _currentBeatIndex = _beats.Length - 2;
                    Debug.Log($"[NarrativeDirector] Skip keyword '{keyword}' detected, advancing to final beat.");
                    return;
                }
            }
        }

        /// <summary>
        /// Main beat progression loop. Iterates through _beats with time-based
        /// advancement and early-exit on goal-met. Uses async Awaitable with
        /// destroyCancellationToken (same pattern as ChatBotManager.ScriptedBurstLoop).
        /// </summary>
        private async Awaitable RunBeatLoop()
        {
            try
            {
                for (int i = 0; i < _beats.Length; i++)
                {
                    _currentBeatIndex = i;
                    _beatGoalMet = false;
                    _questionsAnsweredCount = 0;
                    var beat = _beats[i];

                    // Fire beat transition event (sync point for ChatBotManager)
                    if (i > 0)
                    {
                        OnBeatTransition?.Invoke();
                    }

                    // Wait for Aya to finish current turn before sending director note
                    // (CRITICAL: Pitfall 3 -- never send while Aya speaks)
                    if (_isAyaSpeaking)
                    {
                        _pendingBeatTransition = beat;
                        // Wait for HandleTurnComplete to call ExecuteBeatTransition
                        while (_pendingBeatTransition != null)
                        {
                            await Awaitable.WaitForSecondsAsync(0.1f, destroyCancellationToken);
                        }
                    }
                    else
                    {
                        ExecuteBeatTransition(beat);
                    }

                    // Run scenes within beat's time budget
                    float beatStartTime = Time.time;
                    await ExecuteBeatScenes(beat, beatStartTime);

                    // After scenes complete, wait for remaining beat time (if any) or goal-met
                    float remaining = beat.timeBudgetSeconds - (Time.time - beatStartTime);
                    while (remaining > 0 && !_beatGoalMet)
                    {
                        await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
                        remaining = beat.timeBudgetSeconds - (Time.time - beatStartTime);
                        if (!_narrativeRunning) return;
                    }

                    OnBeatEnded?.Invoke(beat);

                    // Record beat completion fact
                    if (_factTracker != null)
                    {
                        _factTracker.SetFact($"beat_{beat.beatId}_completed");
                    }
                }

                OnAllBeatsComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Unity cancels Awaitables when the MonoBehaviour is destroyed.
                // This is expected and not an error.
            }
        }

        /// <summary>
        /// Executes a beat transition: clears the pending queue, sets the current beat,
        /// fires OnBeatStarted, and sends the director note via SendText.
        /// </summary>
        private void ExecuteBeatTransition(NarrativeBeatConfig beat)
        {
            _pendingBeatTransition = null;
            CurrentBeat = beat;
            OnBeatStarted?.Invoke(beat);

            // Record beat-level facts for cross-system coherence
            if (_factTracker != null)
            {
                _factTracker.SetFact($"beat_{beat.beatId}_started");
                if (_currentBeatIndex == _beats.Length - 1)
                {
                    _factTracker.SetFact("approaching_reveal");
                }
            }

            if (!string.IsNullOrEmpty(beat.directorNote))
            {
                _session.SendText(beat.directorNote);
            }
        }

        // --- Scene Execution ---

        /// <summary>
        /// Iterates through the beat's scenes sequentially, stopping early if the
        /// beat time budget expires or the beat goal is met.
        /// </summary>
        private async Awaitable ExecuteBeatScenes(NarrativeBeatConfig beat, float beatStartTime)
        {
            if (beat.scenes == null) return;

            for (int i = 0; i < beat.scenes.Length; i++)
            {
                if (!_narrativeRunning || _beatGoalMet) return;

                // Check if beat time budget has expired
                if (Time.time - beatStartTime >= beat.timeBudgetSeconds) return;

                var scene = beat.scenes[i];
                await ExecuteScene(scene);
            }
        }

        /// <summary>
        /// Routes a scene to the appropriate handler based on its type, then
        /// waits for any conditional transition before advancing to the next scene.
        /// </summary>
        private async Awaitable ExecuteScene(NarrativeSceneConfig scene)
        {
            switch (scene.type)
            {
                case SceneType.AyaDialogue:
                    await ExecuteAyaDialogue(scene);
                    break;
                case SceneType.AyaChecksChat:
                    await ExecuteAyaChecksChat(scene);
                    break;
                case SceneType.AyaAction:
                    ExecuteAyaAction(scene);
                    break;
                case SceneType.ChatBurst:
                    // ChatBurst runs on the chat queue (ChatBotManager) independently
                    // No action needed here -- ChatBotManager burst loop handles this
                    break;
                case SceneType.UserChoice:
                    // UserChoice not implemented in Phase 14 per scope
                    Debug.Log($"[NarrativeDirector] UserChoice scene '{scene.sceneId}' skipped (Phase 14 scope)");
                    break;
            }

            // Handle conditional transitions
            if (scene.isConditional)
            {
                await WaitForCondition(scene);
            }
        }

        /// <summary>
        /// Sends a randomly selected dialogue alternative to Gemini via SendText.
        /// Waits for Aya to be idle before sending (Pitfall 3) and waits for her
        /// turn to complete before returning.
        /// </summary>
        private async Awaitable ExecuteAyaDialogue(NarrativeSceneConfig scene)
        {
            if (scene.dialogueAlternatives == null || scene.dialogueAlternatives.Length == 0) return;

            // Pick a random dialogue alternative
            string dialogue = scene.dialogueAlternatives[UnityEngine.Random.Range(0, scene.dialogueAlternatives.Length)];

            // Wait for Aya to finish any current speech (never send while speaking -- Pitfall 3)
            await WaitForAyaIdle();

            // Send dialogue context to Gemini via SendText
            _session.SendText(dialogue);

            // Wait for Aya to respond and complete her turn
            _turnComplete = false;
            await WaitForTurnComplete();
        }

        /// <summary>
        /// Gathers unresponded chat messages (user messages prioritized over bot messages),
        /// builds a summary, and sends it to Aya via SendText as a director note. Marks
        /// addressed messages as responded. Waits for Aya's turn to complete before returning.
        /// </summary>
        private async Awaitable ExecuteAyaChecksChat(NarrativeSceneConfig scene)
        {
            if (_chatBotManager == null) return;

            var unresponded = _chatBotManager.GetUnrespondedMessages();
            if (unresponded.Count == 0)
            {
                Debug.Log($"[NarrativeDirector] AyaChecksChat '{scene.sceneId}': no unresponded messages");
                return;
            }

            // User messages get priority over bot messages (per CONTEXT.md decision)
            var userMessages = new List<TrackedChatMessage>();
            var botMessages = new List<TrackedChatMessage>();
            foreach (var msg in unresponded)
            {
                if (msg.Message.IsFromUser) userMessages.Add(msg);
                else botMessages.Add(msg);
            }

            var toAddress = userMessages.Count > 0 ? userMessages : botMessages;
            int count = Mathf.Min(toAddress.Count, scene.maxResponsesToGenerate);

            // Build summary rather than injecting each message (Pitfall 8: context window)
            var sb = new StringBuilder();
            sb.AppendLine("[Director: Your chat audience has been active. Here are messages to respond to:]");
            for (int i = 0; i < count; i++)
            {
                var msg = toAddress[i];
                sb.AppendLine($"- {msg.Message.BotName}: \"{msg.Message.Text}\"");
                msg.AyaHasResponded = true; // Mark as addressed
            }
            sb.AppendLine("[Respond naturally to one or more of these, then continue your current topic.]");

            // Wait for Aya to finish any current speech
            await WaitForAyaIdle();

            _session.SendText(sb.ToString());

            // Wait for Aya's response to complete
            _turnComplete = false;
            await WaitForTurnComplete();

            _questionsAnsweredCount += count;
        }

        /// <summary>
        /// Phase 15 placeholder for action execution. Logs the action description.
        /// </summary>
        private void ExecuteAyaAction(NarrativeSceneConfig scene)
        {
            Debug.Log($"[NarrativeDirector] AyaAction '{scene.sceneId}': {scene.actionDescription} (Phase 15 placeholder)");
        }

        /// <summary>
        /// Waits for a conditional transition to be satisfied before the next scene
        /// can execute. Supports TimedOut (waits N seconds), QuestionsAnswered
        /// (waits for response count), and Always (immediate, no wait).
        /// </summary>
        private async Awaitable WaitForCondition(NarrativeSceneConfig scene)
        {
            switch (scene.conditionType)
            {
                case ConditionType.Always:
                    // Immediate -- no wait
                    break;

                case ConditionType.TimedOut:
                    float elapsed = 0f;
                    while (elapsed < scene.maxDuration && !_beatGoalMet)
                    {
                        await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
                        elapsed += 1f;
                    }
                    break;

                case ConditionType.QuestionsAnswered:
                    while (_questionsAnsweredCount < scene.requiredValue && !_beatGoalMet)
                    {
                        await Awaitable.WaitForSecondsAsync(0.5f, destroyCancellationToken);
                    }
                    break;
            }
        }

        /// <summary>
        /// Waits until Aya is not speaking. Guards all SendText calls to prevent
        /// the interruption anti-pattern (Pitfall 3).
        /// </summary>
        private async Awaitable WaitForAyaIdle()
        {
            while (_isAyaSpeaking)
            {
                await Awaitable.WaitForSecondsAsync(0.1f, destroyCancellationToken);
            }
        }

        /// <summary>
        /// Waits until Aya's current turn completes (signaled by HandleTurnComplete
        /// setting _turnComplete = true).
        /// </summary>
        private async Awaitable WaitForTurnComplete()
        {
            while (!_turnComplete)
            {
                await Awaitable.WaitForSecondsAsync(0.1f, destroyCancellationToken);
            }
        }

        private void OnDestroy()
        {
            _narrativeRunning = false;

            if (_session != null)
            {
                if (_onAISpeakingStarted != null)
                    _session.OnAISpeakingStarted -= _onAISpeakingStarted;
                if (_onAISpeakingStopped != null)
                    _session.OnAISpeakingStopped -= _onAISpeakingStopped;
                if (_onTurnComplete != null)
                    _session.OnTurnComplete -= _onTurnComplete;
                if (_onInputTranscription != null)
                    _session.OnInputTranscription -= _onInputTranscription;
            }
        }
    }
}
