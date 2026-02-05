using System;
using System.Collections.Generic;
using System.Text;
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
        private LiveSession _liveSession;
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

                var systemInstruction = SystemInstructionBuilder.Build(_config, _goalManager);
                var tools = _functionRegistry.HasRegistrations ? _functionRegistry.BuildTools() : null;

                var liveModel = ai.GetLiveModel(
                    modelName: _config.modelName,
                    liveGenerationConfig: liveConfig,
                    tools: tools,
                    systemInstruction: systemInstruction
                );

                _liveSession = await liveModel.ConnectAsync(_sessionCts.Token);

                SetState(SessionState.Connected);

                _packetAssembler = new PacketAssembler();
                _packetAssembler.SetPacketCallback(HandleSyncPacket);

                if (_audioPlayback != null)
                {
                    _audioPlayback.Initialize();
                }

                // Initialize Chirp TTS client when backend is ChirpTTS
                if (_config.voiceBackend == VoiceBackend.ChirpTTS)
                {
                    string apiKey = Firebase.FirebaseApp.DefaultInstance.Options.ApiKey;
                    _chirpClient = new ChirpTTSClient(apiKey);
                    _chirpClient.OnError += HandleChirpError;
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
        /// Registers a function that the AI can call during conversation.
        /// Must be called before <see cref="Connect"/> -- functions are fixed for the session lifetime.
        /// </summary>
        /// <param name="name">The function name matching the FunctionDeclaration.</param>
        /// <param name="declaration">The Firebase FunctionDeclaration describing the function schema.</param>
        /// <param name="handler">The delegate invoked when the AI calls this function.</param>
        public void RegisterFunction(string name, FunctionDeclaration declaration, FunctionHandler handler)
        {
            _functionRegistry.Register(name, declaration, handler);
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

            // If handler returned a value, send it back to Gemini (Pitfall 1: timing)
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
            try
            {
                if (_liveSession == null || State != SessionState.Connected) return;

                // Check cancellation one more time before sending (Pitfall 3)
                if (_functionRegistry.IsCancelled(callId)) return;

                var response = ModelContent.FunctionResponse(name, result, callId);
                await _liveSession.SendAsync(content: response, cancellationToken: _sessionCts.Token);
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    OnError?.Invoke(ex);
                    Debug.LogError($"PersonaSession: Function response send failed: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// Sends an updated system instruction (persona + goals) to the live session.
        /// Uses role "system" ModelContent via SendAsync with REPLACE semantics.
        /// If the wire format is rejected, the error is logged with fallback guidance
        /// (disconnect and reconnect to apply goal changes via initial system instruction).
        /// </summary>
        private async void SendGoalUpdate()
        {
            if (_liveSession == null || State != SessionState.Connected) return;

            try
            {
                var text = SystemInstructionBuilder.BuildInstructionText(_config, _goalManager);
                var content = new ModelContent("system", new ModelContent.TextPart(text));
                await _liveSession.SendAsync(content: content, turnComplete: false, cancellationToken: _sessionCts.Token);
            }
            catch (Exception ex)
            {
                // If role "system" clientContent is rejected, this will fire.
                // Fallback: developer can listen to OnError, disconnect, and reconnect.
                // A future enhancement could auto-reconnect here.
                MainThreadDispatcher.Enqueue(() =>
                {
                    OnError?.Invoke(ex);
                    Debug.LogError($"PersonaSession: Goal update failed: {ex.Message}. " +
                        "Fallback: disconnect and reconnect to apply goal changes via initial system instruction.");
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

            _packetAssembler?.Reset();

            _chirpClient?.Dispose();
            _chirpClient = null;

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
                // Route audio to playback component (VOICE-01: Gemini native audio path)
                var audioChunks = response.AudioAsFloat;
                if (audioChunks != null && audioChunks.Count > 0)
                {
                    // Only route Gemini native audio to playback when using GeminiNative backend
                    if (_config.voiceBackend == VoiceBackend.GeminiNative && _audioPlayback != null)
                    {
                        foreach (var chunk in audioChunks)
                        {
                            _audioPlayback.EnqueueAudio(chunk);
                        }
                    }
                    // NOTE: When ChirpTTS is selected, Gemini audio is intentionally discarded.
                    // Audio playback is driven by ChirpTTSClient synthesis instead.

                    // AI speaking state tracking -- keep for GeminiNative only
                    if (_config.voiceBackend == VoiceBackend.GeminiNative && !_aiSpeaking)
                    {
                        _aiSpeaking = true;
                        MainThreadDispatcher.Enqueue(() => OnAISpeakingStarted?.Invoke());
                    }

                    // Turn start detection unchanged (needed for PacketAssembler in both paths)
                    if (!_turnStarted)
                    {
                        _turnStarted = true;
                        MainThreadDispatcher.Enqueue(() => _packetAssembler?.StartTurn());
                    }

                    // Route audio to PacketAssembler ONLY for GeminiNative
                    // (Chirp path: PacketAssembler gets text from transcription, audio from Chirp synthesis)
                    if (_config.voiceBackend == VoiceBackend.GeminiNative)
                    {
                        foreach (var chunk in audioChunks)
                        {
                            var localChunk = chunk;
                            MainThreadDispatcher.Enqueue(() => _packetAssembler?.AddAudio(localChunk));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(text))
                {
                    MainThreadDispatcher.Enqueue(() => OnTextReceived?.Invoke(text));
                }

                if (content.TurnComplete)
                {
                    if (_aiSpeaking)
                    {
                        _aiSpeaking = false;
                        MainThreadDispatcher.Enqueue(() => OnAISpeakingStopped?.Invoke());
                    }
                    MainThreadDispatcher.Enqueue(() => OnTurnComplete?.Invoke());

                    _turnStarted = false;
                    MainThreadDispatcher.Enqueue(() => _packetAssembler?.FinishTurn());

                    // Chirp full-response mode: synthesize accumulated text on turn end
                    if (_config.voiceBackend == VoiceBackend.ChirpTTS
                        && _config.chirpSynthesisMode == ChirpSynthesisMode.FullResponse
                        && _chirpTextBuffer.Length > 0)
                    {
                        string fullText = _chirpTextBuffer.ToString();
                        _chirpTextBuffer.Clear();
                        MainThreadDispatcher.Enqueue(() => SynthesizeAndEnqueue(fullText));
                    }
                }

                if (content.Interrupted)
                {
                    // Clear buffered audio on barge-in (Research Pitfall 9)
                    if (_audioPlayback != null)
                    {
                        _audioPlayback.ClearBuffer();
                    }
                    if (_aiSpeaking)
                    {
                        _aiSpeaking = false;
                        MainThreadDispatcher.Enqueue(() => OnAISpeakingStopped?.Invoke());
                    }
                    MainThreadDispatcher.Enqueue(() => OnInterrupted?.Invoke());

                    _turnStarted = false;
                    MainThreadDispatcher.Enqueue(() => _packetAssembler?.CancelTurn());

                    // Clear Chirp text buffer on interruption
                    _chirpTextBuffer.Clear();
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

                    // Detect turn start on first transcription data
                    if (!_turnStarted)
                    {
                        _turnStarted = true;
                        MainThreadDispatcher.Enqueue(() => _packetAssembler?.StartTurn());
                    }

                    // Route to PacketAssembler for sentence-boundary subtitle packets
                    string transcriptForAssembler = transcript;
                    MainThreadDispatcher.Enqueue(() => _packetAssembler?.AddTranscription(transcriptForAssembler));

                    // Chirp TTS: capture text for synthesis
                    if (_config.voiceBackend == VoiceBackend.ChirpTTS)
                    {
                        _chirpTextBuffer.Append(transcript);
                    }
                }
            }
            else if (response.Message is LiveSessionToolCall toolCall)
            {
                // Route function calls to PacketAssembler for SyncPacket dispatch
                if (toolCall.FunctionCalls != null)
                {
                    foreach (var fc in toolCall.FunctionCalls)
                    {
                        var name = fc.Name;
                        var args = fc.Args;
                        var id = fc.Id;
                        MainThreadDispatcher.Enqueue(() => _packetAssembler?.AddFunctionCall(name, args, id));
                    }
                }
            }
            else if (response.Message is LiveSessionToolCallCancellation cancellation)
            {
                foreach (var id in cancellation.FunctionIds)
                {
                    var localId = id;
                    MainThreadDispatcher.Enqueue(() => _functionRegistry.MarkCancelled(localId));
                }
            }
        }
    }
}
