using System;
using System.Threading;
using System.Threading.Tasks;
using Firebase.AI;
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

        private CancellationTokenSource _sessionCts;
        private LiveSession _liveSession;
        private bool _aiSpeaking;
        private bool _userSpeaking;
        private bool _isListening;

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
            try
            {
                if (State != SessionState.Disconnected)
                {
                    Debug.LogWarning("PersonaSession: Cannot connect -- session is not in Disconnected state.");
                    return;
                }

                if (_config == null)
                {
                    Debug.LogError("PersonaSession: No PersonaConfig assigned.");
                    return;
                }

                SetState(SessionState.Connecting);

                _sessionCts = new CancellationTokenSource();

                var ai = FirebaseAI.GetInstance(FirebaseAI.Backend.GoogleAI());

                var liveConfig = new LiveGenerationConfig(
                    responseModalities: new[] { ResponseModality.Audio },
                    speechConfig: SpeechConfig.UsePrebuiltVoice(_config.geminiVoiceName),
                    temperature: _config.temperature,
                    inputAudioTranscription: new AudioTranscriptionConfig(),
                    outputAudioTranscription: new AudioTranscriptionConfig()
                );

                var systemInstruction = SystemInstructionBuilder.Build(_config);

                var liveModel = ai.GetLiveModel(
                    modelName: _config.modelName,
                    liveGenerationConfig: liveConfig,
                    systemInstruction: systemInstruction
                );

                _liveSession = await liveModel.ConnectAsync(_sessionCts.Token);

                SetState(SessionState.Connected);

                if (_audioPlayback != null)
                {
                    _audioPlayback.Initialize();
                }

                // Fire and forget -- ReceiveLoopAsync handles its own error reporting
                _ = ReceiveLoopAsync(_liveSession, _sessionCts.Token);
            }
            catch (Exception ex)
            {
                SetState(SessionState.Error);
                OnError?.Invoke(ex);
                Debug.LogError($"PersonaSession: Connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a text message to the AI. The AI's response arrives via
        /// <see cref="OnTextReceived"/> and <see cref="OnTurnComplete"/> events.
        /// </summary>
        /// <param name="message">The text message to send.</param>
        public async void SendText(string message)
        {
            try
            {
                if (_liveSession == null || State != SessionState.Connected)
                {
                    Debug.LogWarning("PersonaSession: Cannot send text -- session is not connected.");
                    return;
                }

                if (string.IsNullOrEmpty(message)) return;

                await _liveSession.SendAsync(
                    content: ModelContent.Text(message),
                    turnComplete: true,
                    cancellationToken: _sessionCts.Token
                );
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    OnError?.Invoke(ex);
                    Debug.LogError($"PersonaSession: Send failed: {ex.Message}");
                });
            }
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
        /// Handles audio chunks from AudioCapture and forwards them to Gemini Live.
        /// Tracks user speaking state and fires corresponding events.
        /// </summary>
        private void HandleAudioCaptured(float[] chunk)
        {
            if (_liveSession == null || State != SessionState.Connected) return;

            if (!_userSpeaking)
            {
                _userSpeaking = true;
                OnUserSpeakingStarted?.Invoke();
            }

            // Fire-and-forget send: SDK handles float->PCM->base64->JSON->WebSocket
            _ = _liveSession.SendAudioAsync(chunk, _sessionCts.Token);
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

                if (_liveSession != null)
                {
                    try
                    {
                        // Use CancellationToken.None -- we WANT the close handshake to complete
                        await _liveSession.CloseAsync(CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        // WebSocket may already be closed (expected per Research Pitfall 7)
                    }

                    _liveSession.Dispose();
                    _liveSession = null;
                }

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

            _sessionCts?.Cancel();
            _liveSession?.Dispose();
            _sessionCts?.Dispose();
            _liveSession = null;
            _sessionCts = null;
        }

        /// <summary>
        /// Continuous receive loop that processes AI responses across multiple turns.
        /// Wraps ReceiveAsync in an outer while loop to solve the single-turn trap
        /// (Research Pitfall 1): ReceiveAsync breaks the IAsyncEnumerable at each
        /// TurnComplete, so without the outer loop the session processes exactly one
        /// response turn and silently stops receiving.
        /// Runs on a background thread pool thread for the entire session duration.
        /// </summary>
        private async Task ReceiveLoopAsync(LiveSession session, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await foreach (var response in session.ReceiveAsync(ct))
                    {
                        ProcessResponse(response);
                    }
                    // ReceiveAsync completed because TurnComplete was received.
                    // Loop back to receive the next turn.
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown -- session is intentionally closing
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    SetState(SessionState.Error);
                    OnError?.Invoke(ex);
                    Debug.LogError($"PersonaSession: Receive loop error: {ex.Message}");
                });
            }
            finally
            {
                // If the loop exits for any reason and State is still Connected,
                // transition to Disconnected so consumer code knows the session ended.
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (State == SessionState.Connected)
                        SetState(SessionState.Disconnected);
                });
            }
        }

        /// <summary>
        /// Processes a single response from the Gemini Live session.
        /// This runs on a BACKGROUND THREAD -- every callback MUST go through
        /// <see cref="MainThreadDispatcher.Enqueue"/> to safely interact with Unity.
        /// All response data is captured into local variables before lambda capture
        /// to prevent stale references.
        /// </summary>
        private void ProcessResponse(LiveSessionResponse response)
        {
            // Capture text into local variable before lambda capture
            string text = response.Text;

            if (response.Message is LiveSessionContent content)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    MainThreadDispatcher.Enqueue(() => OnTextReceived?.Invoke(text));
                }

                if (content.TurnComplete)
                {
                    MainThreadDispatcher.Enqueue(() => OnTurnComplete?.Invoke());
                }

                if (content.Interrupted)
                {
                    MainThreadDispatcher.Enqueue(() => OnInterrupted?.Invoke());
                }

                if (content.InputTranscription.HasValue)
                {
                    string transcript = content.InputTranscription.Value.Text;
                    MainThreadDispatcher.Enqueue(() => OnInputTranscription?.Invoke(transcript));
                }

                if (content.OutputTranscription.HasValue)
                {
                    string transcript = content.OutputTranscription.Value.Text;
                    MainThreadDispatcher.Enqueue(() => OnOutputTranscription?.Invoke(transcript));
                }
            }
            else if (response.Message is LiveSessionToolCall)
            {
                Debug.Log("PersonaSession: Tool call received (not implemented until Phase 4)");
            }
        }
    }
}
