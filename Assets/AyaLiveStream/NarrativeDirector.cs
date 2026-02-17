using System;
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
        /// If a beat transition was queued because Aya was speaking, execute it now
        /// that her turn is complete (Pitfall 3: never send while Aya speaks).
        /// </summary>
        private void HandleTurnComplete()
        {
            if (_pendingBeatTransition != null)
            {
                ExecuteBeatTransition(_pendingBeatTransition);
            }
        }

        /// <summary>
        /// Checks if the user's speech transcript contains any skip keywords for
        /// the current beat. If found, sets _beatGoalMet = true for early exit
        /// and skips to the last beat.
        /// </summary>
        private void CheckSkipKeywords(string transcript)
        {
            if (_currentBeatIndex < 0 || _currentBeatIndex >= _beats.Length)
                return;

            var beat = _beats[_currentBeatIndex];
            if (beat.skipKeywords == null || beat.skipKeywords.Length == 0)
                return;

            // Don't skip if we're already on the last beat
            if (_currentBeatIndex >= _beats.Length - 1)
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

                    // Beat timer loop: advance on goal-met or time-expired
                    float elapsed = 0f;
                    while (elapsed < beat.timeBudgetSeconds && !_beatGoalMet)
                    {
                        await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
                        elapsed += 1f;
                        if (!_narrativeRunning) return;
                    }

                    OnBeatEnded?.Invoke(beat);
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

            if (!string.IsNullOrEmpty(beat.directorNote))
            {
                _session.SendText(beat.directorNote);
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
