using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using AIEmbodiment;

namespace AIEmbodiment.Samples
{
    public enum ConversationState
    {
        Connecting,
        Idle,
        Recording,
        Reviewing,
        Playing
    }

    /// <summary>
    /// Controller for Conversation Demo with Chat UI and confirmation timer.
    /// Owns all audio buffering: AI audio is buffered during Recording/Reviewing,
    /// played when the user confirms (or auto-send timer expires), and discarded on cancel.
    /// </summary>
    public class ConversationController : MonoBehaviour
    {
        [SerializeField] private PersonaSession _session;
        [SerializeField] private AudioPlayback _audioPlayback;
        [SerializeField] private FaceAnimator _faceAnimator;
        [SerializeField] private ChatUI _ui;
        [SerializeField] private float _confirmationTime = 5.0f;

        private ConversationState _state = ConversationState.Connecting;
        private string _aiTranscript = "";
        private bool _turnComplete;
        private bool _playbackInitialized;
        private float _reviewTimer;
        private float _playbackGraceTimer; // Added for robustness
        private readonly List<float[]> _audioBuffer = new List<float[]>();
        private readonly StringBuilder _inputTranscriptBuilder = new StringBuilder();
        private readonly StringBuilder _outputTranscriptBuilder = new StringBuilder();

        private void Start()
        {
            // --- Sample Goals ---
            // Set goals BEFORE Connect() -- they are baked into the system instruction.
            _session.AddGoal(
                "learn_name",
                "Find out the user's first name naturally during conversation.",
                GoalPriority.High);
            // Ensure AudioPlayback is wired up (robustness for existing scenes)
            if (_audioPlayback == null)
            {
                _audioPlayback = GetComponent<AudioPlayback>();
                if (_audioPlayback == null)
                    _audioPlayback = FindFirstObjectByType<AudioPlayback>();
                
                if (_audioPlayback == null)
                    Debug.LogError("ConversationController: AudioPlayback component missing! No audio will play.");
                if (_audioPlayback == null)
                    Debug.LogError("ConversationController: AudioPlayback component missing! No audio will play.");
            }

            if (_faceAnimator == null)
            {
                Debug.LogWarning("ConversationController: FaceAnimator is not assigned! Face will not animate.");
            }

            // Register conversational goals
            _session.AddGoal(
                "learn_interest",
                "Ask what the user is building or working on.",
                GoalPriority.Medium);

            // Use prompt-based function calling -- the native audio model (gemini-2.5-flash)
            // rejects native tool declarations with PolicyViolation. In prompt mode the AI
            // outputs [CALL: complete_goal {...}] tags in its transcription, parsed client-side.
            _session.UseNativeFunctionCalling = false;

            // Register function so the AI can signal when a goal is achieved.
            _session.RegisterFunction(
                "complete_goal",
                new FunctionDeclaration("complete_goal", "Call this when you have successfully achieved one of your conversational goals.")
                    .AddString("goal_id", "The ID of the goal that was achieved.")
                    .AddString("summary", "A brief one-sentence summary of what was learned or accomplished."),
                ctx =>
                {
                    var goalId = ctx.GetString("goal_id", "unknown");
                    var summary = ctx.GetString("summary", "Goal achieved!");
                    _session.RemoveGoal(goalId);
                    _ui.ShowToast($"\u2713 Goal: {summary}");
                    _ui.LogSystemMessage($"[Goal Achieved: {goalId}] {summary}");
                    return null;
                });

            _session.OnStateChanged += HandleStateChanged;
            _session.OnInputTranscription += HandleInputTranscription;
            _session.OnOutputTranscription += HandleOutputTranscription;
            _session.OnSyncPacket += HandleSyncPacket;
            _session.OnTurnComplete += HandleTurnComplete;
            _session.OnAudioReceived += HandleAudioReceived;
            _session.OnError += HandleError;

            _session.Connect();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            switch (_state)
            {
                case ConversationState.Idle:
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                    {
                        Debug.Log("[ConversationDemo] Space pressed - entering recording.");
                        EnterRecording();
                    }
                    break;

                case ConversationState.Recording:
                    if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                    {
                        Debug.Log("[ConversationDemo] Space released - entering reviewing.");
                        EnterReviewing();
                    }
                    break;

                case ConversationState.Reviewing:
                    _reviewTimer -= Time.deltaTime;
                    _ui.SetStatus($"Auto-sending in {Mathf.Max(0, _reviewTimer):F1}s  |  [ENTER] Send Now  |  [ESC] Cancel");

                    if (Keyboard.current.enterKey.wasPressedThisFrame || _reviewTimer <= 0)
                        EnterPlaying();
                    else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                        DiscardAndReturnToIdle();
                    break;

                case ConversationState.Playing:
                    // Return to Idle once turn is complete and audio has finished playing
                    // Use a grace period to avoid stopping immediately on small buffer underruns
                    if (_turnComplete)
                    {
                        if (_audioPlayback == null || !_audioPlayback.IsPlaying)
                        {
                            _playbackGraceTimer += Time.deltaTime;
                            if (_playbackGraceTimer > 0.5f) // Wait 0.5s of silence before stopping
                            {
                                Debug.Log("[ConversationDemo] Playback finished (Grace period complete). Entering Idle.");
                                EnterIdle();
                            }
                        }
                        else
                        {
                            _playbackGraceTimer = 0f; // Reset timer if still playing
                        }
                    }
                    break;
            }
        }

