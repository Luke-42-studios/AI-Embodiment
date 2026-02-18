using System;
using System.Collections.Generic;
using AIEmbodiment;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Top-level orchestrator for the livestream experience. Replaces AyaSampleController
    /// as the root controller for the livestream scene. Holds [SerializeField] references
    /// to all subsystems, initializes them with a connection-wait gate, provides the
    /// "going live" transition, wires cross-system context (AyaTranscriptBuffer, FactTracker),
    /// runs dead air detection, and performs orderly shutdown on narrative completion.
    ///
    /// CRITICAL ANTI-PATTERNS (from 16-RESEARCH.md):
    /// - Do NOT use GetComponent or FindObjectOfType -- all references via [SerializeField]
    /// - Do NOT call _session.StartListening/StopListening -- PushToTalkController handles that
    /// - Do NOT send SendText -- NarrativeDirector handles that
    /// - Do NOT manipulate ChatBotManager._running -- use StartBursts/StopBursts only
    /// - Do NOT duplicate Update() PTT handling from AyaSampleController -- PushToTalkController owns that
    /// </summary>
    public class LivestreamController : MonoBehaviour
    {
        [Header("Core Subsystems")]
        [SerializeField] private PersonaSession _session;
        [SerializeField] private NarrativeDirector _narrativeDirector;
        [SerializeField] private ChatBotManager _chatBotManager;
        [SerializeField] private PushToTalkController _pttController;
        [SerializeField] private SceneTransitionHandler _sceneTransitionHandler;
        [SerializeField] private LivestreamUI _livestreamUI;
        [SerializeField] private AnimationConfig _animationConfig;

        [Header("Configuration")]
        [SerializeField] private float _connectionTimeout = 15f;
        [SerializeField] private float _deadAirThreshold = 10f;
        [SerializeField] private float _thinkingIndicatorDelay = 5f;

        [Header("User Priority")]
        [SerializeField] private float _userSilenceThreshold = 120f;

        // Cross-system context objects (plain C#, created in Start)
        private AyaTranscriptBuffer _ayaTranscriptBuffer;
        private FactTracker _factTracker;

        // Dead air detection state
        private float _lastOutputTime;
        private bool _running;
        private bool _thinkingShown;

        // User silence tracking
        private float _lastUserSpeechTime;

        // Event handler references for clean unsubscription (same pattern as NarrativeDirector)
        private Action<string> _onOutputTranscription;
        private Action _onTurnComplete;
        private Action _onAISpeakingStarted;
        private Action<string, Exception> _onFunctionError;
        private Action<NarrativeBeatConfig> _onBeatStarted;
        private Action _onUserSpeakingStopped;

        private void Start()
        {
            // Create cross-system context objects
            _ayaTranscriptBuffer = new AyaTranscriptBuffer(maxTurns: 5);
            _factTracker = new FactTracker();

            // Wire cross-system context to subsystems (Plan 03 integration)
            _chatBotManager?.SetContextProviders(_ayaTranscriptBuffer, _factTracker);
            _narrativeDirector?.SetFactTracker(_factTracker);

            // Subscribe to beat transitions so ChatBotManager knows which beat is active
            _onBeatStarted = beat => _chatBotManager?.SetCurrentBeat(beat);
            if (_narrativeDirector != null)
            {
                _narrativeDirector.OnBeatStarted += _onBeatStarted;
            }

            // Create event handler references for clean unsubscription
            _onOutputTranscription = HandleOutputTranscription;
            _onTurnComplete = HandleTurnComplete;
            _onAISpeakingStarted = HandleAISpeakingStarted;
            _onFunctionError = HandleFunctionError;

            // Subscribe to PersonaSession events
            if (_session != null)
            {
                _session.OnOutputTranscription += _onOutputTranscription;
                _session.OnTurnComplete += _onTurnComplete;
                _session.OnAISpeakingStarted += _onAISpeakingStarted;
                _session.OnFunctionError += _onFunctionError;
            }

            // User priority: track when user last spoke via PTT
            _lastUserSpeechTime = Time.time;
            _onUserSpeakingStopped = () => { _lastUserSpeechTime = Time.time; };
            if (_session != null)
            {
                _session.OnUserSpeakingStopped += _onUserSpeakingStopped;
            }

            // Inject user silence check into NarrativeDirector
            _narrativeDirector?.SetUserSilenceProvider(
                () => Time.time - _lastUserSpeechTime >= _userSilenceThreshold);

            // Subscribe to narrative completion for orderly shutdown
            if (_narrativeDirector != null)
            {
                _narrativeDirector.OnAllBeatsComplete += HandleNarrativeComplete;
            }

            // Register functions before Connect (required by PersonaSession)
            RegisterFunctions();

            // Kick off initialization
            _ = InitializeAndStart();
        }

        /// <summary>
        /// Registers data-driven animation function with PersonaSession.
        /// Copied from AyaSampleController.RegisterFunctions pattern.
        /// </summary>
        private void RegisterFunctions()
        {
            if (_animationConfig != null && _animationConfig.animations != null
                && _animationConfig.animations.Length > 0)
            {
                string[] animNames = _animationConfig.GetAnimationNames();
                var animDecl = new FunctionDeclaration(
                        "play_animation",
                        "Play a character animation or gesture during conversation. Use this to add expressiveness.")
                    .AddEnum("animation_name", "Name of the animation to play", animNames);
                _session.RegisterFunction("play_animation", animDecl, HandlePlayAnimation);
            }
            else
            {
                Debug.LogWarning("[LivestreamController] No AnimationConfig assigned or empty -- animation function calls disabled.");
            }
        }

        private IDictionary<string, object> HandlePlayAnimation(FunctionCallContext ctx)
        {
            string animName = ctx.GetString("animation_name", "idle");
            Debug.Log($"[Animation] play_animation triggered: {animName}");
            _livestreamUI?.ShowToast($"*{animName}*");
            return null; // fire-and-forget
        }

        // --- Event Handlers ---

        private void HandleOutputTranscription(string text)
        {
            _ayaTranscriptBuffer.AppendText(text);
            _livestreamUI?.UpdateAyaTranscript(text);
        }

        private void HandleTurnComplete()
        {
            _ayaTranscriptBuffer.CompleteTurn();
            _livestreamUI?.CompleteAyaTurn();
        }

        private void HandleAISpeakingStarted()
        {
            _lastOutputTime = Time.time;
            _livestreamUI?.ShowThinkingIndicator(false);
            _thinkingShown = false;
        }

        private void HandleFunctionError(string functionName, Exception ex)
        {
            Debug.LogError($"[LivestreamController] Function error: {functionName} -- {ex.Message}");
        }

        // --- Initialization ---

        /// <summary>
        /// Async initialization sequence: show loading state, connect to Gemini, wait for
        /// connection with timeout, show "going live" transition, then start subsystems.
        /// Uses Pattern 1 from 16-RESEARCH.md (Parallel Initialization with Readiness Gate).
        /// </summary>
        private async Awaitable InitializeAndStart()
        {
            try
            {
                _running = true;
                _livestreamUI?.SetLoadingState(true);

                // Start connection (async, returns immediately)
                _session.Connect();

                // Poll for connection with timeout
                float elapsed = 0f;
                while (_session.State != SessionState.Connected && elapsed < _connectionTimeout)
                {
                    if (_session.State == SessionState.Error)
                    {
                        Debug.LogError("[LivestreamController] Session connection failed.");
                        _livestreamUI?.SetLoadingState(false);
                        return;
                    }
                    await Awaitable.WaitForSecondsAsync(0.1f, destroyCancellationToken);
                    elapsed += 0.1f;
                }

                // Check for timeout -- graceful degradation (bots only, no narrative)
                if (_session.State != SessionState.Connected)
                {
                    Debug.LogWarning(
                        "[LivestreamController] Connection timed out after " +
                        $"{_connectionTimeout}s. Starting in degraded mode (bots only, no Aya).");
                    _livestreamUI?.SetLoadingState(false);
                    _chatBotManager?.StartBursts();
                    return;
                }

                // "Going live" transition moment
                _livestreamUI?.SetLoadingState(false);
                _livestreamUI?.ShowGoingLive();

                // Brief pause for the "GOING LIVE!" visual to register
                await Awaitable.WaitForSecondsAsync(1.5f, destroyCancellationToken);

                // Start subsystems in order
                _chatBotManager?.StartBursts();
                _narrativeDirector?.StartNarrative();

                // Initialize dead air tracking
                _lastOutputTime = Time.time;

                // Fire and forget the background dead air monitor
                _ = MonitorDeadAir();
            }
            catch (OperationCanceledException)
            {
                // Unity cancels Awaitables when the MonoBehaviour is destroyed.
                // This is expected and not an error.
            }
        }

        // --- Dead Air Detection ---

        /// <summary>
        /// Background loop that monitors for periods of silence from Aya.
        /// Shows "Aya is thinking..." indicator after thinkingIndicatorDelay seconds,
        /// and logs dead air detection after deadAirThreshold seconds. The ChatBotManager
        /// burst loop naturally fires to re-engage -- no explicit trigger needed.
        ///
        /// Dead air = Aya silence specifically. Bot messages are visual-only and
        /// do not reset the dead air timer.
        /// </summary>
        private async Awaitable MonitorDeadAir()
        {
            try
            {
                while (_running)
                {
                    await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

                    float silenceDuration = Time.time - _lastOutputTime;

                    // Show "thinking" indicator after threshold
                    if (silenceDuration >= _thinkingIndicatorDelay && !_thinkingShown)
                    {
                        _livestreamUI?.ShowThinkingIndicator(true);
                        _thinkingShown = true;
                    }

                    // Dead air threshold reached -- reset and let bots re-engage naturally
                    if (silenceDuration >= _deadAirThreshold)
                    {
                        _lastOutputTime = Time.time;
                        _thinkingShown = false;
                        _livestreamUI?.ShowThinkingIndicator(false);
                        Debug.Log("[LivestreamController] Dead air detected, bots will re-engage.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Unity cancels Awaitables when the MonoBehaviour is destroyed.
                // This is expected and not an error.
            }
        }

        // --- Shutdown ---

        /// <summary>
        /// Called when NarrativeDirector signals all beats are complete.
        /// Performs orderly shutdown: stops chat bots, hides thinking indicator.
        /// SceneTransitionHandler handles Disconnect + scene load separately
        /// (it subscribes to OnAllBeatsComplete in its own OnEnable).
        /// </summary>
        private void HandleNarrativeComplete()
        {
            _running = false;
            _chatBotManager?.StopBursts();
            _livestreamUI?.ShowThinkingIndicator(false);
        }

        private void OnDestroy()
        {
            _running = false;

            // Unsubscribe PersonaSession events (null checks per NarrativeDirector.OnDestroy pattern)
            if (_session != null)
            {
                if (_onOutputTranscription != null)
                    _session.OnOutputTranscription -= _onOutputTranscription;
                if (_onTurnComplete != null)
                    _session.OnTurnComplete -= _onTurnComplete;
                if (_onAISpeakingStarted != null)
                    _session.OnAISpeakingStarted -= _onAISpeakingStarted;
                if (_onFunctionError != null)
                    _session.OnFunctionError -= _onFunctionError;
                if (_onUserSpeakingStopped != null)
                    _session.OnUserSpeakingStopped -= _onUserSpeakingStopped;
            }

            // Unsubscribe narrative events
            if (_narrativeDirector != null)
            {
                _narrativeDirector.OnAllBeatsComplete -= HandleNarrativeComplete;
                if (_onBeatStarted != null)
                    _narrativeDirector.OnBeatStarted -= _onBeatStarted;
            }
        }
    }
}
