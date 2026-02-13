using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Core MonoBehaviour managing the Gemini Live session lifecycle.
    /// Attach to a GameObject, assign a <see cref="PersonaConfig"/> in the Inspector,
    /// and call <see cref="Connect"/> to establish a bidirectional AI conversation.
    /// </summary>
    public class PersonaSession : MonoBehaviour
    {
        [SerializeField] private PersonaConfig _config;
        [SerializeField] private AudioCapture _audioCapture;   // optional
        [SerializeField] private AudioPlayback _audioPlayback;  // optional

        /// <summary>Current session lifecycle state.</summary>
        public SessionState State { get; private set; } = SessionState.Disconnected;

        /// <summary>Read-only access to the assigned persona configuration.</summary>
        public PersonaConfig Config => _config;

        /// <summary>Fires on every state transition.</summary>
        public event Action<SessionState> OnStateChanged;

        /// <summary>Fires when a text chunk arrives from the AI.</summary>
        public event Action<string> OnTextReceived;

        /// <summary>Fires when the AI completes a response turn.</summary>
        public event Action OnTurnComplete;

        /// <summary>Fires with the user's speech transcript (populated in Phase 2).</summary>
        public event Action<string> OnInputTranscription;

        /// <summary>Fires with the AI's speech transcript.</summary>
        public event Action<string> OnOutputTranscription;

        /// <summary>Fires when the user interrupts the AI.</summary>
        public event Action OnInterrupted;

        /// <summary>Fires when the AI starts producing audio output.</summary>
        public event Action OnAISpeakingStarted;

        /// <summary>Fires when the AI finishes producing audio output (after buffer drains).</summary>
        public event Action OnAISpeakingStopped;

        /// <summary>Fires when the user starts speaking (first audio chunk sent after StartListening).</summary>
        public event Action OnUserSpeakingStarted;

        /// <summary>Fires when the user stops speaking (StopListening called).</summary>
        public event Action OnUserSpeakingStopped;

        /// <summary>Fires on errors with the exception details.</summary>
        public event Action<Exception> OnError;

        /// <summary>
        /// Fires when a function handler throws an exception.
        /// The conversation continues despite the error.
        /// </summary>
        public event Action<string, Exception> OnFunctionError;

        /// <summary>
        /// Fires with correlated text, audio, and function call data packaged into SyncPackets.
        /// Subscribe to this for synchronized subtitles, audio, and event handling.
        /// </summary>
        public event Action<SyncPacket> OnSyncPacket;

        private readonly FunctionRegistry _functionRegistry = new FunctionRegistry();
        private readonly GoalManager _goalManager = new GoalManager();

        private CancellationTokenSource _sessionCts;
        private GeminiLiveClient _client;
        private PacketAssembler _packetAssembler;
        private bool _aiSpeaking;
        private bool _turnStarted;
        private bool _userSpeaking;
        private bool _isListening;

        private ChirpTTSClient _chirpClient;
        private readonly StringBuilder _chirpTextBuffer = new StringBuilder();
        private bool _chirpSynthesizing;  // prevents overlapping synthesis requests

        private void SetState(SessionState newState)
        {
            if (State == newState) return;
            State = newState;
            OnStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// Establishes a Gemini Live session using the assigned <see cref="PersonaConfig"/>.
        /// Fires <see cref="OnStateChanged"/> with Connecting then Connected (or Error on failure).
        /// </summary>
        public async void Connect()
        {
            // TODO: Plan 08-02 will fully rewrite this method to use GeminiLiveClient
        }

        /// <summary>
        /// Sends a text message to the AI. The AI's response arrives via
        /// <see cref="OnTextReceived"/> and <see cref="OnTurnComplete"/> events.
        /// </summary>
        /// <param name="message">The text message to send.</param>
        public async void SendText(string message)
        {
            // TODO: Plan 08-02 will fully rewrite this method
        }

        /// <summary>
        /// Begins microphone capture and streams audio to Gemini Live.
        /// Requires an <see cref="AudioCapture"/> component assigned in the Inspector.
        /// No-op if already listening, not connected, or no AudioCapture assigned.
        /// </summary>
        public void StartListening()
        {
            if (_audioCapture == null)
            {
                Debug.LogWarning("PersonaSession: No AudioCapture assigned -- cannot listen.");
                return;
            }
            if (State != SessionState.Connected)
            {
                Debug.LogWarning("PersonaSession: Cannot start listening -- session is not connected.");
                return;
            }
            if (_isListening) return;

            _isListening = true;
            _audioCapture.OnAudioCaptured += HandleAudioCaptured;
            _audioCapture.StartCapture();
        }

        /// <summary>
        /// Stops microphone capture and audio streaming.
        /// No-op if not currently listening.
        /// </summary>
        public void StopListening()
        {
            if (!_isListening) return;

            _isListening = false;
            _audioCapture.StopCapture();
            _audioCapture.OnAudioCaptured -= HandleAudioCaptured;

            if (_userSpeaking)
            {
                _userSpeaking = false;
                OnUserSpeakingStopped?.Invoke();
            }
        }

        /// <summary>
        /// Registers a function that the AI can call during conversation.
        /// Must be called before <see cref="Connect"/> -- functions are fixed for the session lifetime.
        /// </summary>
        /// <param name="name">The function name used for lookup.</param>
        /// <param name="handler">The delegate invoked when the AI calls this function.</param>
        // TODO: Phase 10 -- add function declaration parameter back with WebSocket-native type
        public void RegisterFunction(string name, FunctionHandler handler)
        {
            _functionRegistry.Register(name, handler);
        }

        /// <summary>Adds a conversational goal. Triggers immediate instruction update if connected.</summary>
        /// <param name="id">Unique identifier for this goal.</param>
        /// <param name="description">Natural language description of what the AI should try to accomplish.</param>
        /// <param name="priority">Urgency level for this goal.</param>
        public void AddGoal(string id, string description, GoalPriority priority)
        {
            _goalManager.AddGoal(new ConversationalGoal(id, description, priority));
            SendGoalUpdate();
        }

        /// <summary>Removes a conversational goal by ID. Triggers immediate instruction update if connected.</summary>
        /// <param name="goalId">The identifier of the goal to remove.</param>
        /// <returns>True if the goal was found and removed; false otherwise.</returns>
        public bool RemoveGoal(string goalId)
        {
            bool removed = _goalManager.RemoveGoal(goalId);
            if (removed) SendGoalUpdate();
            return removed;
        }

        /// <summary>Changes a goal's priority. Triggers immediate instruction update if connected.</summary>
        /// <param name="goalId">The identifier of the goal to reprioritize.</param>
        /// <param name="newPriority">The new urgency level for this goal.</param>
        /// <returns>True if the goal was found and reprioritized; false otherwise.</returns>
        public bool ReprioritizeGoal(string goalId, GoalPriority newPriority)
        {
            var goal = _goalManager.GetGoal(goalId);
            if (goal == null) return false;
            goal.Priority = newPriority;
            SendGoalUpdate();
            return true;
        }

        /// <summary>
        /// Registers a sync driver that controls packet release timing.
        /// The highest-latency driver wins.
        /// </summary>
        /// <param name="driver">The sync driver to register.</param>
        public void RegisterSyncDriver(ISyncDriver driver)
        {
            if (_packetAssembler == null)
            {
                Debug.LogWarning("PersonaSession: Cannot register sync driver -- no active session.");
                return;
            }
            _packetAssembler.RegisterSyncDriver(driver);
        }

        /// <summary>
        /// Handles audio chunks from AudioCapture and forwards them to Gemini Live.
        /// Tracks user speaking state and fires corresponding events.
        /// </summary>
        private void HandleAudioCaptured(float[] chunk)
        {
            // TODO: Plan 08-02 will fully rewrite this method
        }

        /// <summary>
        /// Routes SyncPackets to function dispatch when applicable, then forwards to subscribers.
        /// </summary>
        private void HandleSyncPacket(SyncPacket packet)
        {
            // Chirp sentence-by-sentence synthesis: synthesize text from each SyncPacket
            if (_config.voiceBackend == VoiceBackend.ChirpTTS
                && _config.chirpSynthesisMode == ChirpSynthesisMode.SentenceBySentence
                && packet.Type == SyncPacketType.TextAudio
                && !string.IsNullOrEmpty(packet.Text))
            {
                SynthesizeAndEnqueue(packet.Text);
            }

            // Existing function dispatch (unchanged)
            if (packet.Type == SyncPacketType.FunctionCall && !string.IsNullOrEmpty(packet.FunctionName))
            {
                DispatchFunctionCall(packet);
            }

            // Always forward to developer subscribers (unchanged)
            OnSyncPacket?.Invoke(packet);
        }

        /// <summary>
        /// Synthesizes text via Chirp TTS and enqueues the resulting PCM audio for playback.
        /// Runs on the main thread (required by UnityWebRequest).
        /// On failure: logs error, fires OnError, but conversation continues (silent skip per CONTEXT.md).
        /// </summary>
        private async void SynthesizeAndEnqueue(string text)
        {
            if (_chirpClient == null || _audioPlayback == null || string.IsNullOrEmpty(text)) return;

            try
            {
                // Determine voice parameters from config
                string voiceCloningKey = _config.IsCustomChirpVoice ? _config.voiceCloningKey : null;
                string voiceName = _config.IsCustomChirpVoice ? _config.customVoiceName : _config.chirpVoiceShortName;

                // Fire AI speaking started on first synthesis of a turn
                if (!_aiSpeaking)
                {
                    _aiSpeaking = true;
                    OnAISpeakingStarted?.Invoke();
                }

                float[] pcm = await _chirpClient.SynthesizeAsync(
                    text,
                    voiceName,
                    _config.chirpLanguageCode,
                    voiceCloningKey
                );

                if (pcm != null && pcm.Length > 0 && _audioPlayback != null)
                {
                    _audioPlayback.EnqueueAudio(pcm);
                }
            }
            catch (Exception ex)
            {
                // Silent skip + error event per CONTEXT.md decision
                // Text still displays via OnSyncPacket, conversation continues
                OnError?.Invoke(ex);
                Debug.LogWarning($"PersonaSession: Chirp TTS synthesis failed (text still displayed): {ex.Message}");
            }
        }

        /// <summary>
        /// Handles errors from the ChirpTTSClient's OnError event.
        /// Routes to main thread for safe Unity API access.
        /// </summary>
        private void HandleChirpError(Exception ex)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                OnError?.Invoke(ex);
                Debug.LogWarning($"PersonaSession: Chirp TTS error: {ex.Message}");
            });
        }

        /// <summary>
        /// Dispatches a function call SyncPacket to its registered handler.
        /// Checks cancellation, invokes the handler, and sends the response if non-null.
        /// </summary>
        private void DispatchFunctionCall(SyncPacket packet)
        {
            // Check cancellation (Pitfall 3: race condition)
            if (packet.FunctionId != null && _functionRegistry.IsCancelled(packet.FunctionId))
            {
                return; // Call was cancelled by user interruption, skip dispatch
            }

            if (!_functionRegistry.TryGetHandler(packet.FunctionName, out var handler))
            {
                Debug.LogWarning($"PersonaSession: No handler registered for function '{packet.FunctionName}'");
                return;
            }

            IDictionary<string, object> result = null;
            try
            {
                var context = new FunctionCallContext(packet.FunctionName, packet.FunctionId, packet.FunctionArgs);
                result = handler(context);
            }
            catch (Exception ex)
            {
                OnFunctionError?.Invoke(packet.FunctionName, ex);
                Debug.LogError($"PersonaSession: Function handler '{packet.FunctionName}' threw: {ex.Message}");
                return; // Don't send response on error
            }

            // If handler returned a value, send it back to Gemini
            if (result != null && packet.FunctionId != null)
            {
                _ = SendFunctionResponseAsync(packet.FunctionName, result, packet.FunctionId);
            }
        }

        /// <summary>
        /// Sends a function response back to Gemini via the live session.
        /// </summary>
        private async Task SendFunctionResponseAsync(string name, IDictionary<string, object> result, string callId)
        {
            // TODO: Plan 08-02 will fully rewrite this method
        }

        /// <summary>
        /// Sends an updated system instruction (persona + goals) to the live session.
        /// </summary>
        private async void SendGoalUpdate()
        {
            // TODO: Plan 08-02 will fully rewrite this method
        }

        /// <summary>
        /// Cleanly disconnects the session: cancels the receive loop,
        /// awaits the WebSocket close handshake, and disposes resources.
        /// </summary>
        public async void Disconnect()
        {
            try
            {
                if (State == SessionState.Disconnected || State == SessionState.Disconnecting)
                    return;

                SetState(SessionState.Disconnecting);

                _sessionCts?.Cancel();

                // Stop audio components (CONTEXT.md: "PersonaSession auto-stops on disconnect")
                if (_isListening)
                {
                    StopListening();
                }
                if (_audioPlayback != null)
                {
                    _audioPlayback.Stop();
                }
                _aiSpeaking = false;

                _packetAssembler?.Reset();
                _packetAssembler = null;

                if (_chirpClient != null)
                {
                    _chirpClient.OnError -= HandleChirpError;
                    _chirpClient.Dispose();
                    _chirpClient = null;
                }
                _chirpTextBuffer.Clear();
                _aiSpeaking = false;

                _client?.Disconnect();
                _client?.Dispose();
                _client = null;

                _sessionCts?.Dispose();
                _sessionCts = null;

                SetState(SessionState.Disconnected);
            }
            catch (Exception)
            {
                SetState(SessionState.Disconnected);
            }
        }

        /// <summary>
        /// Synchronous safety net for scene transitions. Cancels the CTS and disposes
        /// the live session without awaiting the close handshake.
        /// </summary>
        private void OnDestroy()
        {
            if (_isListening && _audioCapture != null)
            {
                _audioCapture.StopCapture();
                _audioCapture.OnAudioCaptured -= HandleAudioCaptured;
            }
            _audioPlayback?.Stop();

            _packetAssembler?.Reset();

            _chirpClient?.Dispose();
            _chirpClient = null;

            _sessionCts?.Cancel();
            _client?.Dispose();
            _sessionCts?.Dispose();
            _client = null;
            _sessionCts = null;
        }
    }
}
