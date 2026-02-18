using UnityEngine;
using UnityEngine.InputSystem;
using AIEmbodiment;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Push-to-talk controller with finish-first state machine. Manages the user's
    /// primary interaction with Aya during the livestream: hold SPACE to record,
    /// review the transcript, then approve (Enter/auto-submit) or cancel (Escape).
    ///
    /// FINISH-FIRST PATTERN: When the user presses SPACE while Aya is speaking,
    /// the controller enters WaitingForAya state and shows an acknowledgment indicator.
    /// Microphone recording is deferred until OnTurnComplete fires -- this prevents
    /// audio from being sent to Gemini while Aya is speaking, which would trigger
    /// an interruption and clear her audio buffer (Pitfall 3).
    ///
    /// State machine:
    ///   Idle -> Recording        (SPACE pressed, Aya NOT speaking)
    ///   Idle -> WaitingForAya    (SPACE pressed, Aya IS speaking -- deferred recording)
    ///   WaitingForAya -> Recording (Aya turn completes -- now start recording)
    ///   WaitingForAya -> Idle    (SPACE released before Aya finishes -- cancel deferred)
    ///   Recording -> Reviewing   (SPACE released)
    ///   Reviewing -> Submitted   (Enter pressed OR 3s auto-submit)
    ///   Reviewing -> Idle        (Escape pressed -- silent discard)
    ///   Submitted -> Idle        (Aya turn complete)
    /// </summary>
    public class PushToTalkController : MonoBehaviour
    {
        [SerializeField] private PersonaSession _session;
        [SerializeField] private NarrativeDirector _narrativeDirector;
        [SerializeField] private LivestreamUI _livestreamUI;
        [SerializeField] private ChatBotManager _chatBotManager;

        private PTTState _state = PTTState.Idle;
        private string _transcript = "";
        private float _autoSubmitTimer;
        private const float AutoSubmitDelay = 3f;
        private enum PTTState { Idle, Recording, Reviewing, Submitted }

        private void Start()
        {
            if (_session != null)
            {
                _session.OnInputTranscription += HandleTranscription;
                _session.OnTurnComplete += HandleTurnComplete;
            }
        }

        /// <summary>
        /// Updates transcript text live during recording and reviewing.
        /// </summary>
        private void HandleTranscription(string text)
        {
            if (_state == PTTState.Recording || _state == PTTState.Reviewing)
            {
                _transcript = text;
                _livestreamUI.SetTranscriptText(text);
            }
        }

        /// <summary>
        /// Aya finished responding -- reset PTT to Idle.
        /// Handles TurnComplete arriving during Reviewing (before auto-submit timer)
        /// which is common when Gemini responds faster than the 3s countdown.
        /// </summary>
        private void HandleTurnComplete()
        {
            // TurnComplete arrived before auto-submit -- force submit now
            if (_state == PTTState.Reviewing)
            {
                SubmitTranscript();
            }

            if (_state == PTTState.Submitted)
            {
                _state = PTTState.Idle;
                _livestreamUI.ShowPTTAcknowledgment(false);
                _livestreamUI.ShowTranscriptOverlay(false);
                _livestreamUI.SetPTTStatus("Hold SPACE to talk", active: false);
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            switch (_state)
            {
                case PTTState.Idle:
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                        EnterRecording();
                    break;

                case PTTState.Recording:
                    if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                        EnterReviewing();
                    break;

                case PTTState.Reviewing:
                    // User pressed SPACE during review — submit current and start new recording
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                    {
                        SubmitTranscript();
                        EnterRecording();
                    }
                    else
                    {
                        UpdateReviewState();
                    }
                    break;

                case PTTState.Submitted:
                    // User pressed SPACE while Aya is responding — allow new recording
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                        EnterRecording();
                    break;
            }
        }

        /// <summary>
        /// Starts recording immediately. If Aya is speaking, Gemini handles the
        /// barge-in natively (interrupts Aya and listens to the user).
        /// </summary>
        private void EnterRecording()
        {
            _transcript = "";
            _state = PTTState.Recording;
            _session.StartListening();
            _livestreamUI.SetPTTStatus("Recording...", active: true);
        }

        /// <summary>
        /// Transitions from Recording to Reviewing: stops mic, shows transcript overlay.
        /// </summary>
        private void EnterReviewing()
        {
            _state = PTTState.Reviewing;
            _session.StopListening(); // Sends audioStreamEnd, triggers Gemini STT

            _autoSubmitTimer = AutoSubmitDelay;

            // Transcript overlay slides up over chat feed
            _livestreamUI.ShowTranscriptOverlay(true);
            _livestreamUI.SetTranscriptText(_transcript);
            _livestreamUI.UpdateAutoSubmitProgress(1f);
            _livestreamUI.SetPTTStatus("Review your message", active: false);
        }

        /// <summary>
        /// Handles auto-submit countdown, Enter to send, Escape to cancel.
        /// </summary>
        private void UpdateReviewState()
        {
            // Auto-submit countdown
            _autoSubmitTimer -= Time.deltaTime;
            _livestreamUI.UpdateAutoSubmitProgress(_autoSubmitTimer / AutoSubmitDelay);

            if (_autoSubmitTimer <= 0f)
            {
                SubmitTranscript();
                return;
            }

            // Manual submit (Enter)
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                SubmitTranscript();
                return;
            }

            // Cancel (Escape) -- silent discard, no feedback animation (per CONTEXT.md)
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelTranscript();
                return;
            }
        }

        /// <summary>
        /// Submits the transcript: posts user message to chat feed, hides overlay.
        /// Idempotent guard prevents double submission from Enter + timer race (Pitfall 6).
        /// </summary>
        private void SubmitTranscript()
        {
            // Idempotent guard (Pitfall 6: double submission from Enter + timer)
            if (_state == PTTState.Submitted) return;

            _state = PTTState.Submitted;

            // Post user message to chat feed so other viewers can see it
            if (_livestreamUI != null && !string.IsNullOrWhiteSpace(_transcript))
            {
                _livestreamUI.AddMessage(ChatMessage.FromUser(_transcript));
            }

            // Hide overlay -- the transcript has been "sent"
            _livestreamUI.ShowTranscriptOverlay(false);
            _livestreamUI.SetPTTStatus("Aya is responding...", active: false);

            // NOTE: The actual user speech audio has already been sent to Gemini during Recording.
            // StopListening triggered the STT pipeline. Gemini will respond when ready.
            // We wait in Submitted state for HandleTurnComplete to reset to Idle.
        }

        /// <summary>
        /// Cancels the transcript review: discards text, hides overlay, returns to Idle.
        /// </summary>
        private void CancelTranscript()
        {
            _state = PTTState.Idle;
            _transcript = "";
            _livestreamUI.ShowTranscriptOverlay(false);
            _livestreamUI.ShowPTTAcknowledgment(false);
            _livestreamUI.SetPTTStatus("Hold SPACE to talk", active: false);
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnInputTranscription -= HandleTranscription;
                _session.OnTurnComplete -= HandleTurnComplete;
            }
        }
    }
}