        private void EnterRecording()
        {
            _state = ConversationState.Recording;
            _aiTranscript = "";
            _turnComplete = false;
            _audioBuffer.Clear();
            _audioPlayback?.ClearBuffer();
            _inputTranscriptBuilder.Clear();
            _outputTranscriptBuilder.Clear();

            _session.StartListening();

            _ui.SetState(ChatUI.State.Recording);
            _ui.SetStatus("Recording... [SPACE] Release to Review");
        }

        private void EnterReviewing()
        {
            _state = ConversationState.Reviewing;
            _session.StopListening();

            _reviewTimer = _confirmationTime;
            _ui.SetState(ChatUI.State.Reviewing);
        }

        private void EnterPlaying()
        {
            Debug.Log("[ConversationDemo] EnterPlaying - AI Speaking.");
            _state = ConversationState.Playing;
            _playbackGraceTimer = 0f;
            EnsurePlaybackInitialized();
            
            _ui.FinalizeUserMessage();
            _ui.SetState(ChatUI.State.Playing);
            _ui.SetStatus("AI Responding...");

            // Flush buffered audio to playback
            if (_audioPlayback != null)
            {
                foreach (var chunk in _audioBuffer)
                {
                    _audioPlayback.EnqueueAudio(chunk);
                    if (_faceAnimator != null)
                    {
                        _faceAnimator.ProcessAudio(chunk);
                    }
                }
            }
            _audioBuffer.Clear();

            if (!string.IsNullOrEmpty(_aiTranscript))
                _ui.SetAITranscript(_aiTranscript);
        }

        private void EnterIdle()
        {
            // Ensure animation stops and resets
            if (_faceAnimator != null)
            {
                _faceAnimator.Cancel();
            }

            _state = ConversationState.Idle;
            _ui.FinalizeAIMessage();
            _ui.SetState(ChatUI.State.Idle);
            _ui.SetStatus("Ready");
        }

        private void DiscardAndReturnToIdle()
        {
            // Discard all buffered audio -- user cancelled the turn
            _audioBuffer.Clear();
            _audioPlayback?.ClearBuffer();
            _aiTranscript = "";
            _turnComplete = false;
            _inputTranscriptBuilder.Clear();
            _outputTranscriptBuilder.Clear();

            _ui.DiscardDraft();
            Debug.Log("[ConversationDemo] User Cancelled (Escape or Discard). Returning to Idle.");
            _ui.LogSystemMessage("[Cancelled]");

            _state = ConversationState.Idle;
            _ui.SetState(ChatUI.State.Idle);
            _ui.SetStatus("Ready");
        }

