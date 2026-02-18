using UnityEngine;
using UnityEngine.UIElements;
using AIEmbodiment;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// UI Toolkit chat log controller for the Aya Live Stream sample.
    /// Manages the UIDocument, subscribes to PersonaSession events, and provides
    /// methods for logging messages and updating UI state.
    /// </summary>
    public class AyaChatUI : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private PersonaSession _session;

        private ScrollView _chatLog;
        private Label _nameLabel;
        private Label _statusLabel;
        private Button _pttButton;
        private VisualElement _speakingIndicator;

        private Label _currentUserMessage;
        private bool _isUserSpeaking;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _chatLog = root.Q<ScrollView>("chat-log");
            _nameLabel = root.Q<Label>("persona-name");
            _statusLabel = root.Q<Label>("status-label");
            _pttButton = root.Q<Button>("ptt-button");
            _speakingIndicator = root.Q("speaking-indicator");

            _nameLabel.text = "Aya";//_session.Config.displayName;

            _session.OnInputTranscription += HandleUserTranscription;
            _session.OnAISpeakingStarted += HandleAISpeakingStarted;
            _session.OnAISpeakingStopped += HandleAISpeakingStopped;
            _session.OnUserSpeakingStarted += HandleUserSpeakingStarted;
            _session.OnUserSpeakingStopped += HandleUserSpeakingStopped;
            _session.OnStateChanged += HandleStateChanged;
            _session.OnTurnComplete += HandleTurnComplete;

            _pttButton.RegisterCallback<PointerDownEvent>(e => _session.StartListening());
            _pttButton.RegisterCallback<PointerUpEvent>(e => _session.StopListening());
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.OnInputTranscription -= HandleUserTranscription;
                _session.OnAISpeakingStarted -= HandleAISpeakingStarted;
                _session.OnAISpeakingStopped -= HandleAISpeakingStopped;
                _session.OnUserSpeakingStarted -= HandleUserSpeakingStarted;
                _session.OnUserSpeakingStopped -= HandleUserSpeakingStopped;
                _session.OnStateChanged -= HandleStateChanged;
                _session.OnTurnComplete -= HandleTurnComplete;
            }
        }

        private void HandleUserTranscription(string text)
        {
            if (_currentUserMessage == null)
            {
                _currentUserMessage = new Label();
                _currentUserMessage.AddToClassList("msg-user");
                _chatLog.Add(_currentUserMessage);
            }
            _currentUserMessage.text = $"You: {text}";
            AutoScroll();
        }

        private void HandleTurnComplete()
        {
            AutoScroll();
        }

        private void HandleAISpeakingStarted()
        {
            SetSpeakingGlow(true);
        }

        private void HandleAISpeakingStopped()
        {
            SetSpeakingGlow(false);
        }

        private void HandleUserSpeakingStarted()
        {
            _isUserSpeaking = true;
            _currentUserMessage = null;
        }

        private void HandleUserSpeakingStopped()
        {
            _isUserSpeaking = false;
            _currentUserMessage = null;
        }

        private void HandleStateChanged(SessionState state)
        {
            switch (state)
            {
                case SessionState.Connecting:
                    _statusLabel.text = "Connecting...";
                    break;
                case SessionState.Connected:
                    _statusLabel.text = "Live! Hold SPACE to talk.";
                    break;
                case SessionState.Disconnecting:
                    _statusLabel.text = "Disconnecting...";
                    break;
                case SessionState.Disconnected:
                    _statusLabel.text = "Disconnected.";
                    break;
                case SessionState.Error:
                    _statusLabel.text = "Error occurred.";
                    break;
            }
        }

        private void SetSpeakingGlow(bool speaking)
        {
            if (speaking)
                _speakingIndicator.AddToClassList("indicator--speaking");
            else
                _speakingIndicator.RemoveFromClassList("indicator--speaking");
        }

        /// <summary>
        /// Logs a system message to the chat log (e.g., function call results, status updates).
        /// </summary>
        /// <param name="message">The message text to display.</param>
        public void LogSystemMessage(string message)
        {
            var label = new Label(message);
            label.AddToClassList("msg-system");
            _chatLog.Add(label);
            AutoScroll();
        }

        /// <summary>
        /// Sets the status label text directly.
        /// </summary>
        /// <param name="text">The status text to display.</param>
        public void SetStatus(string text)
        {
            _statusLabel.text = text;
        }

        private void AutoScroll()
        {
            _chatLog.schedule.Execute(() =>
            {
                _chatLog.scrollOffset = new Vector2(0, _chatLog.contentContainer.layout.height);
            });
        }
    }
}
