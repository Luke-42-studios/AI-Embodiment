using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AIEmbodiment;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Discriminates the current state of the queued response UX flow.
    /// </summary>
    public enum QueuedState
    {
        Connecting,
        Idle,
        Recording,
        Reviewing,
        Playing
    }

    /// <summary>
    /// State machine controller for the queued response sample.
    /// Buffers AI audio during the Reviewing state and feeds it to AudioPlayback
    /// when the user approves with Enter. Manages push-to-talk keyboard input
    /// and five-state UX flow: Connecting, Idle, Recording, Reviewing, Playing.
    /// </summary>
    public class QueuedResponseController : MonoBehaviour
    {
        /// <summary>Session providing AI events and mic control.</summary>
        [SerializeField] private PersonaSession _session;

        /// <summary>Audio playback managed by this controller, not by PersonaSession.</summary>
        [SerializeField] private AudioPlayback _audioPlayback;

        /// <summary>UI controller for state-driven display updates.</summary>
        [SerializeField] private QueuedResponseUI _ui;

        private QueuedState _state = QueuedState.Connecting;
        private readonly List<float[]> _audioBuffer = new List<float[]>();
        private string _aiTranscript = "";
        private bool _turnComplete;
        private bool _playbackInitialized;

        private void Start()
        {
            _session.OnStateChanged += HandleStateChanged;
            _session.OnInputTranscription += HandleInputTranscription;
            _session.OnOutputTranscription += HandleOutputTranscription;
            _session.OnSyncPacket += HandleSyncPacket;
            _session.OnTurnComplete += HandleTurnComplete;

            _session.Connect();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            switch (_state)
            {
                case QueuedState.Idle:
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                        EnterRecording();
                    break;

                case QueuedState.Recording:
                    if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                        EnterReviewing();
                    break;

                case QueuedState.Reviewing:
                    if (Keyboard.current.enterKey.wasPressedThisFrame)
                        EnterPlaying();
                    else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                        DiscardAndReturnToIdle();
                    break;

                case QueuedState.Playing:
                    if (_turnComplete && !_audioPlayback.IsPlaying)
                        EnterIdle();
                    break;
            }
        }

        private void EnterRecording()
        {
            _state = QueuedState.Recording;
            _audioBuffer.Clear();
            _aiTranscript = "";
            _turnComplete = false;
            _audioPlayback?.ClearBuffer();
            _session.StartListening();
            _ui.SetState(QueuedState.Recording);
            _ui.ClearTranscripts();
        }

        private void EnterReviewing()
        {
            _state = QueuedState.Reviewing;
            _session.StopListening();
            _ui.SetState(QueuedState.Reviewing);
        }

        private void EnterPlaying()
        {
            _state = QueuedState.Playing;
            EnsurePlaybackInitialized();

            foreach (var chunk in _audioBuffer)
            {
                _audioPlayback.EnqueueAudio(chunk);
            }
            _audioBuffer.Clear();

            _ui.SetState(QueuedState.Playing);
            _ui.SetAITranscript(_aiTranscript);
        }

        private void EnterIdle()
        {
            _state = QueuedState.Idle;
            _ui.SetState(QueuedState.Idle);
        }

        private void DiscardAndReturnToIdle()
        {
            _audioBuffer.Clear();
            _aiTranscript = "";
            _turnComplete = false;
            _audioPlayback?.ClearBuffer();
            _state = QueuedState.Idle;
            _ui.SetState(QueuedState.Idle);
            _ui.ClearTranscripts();
        }

        private void EnsurePlaybackInitialized()
        {
            if (_playbackInitialized || _audioPlayback == null) return;
            _audioPlayback.Initialize();
            _playbackInitialized = true;
        }

        private void HandleSyncPacket(SyncPacket packet)
        {
            if (packet.Type != SyncPacketType.TextAudio) return;

            if (_state == QueuedState.Reviewing)
            {
                if (packet.Audio != null && packet.Audio.Length > 0)
                    _audioBuffer.Add(packet.Audio);
            }
            else if (_state == QueuedState.Playing)
            {
                if (packet.Audio != null && packet.Audio.Length > 0)
                {
                    EnsurePlaybackInitialized();
                    _audioPlayback.EnqueueAudio(packet.Audio);
                }
            }
        }

        private void HandleOutputTranscription(string text)
        {
            if (_state == QueuedState.Reviewing || _state == QueuedState.Playing)
            {
                _aiTranscript = text;
                if (_state == QueuedState.Playing)
                    _ui.SetAITranscript(text);
            }
        }

        private void HandleInputTranscription(string text)
        {
            if (_state == QueuedState.Recording || _state == QueuedState.Reviewing)
                _ui.SetUserTranscript(text);
        }

        private void HandleTurnComplete()
        {
            _turnComplete = true;
            if (_state == QueuedState.Reviewing)
                _ui.ShowReadyIndicator();
        }

        private void HandleStateChanged(SessionState state)
        {
            if (state == SessionState.Connected)
            {
                _state = QueuedState.Idle;
                _ui.SetState(QueuedState.Idle);
            }
            else if (state == SessionState.Error)
            {
                _ui.SetStatus("Connection error. Restart the scene.");
            }
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
            }
        }
    }
}
