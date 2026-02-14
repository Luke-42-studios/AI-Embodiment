using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace AIEmbodiment
{
    /// <summary>
    /// Core MonoBehaviour managing the Gemini Live session lifecycle.
    /// Attach to a GameObject, assign a <see cref="PersonaConfig"/> in the Inspector,
    /// and call <see cref="Connect"/> to establish a bidirectional AI conversation.
    ///
    /// <para>
    /// Architecture: GeminiLiveClient uses a ConcurrentQueue drained by ProcessEvents()
    /// in Update(). All events arrive on the main thread naturally -- no thread dispatch
    /// wrapping needed for session event routing.
    /// </para>
    /// </summary>
    public class PersonaSession : MonoBehaviour
    {
        [SerializeField] private PersonaConfig _config;
        [SerializeField] private AudioCapture _audioCapture;   // optional
        [SerializeField] private AudioPlayback _audioPlayback;  // optional

        /// <summary>
        /// When true, function declarations are sent as native Gemini tool JSON in the setup handshake.
        /// When false, function instructions are injected into the system prompt and the AI outputs
        /// [CALL: name {"arg": "val"}] tags parsed from transcription.
        /// Default: true (native path). Set to false if native tool calling is unreliable with audio-only models.
        /// </summary>
        public static bool UseNativeFunctionCalling = false;

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

        private ITTSProvider _ttsProvider;
        private readonly StringBuilder _ttsTextBuffer = new StringBuilder();
        private readonly StringBuilder _functionCallBuffer = new StringBuilder();

        private static readonly Regex FunctionCallPattern =
            new Regex(@"\[CALL:\s*(\w+)\s*(\{[^}]*\})\]", RegexOptions.Compiled);

        private void SetState(SessionState newState)
        {
            if (State == newState) return;
            State = newState;
            OnStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// Converts float[-1..1] audio samples to 16-bit PCM little-endian byte array.
        /// Used by HandleAudioCaptured to convert AudioCapture output for GeminiLiveClient.SendAudio.
        /// </summary>
        private static byte[] FloatToPcm16(float[] samples)
        {
            byte[] pcm = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                pcm[i * 2] = (byte)(s & 0xFF);
                pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            return pcm;
        }

        /// <summary>
        /// Establishes a Gemini Live session using the assigned <see cref="PersonaConfig"/>.
        /// Fires <see cref="OnStateChanged"/> with Connecting. Connected state is set when
        /// GeminiLiveClient receives setupComplete from the server (via HandleGeminiEvent).
        /// </summary>
        public async void Connect()
        {
            if (State != SessionState.Disconnected)
            {
                Debug.LogWarning("PersonaSession: Connect called while not disconnected. Current state: " + State);
                return;
            }

            if (_config == null)
            {
                Debug.LogError("PersonaSession: No PersonaConfig assigned. Assign one in the Inspector.");
                return;
            }

            var settings = AIEmbodimentSettings.Instance;
            if (settings == null || string.IsNullOrEmpty(settings.ApiKey))
            {
                Debug.LogError(
                    "PersonaSession: No API key configured. " +
                    "Create an AIEmbodimentSettings asset: Assets > Create > AI Embodiment > Settings, " +
                    "place it in a Resources folder, and set the API key.");
                return;
            }

            SetState(SessionState.Connecting);

            try
            {
                _sessionCts = new CancellationTokenSource();

                string functionInstructions = null;
                if (_functionRegistry.HasRegistrations && !UseNativeFunctionCalling)
                {
                    functionInstructions = _functionRegistry.BuildPromptInstructions();
                }
                var systemInstruction = SystemInstructionBuilder.Build(_config, _goalManager, functionInstructions);

                var liveConfig = new GeminiLiveConfig
                {
                    ApiKey = settings.ApiKey,
                    Model = _config.modelName,
                    SystemInstruction = systemInstruction,
                    VoiceName = _config.geminiVoiceName
                };

                // Function calling: build tool declarations for setup handshake
                if (_functionRegistry.HasRegistrations)
                {
                    _functionRegistry.Freeze();
                    if (UseNativeFunctionCalling)
                    {
                        liveConfig.ToolsJson = _functionRegistry.BuildToolsJson();
                    }
                }

                _client = new GeminiLiveClient(liveConfig);
                _client.OnEvent += HandleGeminiEvent;

                await _client.ConnectAsync();

                // Initialize PacketAssembler
                _packetAssembler = new PacketAssembler();
                _packetAssembler.SetPacketCallback(HandleSyncPacket);

                // Initialize AudioPlayback if assigned
                if (_audioPlayback != null)
                {
                    _audioPlayback.Initialize();
                }

                // Initialize TTS provider based on voice backend
                if (_config.voiceBackend == VoiceBackend.ChirpTTS)
                {
                    _ttsProvider = new ChirpTTSClient(
                        settings.ApiKey,
                        _config.IsCustomChirpVoice ? _config.voiceCloningKey : null);
                }
                else if (_config.voiceBackend == VoiceBackend.Custom)
                {
                    _ttsProvider = _config.CustomTTSProvider;
                    if (_ttsProvider == null)
                    {
                        Debug.LogError("PersonaSession: VoiceBackend.Custom selected but no ITTSProvider assigned in PersonaConfig.");
                    }
                }
                // GeminiNative: _ttsProvider remains null

                // NOTE: SetState(Connected) is NOT called here.
                // It happens in HandleGeminiEvent when GeminiEventType.Connected arrives
                // (setupComplete acknowledged by the server).
            }
            catch (Exception ex)
            {
                SetState(SessionState.Error);
                OnError?.Invoke(ex);
                Debug.LogError($"PersonaSession: Connection failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Drains the GeminiLiveClient event queue on the main thread every frame.
        /// This is the fundamental architectural change from the old background-thread push model
        /// to GeminiLiveClient's main-thread poll model.
        /// </summary>
        private void Update()
        {
            _client?.ProcessEvents();
        }

        /// <summary>
        /// Sends a text message to the AI. The AI's response arrives via
        /// <see cref="OnTextReceived"/> and <see cref="OnTurnComplete"/> events.
        /// </summary>
        /// <param name="message">The text message to send.</param>
        public void SendText(string message)
        {
            if (_client == null || !_client.IsConnected || State != SessionState.Connected)
            {
                Debug.LogWarning("PersonaSession: Cannot send text -- session is not connected.");
                return;
            }

            if (string.IsNullOrEmpty(message)) return;

            _client.SendText(message);
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

            // Signal Gemini to flush cached audio and process the user's speech.
            // Without this, the audio stream just stops and the VAD may keep waiting.
            _client?.SendAudioStreamEnd();

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
        /// <param name="declaration">Typed function declaration describing parameters and schema.</param>
        /// <param name="handler">The delegate invoked when the AI calls this function.</param>
        public void RegisterFunction(string name, FunctionDeclaration declaration, FunctionHandler handler)
        {
            _functionRegistry.Register(name, declaration, handler);
        }

        /// <summary>
        /// Sets a custom TTS provider for the next session. Must be called before Connect().
        /// Throws if called while a session is active.
        /// </summary>
        /// <param name="provider">The ITTSProvider implementation to use, or null to use config defaults.</param>
        public void SetTTSProvider(ITTSProvider provider)
        {
            if (State != SessionState.Disconnected)
            {
                Debug.LogError("PersonaSession: SetTTSProvider must be called before Connect(). Current state: " + State);
                return;
            }
            _ttsProvider = provider;
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
        /// Converts float[] to PCM16 bytes for GeminiLiveClient.SendAudio.
        /// </summary>
        private void HandleAudioCaptured(float[] chunk)
        {
            if (_client == null || !_client.IsConnected || State != SessionState.Connected)
                return;

            // Suppress mic audio while the AI is speaking to prevent a feedback loop:
            // speaker audio → mic → Gemini → "interrupted" → buffer cleared → only last word heard.
            if (_aiSpeaking)
                return;

            // Track user speaking state
            if (!_userSpeaking)
            {
                _userSpeaking = true;
                OnUserSpeakingStarted?.Invoke();
            }

            // Convert float[] to PCM16 bytes and send
            byte[] pcmBytes = FloatToPcm16(chunk);
            _client.SendAudio(pcmBytes);
        }

        // =====================================================================
        // Event Handling (HandleGeminiEvent and sub-handlers -- Task 2)
        // =====================================================================

        /// <summary>
        /// Central event router for all GeminiLiveClient events.
        /// Called from ProcessEvents() in Update() -- already on the main thread.
        /// </summary>
        private void HandleGeminiEvent(GeminiEvent ev)
        {
            switch (ev.Type)
            {
                case GeminiEventType.Connected:
                    SetState(SessionState.Connected);
                    break;

                case GeminiEventType.Audio:
                    HandleAudioEvent(ev);
                    break;

                case GeminiEventType.OutputTranscription:
                    HandleOutputTranscription(ev.Text);
                    break;

                case GeminiEventType.InputTranscription:
                    OnInputTranscription?.Invoke(ev.Text);
                    break;

                case GeminiEventType.TurnComplete:
                    HandleTurnCompleteEvent();
                    break;

                case GeminiEventType.Interrupted:
                    HandleInterruptedEvent();
                    break;

                case GeminiEventType.FunctionCall:
                    HandleFunctionCallEvent(ev);
                    break;

                case GeminiEventType.FunctionCallCancellation:
                    _functionRegistry.MarkCancelled(ev.FunctionId);
                    break;

                case GeminiEventType.Disconnected:
                    if (State == SessionState.Connected || State == SessionState.Connecting)
                        SetState(SessionState.Disconnected);
                    break;

                case GeminiEventType.Error:
                    OnError?.Invoke(new Exception(ev.Text));
                    SetState(SessionState.Error);
                    Debug.LogError($"PersonaSession: GeminiLiveClient error: {ev.Text}");
                    break;
            }
        }

        /// <summary>
        /// Handles audio data events from GeminiLiveClient.
        /// Routes audio to AudioPlayback and PacketAssembler based on voice backend.
        /// </summary>
        private void HandleAudioEvent(GeminiEvent ev)
        {
            if (ev.AudioData == null || ev.AudioData.Length == 0) return;

            // Track turn start
            if (!_turnStarted)
            {
                _turnStarted = true;
                _packetAssembler?.StartTurn();
            }

            if (_ttsProvider == null)
            {
                // Native audio path (GeminiNative or Custom with no provider assigned)
                _audioPlayback?.EnqueueAudio(ev.AudioData);

                // Track AI speaking state
                if (!_aiSpeaking)
                {
                    _aiSpeaking = true;
                    OnAISpeakingStarted?.Invoke();
                }

                // Route to PacketAssembler for sync packet correlation
                _packetAssembler?.AddAudio(ev.AudioData);
            }
            // If _ttsProvider != null: discard Gemini audio (TTS provider handles playback)
        }

        /// <summary>
        /// Handles output transcription events from GeminiLiveClient.
        /// Fires both OnOutputTranscription and OnTextReceived, routes to PacketAssembler,
        /// and accumulates text for TTS provider if applicable.
        /// </summary>
        private void HandleOutputTranscription(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            OnOutputTranscription?.Invoke(text);
            OnTextReceived?.Invoke(text);

            // Track turn start
            if (!_turnStarted)
            {
                _turnStarted = true;
                _packetAssembler?.StartTurn();
            }

            // Route to PacketAssembler for sync packet correlation
            _packetAssembler?.AddTranscription(text);

            // TTS text accumulation (for any active TTS provider)
            if (_ttsProvider != null)
            {
                _ttsTextBuffer.Append(text);
            }

            // Prompt-based function calling: buffer text and scan for [CALL: ...] tags
            if (!UseNativeFunctionCalling && _functionRegistry.HasRegistrations)
            {
                _functionCallBuffer.Append(text);
                ParsePromptFunctionCalls();
            }
        }

        /// <summary>
        /// Scans the function call buffer for complete [CALL: name {...}] patterns
        /// and dispatches them as function calls. Handles transcription fragmentation
        /// by accumulating text and scanning the buffer after each append.
        /// </summary>
        private void ParsePromptFunctionCalls()
        {
            string buffer = _functionCallBuffer.ToString();
            var match = FunctionCallPattern.Match(buffer);

            while (match.Success)
            {
                string funcName = match.Groups[1].Value;
                string argsJson = match.Groups[2].Value;

                var args = new Dictionary<string, object>();
                try
                {
                    args = JObject.Parse(argsJson).ToObject<Dictionary<string, object>>()
                        ?? new Dictionary<string, object>();
                }
                catch
                {
                    // Garbled JSON from transcription -- use empty args
                }

                // Dispatch through PacketAssembler like native function calls
                // Prompt-based calls have no server-assigned ID, so callId is null (fire-and-forget only)
                _packetAssembler?.AddFunctionCall(funcName, args, null);

                match = match.NextMatch();
            }

            // Clear matched patterns from buffer, keep any trailing unmatched text
            var lastMatch = FunctionCallPattern.Match(buffer);
            int lastEnd = 0;
            while (lastMatch.Success)
            {
                lastEnd = lastMatch.Index + lastMatch.Length;
                lastMatch = lastMatch.NextMatch();
            }
            if (lastEnd > 0)
            {
                _functionCallBuffer.Remove(0, lastEnd);
            }

            // Prevent unbounded growth: if buffer exceeds 1000 chars with no match, trim from front
            if (_functionCallBuffer.Length > 1000)
            {
                _functionCallBuffer.Remove(0, _functionCallBuffer.Length - 500);
            }
        }

        /// <summary>
        /// Handles turn complete events from GeminiLiveClient.
        /// Stops AI speaking, fires events, and triggers TTS full-response synthesis.
        /// </summary>
        private void HandleTurnCompleteEvent()
        {
            if (_aiSpeaking)
            {
                _aiSpeaking = false;
                OnAISpeakingStopped?.Invoke();
            }

            OnTurnComplete?.Invoke();
            _turnStarted = false;
            _packetAssembler?.FinishTurn();

            // TTS full-response mode: synthesize accumulated text on turn complete
            if (_ttsProvider != null
                && _config.synthesisMode == TTSSynthesisMode.FullResponse
                && _ttsTextBuffer.Length > 0)
            {
                string fullText = _ttsTextBuffer.ToString();
                _ttsTextBuffer.Clear();
                SynthesizeAndEnqueue(fullText);
            }

            _functionCallBuffer.Clear();
        }

        /// <summary>
        /// Handles interruption events from GeminiLiveClient.
        /// Clears audio buffers, stops AI speaking, and cancels the current turn.
        /// </summary>
        private void HandleInterruptedEvent()
        {
            _audioPlayback?.ClearBuffer();

            if (_aiSpeaking)
            {
                _aiSpeaking = false;
                OnAISpeakingStopped?.Invoke();
            }

            OnInterrupted?.Invoke();
            _turnStarted = false;
            _packetAssembler?.CancelTurn();
            _ttsTextBuffer.Clear();
            _functionCallBuffer.Clear();
        }

        /// <summary>
        /// Handles function call events from GeminiLiveClient.
        /// Parses JSON arguments and routes to PacketAssembler for sync packet dispatch.
        /// </summary>
        private void HandleFunctionCallEvent(GeminiEvent ev)
        {
            var args = string.IsNullOrEmpty(ev.FunctionArgsJson)
                ? new Dictionary<string, object>()
                : JObject.Parse(ev.FunctionArgsJson).ToObject<Dictionary<string, object>>();

            _packetAssembler?.AddFunctionCall(ev.FunctionName, args, ev.FunctionId);
        }

        // =====================================================================
        // SyncPacket, Function Dispatch, TTS Synthesis
        // =====================================================================

        /// <summary>
        /// Routes SyncPackets to function dispatch when applicable, then forwards to subscribers.
        /// </summary>
        private void HandleSyncPacket(SyncPacket packet)
        {
            // TTS sentence-by-sentence synthesis: synthesize text from each SyncPacket
            if (_ttsProvider != null
                && _config.synthesisMode == TTSSynthesisMode.SentenceBySentence
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
        /// Synthesizes text via the active TTS provider and enqueues the resulting PCM audio for playback.
        /// Runs on the main thread (required by UnityWebRequest for REST-based providers).
        /// On failure: logs error, fires OnError, but conversation continues (silent skip per CONTEXT.md).
        /// </summary>
        private async void SynthesizeAndEnqueue(string text)
        {
            if (_ttsProvider == null || _audioPlayback == null || string.IsNullOrEmpty(text)) return;

            try
            {
                string voiceName = _config.voiceBackend == VoiceBackend.ChirpTTS
                    ? (_config.IsCustomChirpVoice ? _config.customVoiceName : _config.chirpVoiceShortName)
                    : _config.customVoiceName;
                string languageCode = _config.chirpLanguageCode;

                if (!_aiSpeaking)
                {
                    _aiSpeaking = true;
                    OnAISpeakingStarted?.Invoke();
                }

                TTSResult result = await _ttsProvider.SynthesizeAsync(text, voiceName, languageCode);

                if (result.HasAudio && _audioPlayback != null)
                {
                    if (result.SampleRate != 24000)
                    {
                        Debug.LogWarning(
                            $"PersonaSession: TTS provider returned {result.SampleRate}Hz audio. " +
                            "AudioPlayback expects 24000Hz. Audio may play at wrong speed.");
                    }
                    _audioPlayback.EnqueueAudio(result.Samples);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.LogWarning($"PersonaSession: TTS synthesis failed (text still displayed): {ex.Message}");
            }
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
                SendFunctionResponse(packet.FunctionName, result, packet.FunctionId);
            }
        }

        /// <summary>
        /// Sends a function response back to Gemini via the live session.
        /// </summary>
        private void SendFunctionResponse(string name, IDictionary<string, object> result, string callId)
        {
            if (_client == null || !_client.IsConnected) return;
            if (string.IsNullOrEmpty(callId))
            {
                Debug.LogWarning($"PersonaSession: Cannot send function response for '{name}' -- no call ID.");
                return;
            }
            _client.SendToolResponse(callId, name, result);
        }

        /// <summary>
        /// Sends an updated system instruction (persona + goals) to the live session.
        /// </summary>
        private void SendGoalUpdate()
        {
            if (_client == null || !_client.IsConnected || State != SessionState.Connected)
                return;

            // Gemini Live API does not support mid-session system instruction updates.
            // Goals accumulate locally and will be applied at next Connect().
            Debug.Log(
                "PersonaSession: Goal updated. Mid-session system instruction updates are not supported " +
                "by the Gemini Live API. Goals will take effect on next connection.");
        }

        // =====================================================================
        // Lifecycle Teardown
        // =====================================================================

        /// <summary>
        /// Cleanly disconnects the session: cancels operations, closes the WebSocket,
        /// and disposes resources. This method is synchronous -- GeminiLiveClient.Disconnect()
        /// blocks up to 2 seconds for the close handshake.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (State == SessionState.Disconnected || State == SessionState.Disconnecting)
                    return;

                SetState(SessionState.Disconnecting);

                _sessionCts?.Cancel();

                // Stop audio components
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

                if (_ttsProvider != null)
                {
                    _ttsProvider.Dispose();
                    _ttsProvider = null;
                }
                _ttsTextBuffer.Clear();
                _functionCallBuffer.Clear();

                if (_client != null)
                {
                    _client.OnEvent -= HandleGeminiEvent;
                    _client.Disconnect();
                    _client = null;
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
        /// the client without awaiting the close handshake.
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

            if (_ttsProvider != null)
            {
                _ttsProvider.Dispose();
                _ttsProvider = null;
            }

            _sessionCts?.Cancel();

            if (_client != null)
            {
                _client.OnEvent -= HandleGeminiEvent;
                _client.Dispose();
                _client = null;
            }

            _sessionCts?.Dispose();
            _sessionCts = null;
        }
    }
}
