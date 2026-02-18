using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Reusable UI Toolkit chat controller.
    /// Manages a scrolling chat log with support for user drafts, AI streaming,
    /// and system status updates.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Recording,
            Reviewing,
            Playing,
            Disabled
        }

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        private ScrollView _chatLog;
        private Label _statusLabel;
        private Label _keyHints;
        private VisualElement _indicator;

        private Label _currentDraftMessage;
        private Label _currentAIMessage;

        private void OnEnable()
        {
            _root = _uiDocument.rootVisualElement;
            _chatLog = _root.Q<ScrollView>("chat-log");
            _statusLabel = _root.Q<Label>("status-label");
            _keyHints = _root.Q<Label>("key-hints");
            _indicator = _root.Q("speaking-indicator");
        }

        public void SetState(State state)
        {
            // Reset indicator classes
            _indicator.RemoveFromClassList("indicator--speaking");
            _indicator.RemoveFromClassList("indicator--recording");
            _indicator.RemoveFromClassList("indicator--reviewing");

            switch (state)
            {
                case State.Recording:
                    _indicator.AddToClassList("indicator--recording");
                    _keyHints.text = "[SPACE] Release to Review";
                    break;
                case State.Reviewing:
                    _indicator.AddToClassList("indicator--reviewing");
                    _keyHints.text = "[ENTER] Send Now  |  [ESC] Cancel";
                    break;
                case State.Playing:
                    _indicator.AddToClassList("indicator--speaking");
                    _keyHints.text = "";
                    break;
                case State.Idle:
                default:
                    _keyHints.text = "[SPACE] Hold to Record";
                    break;
            }
        }

        public void SetStatus(string text)
        {
            _statusLabel.text = text;
        }

        /// <summary>
        /// Shows a toast notification that auto-dismisses after the given duration.
        /// </summary>
        public void ShowToast(string message, float durationSeconds = 3f)
        {
            var toast = new Label(message);
            toast.AddToClassList("toast");
            _root.Add(toast);

            // Schedule removal after duration
            toast.schedule.Execute(() =>
            {
                if (_root.Contains(toast))
                    _root.Remove(toast);
            }).StartingIn((long)(durationSeconds * 1000));
        }

        /// <summary>
        /// Updates the current draft message in the chat log.
        /// Creates it if it doesn't exist.
        /// </summary>
        public void SetUserDraft(string text)
        {
            if (_currentDraftMessage == null)
            {
                _currentDraftMessage = new Label();
                _currentDraftMessage.AddToClassList("msg-user-draft");
                _chatLog.Add(_currentDraftMessage);
                AutoScroll();
            }
            _currentDraftMessage.text = $"You (Draft): {text}";
        }

        /// <summary>
        /// Finalizes the current draft into a permanent user message.
        /// </summary>
        public void FinalizeUserMessage()
        {
            if (_currentDraftMessage != null)
            {
                _currentDraftMessage.RemoveFromClassList("msg-user-draft");
                _currentDraftMessage.AddToClassList("msg-user");
                string rawText = _currentDraftMessage.text.Replace("You (Draft): ", "");
                _currentDraftMessage.text = $"You: {rawText}";

                _currentDraftMessage = null;
            }
        }

        /// <summary>
        /// Removes the current draft message (e.g., cancelled).
        /// </summary>
        public void DiscardDraft()
        {
            if (_currentDraftMessage != null)
            {
                _chatLog.Remove(_currentDraftMessage);
                _currentDraftMessage = null;
            }
        }

        /// <summary>
        /// Updates the current AI response message.
        /// Creates it if it doesn't exist.
        /// </summary>
        public void SetAITranscript(string text)
        {
            if (_currentAIMessage == null)
            {
                _currentAIMessage = new Label();
                _currentAIMessage.AddToClassList("msg-aya");
                _chatLog.Add(_currentAIMessage);
                AutoScroll();
            }
            _currentAIMessage.text = $"Bot: {text}";
            AutoScroll();
        }

        /// <summary>
        /// Marks the current AI turn as complete (stops streaming to the same label).
        /// </summary>
        public void FinalizeAIMessage()
        {
            _currentAIMessage = null;
        }

        public void LogSystemMessage(string text)
        {
            var label = new Label(text);
            label.AddToClassList("msg-system");
            _chatLog.Add(label);
            AutoScroll();
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
