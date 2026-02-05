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

        /// <summary>Fires on errors with the exception details.</summary>
        public event Action<Exception> OnError;

        private CancellationTokenSource _sessionCts;
        private LiveSession _liveSession;

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
