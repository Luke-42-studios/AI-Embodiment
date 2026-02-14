using UnityEngine;
using UnityEngine.UIElements;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// UI Toolkit controller for the queued response sample.
    /// Binds to UIDocument elements and provides state-driven display methods
    /// for status text, key hints, transcript display, and indicator styling.
    /// </summary>
    public class QueuedResponseUI : MonoBehaviour
    {
        /// <summary>UIDocument containing the QueuedResponsePanel layout.</summary>
        [SerializeField] private UIDocument _uiDocument;

        private Label _userTranscript;
        private Label _aiResponse;
        private Label _statusLabel;
        private Label _keyHints;
        private VisualElement _stateIndicator;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _userTranscript = root.Q<Label>("user-transcript");
            _aiResponse = root.Q<Label>("ai-response");
            _statusLabel = root.Q<Label>("status-label");
            _keyHints = root.Q<Label>("key-hints");
            _stateIndicator = root.Q("state-indicator");
        }

        /// <summary>
        /// Updates status text, key hints, and indicator styling for the given state.
        /// </summary>
        /// <param name="state">The current queued response state.</param>
        public void SetState(QueuedState state)
        {
            _stateIndicator.RemoveFromClassList("recording");
            _stateIndicator.RemoveFromClassList("response-ready");
            _stateIndicator.RemoveFromClassList("indicator--speaking");

            _statusLabel.text = state switch
            {
                QueuedState.Connecting => "Connecting to AI...",
                QueuedState.Idle => "Ready",
                QueuedState.Recording => "Recording... Release SPACE when done.",
                QueuedState.Reviewing => "Review your message.",
                QueuedState.Playing => "AI is responding...",
                _ => ""
            };

            _keyHints.text = state switch
            {
                QueuedState.Connecting => "",
                QueuedState.Idle => "[SPACE] Record",
                QueuedState.Recording => "[SPACE] Release to stop",
                QueuedState.Reviewing => "[ENTER] Send  |  [ESC] Cancel",
                QueuedState.Playing => "",
                _ => ""
            };

            switch (state)
            {
                case QueuedState.Recording:
                    _stateIndicator.AddToClassList("recording");
                    break;
                case QueuedState.Playing:
                    _stateIndicator.AddToClassList("indicator--speaking");
                    break;
            }
        }

        /// <summary>
        /// Sets the user transcript label text.
        /// </summary>
        /// <param name="text">The full user transcript (replacement semantics).</param>
        public void SetUserTranscript(string text)
        {
            _userTranscript.text = text;
        }

        /// <summary>
        /// Sets the AI response label text.
        /// </summary>
        /// <param name="text">The accumulated AI response transcript.</param>
        public void SetAITranscript(string text)
        {
            _aiResponse.text = text;
        }

        /// <summary>
        /// Clears both the user transcript and AI response labels.
        /// </summary>
        public void ClearTranscripts()
        {
            _userTranscript.text = "";
            _aiResponse.text = "";
        }

        /// <summary>
        /// Adds a pulsing green glow to the state indicator to signal the AI response is ready.
        /// </summary>
        public void ShowReadyIndicator()
        {
            _stateIndicator.AddToClassList("response-ready");
        }

        /// <summary>
        /// Sets the status label text directly for error messages or custom status.
        /// </summary>
        /// <param name="text">The status text to display.</param>
        public void SetStatus(string text)
        {
            _statusLabel.text = text;
        }
    }
}
