using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// MonoBehaviour controller for the livestream UI.
    /// Binds to a UIDocument containing LivestreamPanel.uxml and manages
    /// the ListView-based chat feed, Aya transcript panel, stream status
    /// indicators, and push-to-talk state.
    /// </summary>
    public class LivestreamUI : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private ListView _chatFeed;
        private Label _liveBadge;
        private Label _viewerCount;
        private Label _durationTimer;
        private ScrollView _ayaTranscript;
        private VisualElement _ayaIndicator;
        private Label _pttStatus;

        // Transcript overlay elements
        private VisualElement _transcriptOverlay;
        private Label _transcriptText;
        private VisualElement _autoSubmitFill;

        // PTT acknowledgment
        private VisualElement _pttAck;

        private readonly List<ChatMessage> _messages = new();
        private float _sessionStartTime;
        private Label _currentAyaMessage;

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;

            _chatFeed = root.Q<ListView>("chat-feed");
            _liveBadge = root.Q<Label>("live-badge");
            _viewerCount = root.Q<Label>("viewer-count");
            _durationTimer = root.Q<Label>("duration-timer");
            _ayaTranscript = root.Q<ScrollView>("aya-transcript");
            _ayaIndicator = root.Q("aya-indicator");
            _pttStatus = root.Q<Label>("ptt-status");

            _transcriptOverlay = root.Q("transcript-overlay");
            _transcriptText = root.Q<Label>("transcript-text");
            _autoSubmitFill = root.Q("auto-submit-fill");
            _pttAck = root.Q("ptt-ack");

            // Configure ListView for chat messages
            _chatFeed.makeItem = MakeChatItem;
            _chatFeed.bindItem = BindChatItem;
            _chatFeed.itemsSource = _messages;
            _chatFeed.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _chatFeed.selectionType = SelectionType.None;

            _sessionStartTime = Time.time;
        }

        /// <summary>
        /// Creates the visual template for a single chat message item in the ListView.
        /// </summary>
        private VisualElement MakeChatItem()
        {
            var container = new VisualElement();
            container.AddToClassList("chat-message");

            var nameLabel = new Label();
            nameLabel.name = "bot-name";
            nameLabel.AddToClassList("bot-name");
            container.Add(nameLabel);

            var textLabel = new Label();
            textLabel.name = "message-text";
            textLabel.AddToClassList("message-text");
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            container.Add(textLabel);

            var timestampLabel = new Label();
            timestampLabel.name = "timestamp";
            timestampLabel.AddToClassList("timestamp");
            container.Add(timestampLabel);

            return container;
        }

        /// <summary>
        /// Binds a ChatMessage to the visual element at the given index.
        /// </summary>
        private void BindChatItem(VisualElement element, int index)
        {
            ChatMessage msg = _messages[index];

            var nameLabel = element.Q<Label>("bot-name");
            var textLabel = element.Q<Label>("message-text");
            var timestampLabel = element.Q<Label>("timestamp");

            nameLabel.text = msg.BotName;
            nameLabel.style.color = msg.BotColor;
            textLabel.text = msg.Text;
            timestampLabel.text = msg.Timestamp;
        }

        /// <summary>
        /// Adds a chat message to the ListView feed.
        /// The message appears at the bottom and the feed auto-scrolls.
        /// </summary>
        /// <param name="msg">The chat message to add.</param>
        public void AddMessage(ChatMessage msg)
        {
            _messages.Add(msg);
            _chatFeed.RefreshItems();
            _chatFeed.schedule.Execute(() => _chatFeed.ScrollToItem(_messages.Count - 1));
        }

        /// <summary>
        /// Updates the Aya transcript panel with streaming speech text.
        /// Creates a new label on the first call per turn, then updates it.
        /// </summary>
        /// <param name="text">The current transcription text.</param>
        public void UpdateAyaTranscript(string text)
        {
            if (_currentAyaMessage == null)
            {
                _currentAyaMessage = new Label();
                _currentAyaMessage.AddToClassList("aya-msg");
                _ayaTranscript.Add(_currentAyaMessage);
            }

            _currentAyaMessage.text = text;

            // Auto-scroll the transcript (deferred, matching AyaChatUI pattern)
            _ayaTranscript.schedule.Execute(() =>
            {
                _ayaTranscript.scrollOffset = new Vector2(
                    0, _ayaTranscript.contentContainer.layout.height);
            });
        }

        /// <summary>
        /// Marks the current Aya turn as complete. The next call to
        /// <see cref="UpdateAyaTranscript"/> will create a new label.
        /// </summary>
        public void CompleteAyaTurn()
        {
            _currentAyaMessage = null;
        }

        /// <summary>
        /// Toggles the speaking indicator on the Aya panel.
        /// </summary>
        /// <param name="speaking">True if Aya is currently speaking.</param>
        public void SetAyaSpeaking(bool speaking)
        {
            if (speaking)
                _ayaIndicator.AddToClassList("aya-indicator--speaking");
            else
                _ayaIndicator.RemoveFromClassList("aya-indicator--speaking");
        }

        /// <summary>
        /// Updates the push-to-talk status text and active state.
        /// </summary>
        /// <param name="text">The status text to display.</param>
        /// <param name="active">Whether the PTT is currently active.</param>
        public void SetPTTStatus(string text, bool active = false)
        {
            _pttStatus.text = text;

            if (active)
                _pttStatus.AddToClassList("ptt-status--active");
            else
                _pttStatus.RemoveFromClassList("ptt-status--active");
        }

        /// <summary>
        /// Updates the viewer count display in the stream status bar.
        /// </summary>
        /// <param name="count">The number of viewers to display.</param>
        public void SetViewerCount(int count)
        {
            _viewerCount.text = $"{count} viewers";
        }

        /// <summary>
        /// Shows or hides the "Aya noticed you" acknowledgment indicator.
        /// Uses CSS class toggle for synchronous, frame-immediate update (&lt;500ms).
        /// </summary>
        public void ShowPTTAcknowledgment(bool show)
        {
            if (_pttAck == null) return;
            if (show)
            {
                _pttAck.RemoveFromClassList("ptt-ack--hidden");
                _pttAck.AddToClassList("ptt-ack--visible");
            }
            else
            {
                _pttAck.RemoveFromClassList("ptt-ack--visible");
                _pttAck.AddToClassList("ptt-ack--hidden");
            }
        }

        /// <summary>
        /// Shows or hides the transcript review overlay.
        /// </summary>
        public void ShowTranscriptOverlay(bool show)
        {
            if (_transcriptOverlay == null) return;
            if (show)
            {
                _transcriptOverlay.RemoveFromClassList("transcript-overlay--hidden");
                _transcriptOverlay.AddToClassList("transcript-overlay--visible");
            }
            else
            {
                _transcriptOverlay.RemoveFromClassList("transcript-overlay--visible");
                _transcriptOverlay.AddToClassList("transcript-overlay--hidden");
            }
        }

        /// <summary>
        /// Sets the transcript text displayed in the review overlay.
        /// </summary>
        public void SetTranscriptText(string text)
        {
            if (_transcriptText != null) _transcriptText.text = text;
        }

        /// <summary>
        /// Updates the auto-submit countdown progress bar.
        /// </summary>
        /// <param name="progress">0.0 (empty) to 1.0 (full).</param>
        public void UpdateAutoSubmitProgress(float progress)
        {
            if (_autoSubmitFill != null)
            {
                _autoSubmitFill.style.width = new StyleLength(Length.Percent(Mathf.Clamp01(progress) * 100f));
            }
        }

        private void Update()
        {
            float elapsed = Time.time - _sessionStartTime;
            int minutes = (int)(elapsed / 60);
            int seconds = (int)(elapsed % 60);
            _durationTimer.text = $"{minutes:D2}:{seconds:D2}";
        }
    }
}