        private void EnsurePlaybackInitialized()
        {
            if (_playbackInitialized || _audioPlayback == null) return;
            _audioPlayback.Initialize();
            _playbackInitialized = true;
        }

        // ── Event Handlers ──────────────────────────────────────────────────────

        /// <summary>
        /// Receives raw audio chunks from PersonaSession.
        /// Buffers during Recording/Reviewing; plays directly during Playing.
        /// </summary>
        private void HandleAudioReceived(float[] samples)
        {
            // Debug.Log($"[ConversationDemo] Audio Received. Bytes: {samples.Length}");
            if (_state == ConversationState.Recording || _state == ConversationState.Reviewing)
            {
                // Buffer -- not ready to play yet
                _audioBuffer.Add(samples);
            }
            else if (_state == ConversationState.Playing)
            {
                // Already confirmed -- play immediately as chunks arrive
                EnsurePlaybackInitialized();
                _audioPlayback.EnqueueAudio(samples);
                if (_faceAnimator != null) _faceAnimator.ProcessAudio(samples);
            }
            // Ignore audio in other states (Connecting, Idle)
        }

        private void HandleSyncPacket(SyncPacket packet)
        {
            if (packet.Type == SyncPacketType.FunctionCall)
                _ui.LogSystemMessage($"[Function: {packet.FunctionName}]");
        }

        private void HandleOutputTranscription(string text)
        {
            if (_state == ConversationState.Reviewing || _state == ConversationState.Playing)
            {
                _outputTranscriptBuilder.Append(text);
                _aiTranscript = _outputTranscriptBuilder.ToString();
                if (_state == ConversationState.Playing)
                    _ui.SetAITranscript(_aiTranscript);
            }
        }

        private void HandleInputTranscription(string text)
        {
            if (_state == ConversationState.Recording || _state == ConversationState.Reviewing)
            {
                _inputTranscriptBuilder.Append(text);
                _ui.SetUserDraft(_inputTranscriptBuilder.ToString());
            }
        }

        private void HandleTurnComplete()
        {
            _turnComplete = true;
        }

        private void HandleStateChanged(SessionState state)
        {
            Debug.Log($"[ConversationDemo] State Changed: {state}");
            if (state == SessionState.Connected)
            {
                _state = ConversationState.Idle;
                _ui.SetState(ChatUI.State.Idle);
                _ui.SetStatus("Ready");
                _ui.LogSystemMessage("[Connected]");
            }
            else if (state == SessionState.Error)
            {
                _ui.SetStatus("Connection error.");
                _ui.LogSystemMessage("[Error Occurred]");
                Debug.LogError("[ConversationDemo] Session entered Error state.");
            }
            else if (state == SessionState.Disconnected)
            {
                _ui.SetStatus("Disconnected");
                _ui.LogSystemMessage("[Disconnected]");
                Debug.Log("[ConversationDemo] Session Disconnected.");
            }
            else
            {
                _ui.SetStatus(state.ToString());
            }
        }

        private void HandleError(Exception e)
        {
            Debug.LogError($"[ConversationDemo] Session Error: {e.Message}\n{e.StackTrace}");
            _ui.LogSystemMessage($"[Error] {e.Message}");
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnStateChanged -= HandleStateChanged;
                _session.OnInputTranscription -= HandleInputTranscription;
                _session.OnOutputTranscription -= HandleOutputTranscription;
                _session.OnSyncPacket -= HandleSyncPacket;
                _session.OnTurnComplete -= HandleTurnComplete;
                _session.OnAudioReceived -= HandleAudioReceived;
                _session.OnError -= HandleError;
            }
        }
    }
}
