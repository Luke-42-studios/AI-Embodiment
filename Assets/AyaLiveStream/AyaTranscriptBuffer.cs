using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Ring buffer of Aya's recent transcript turns for prompt enrichment.
    /// Populated by LivestreamController subscribing to PersonaSession.OnOutputTranscription
    /// and OnTurnComplete. Provides GetRecentTurns(n) so ChatBotManager can include
    /// Aya's recent speech context in dynamic Gemini bot prompts (Aya-to-bot context flow).
    ///
    /// Plain C# class (NOT MonoBehaviour) -- created in LivestreamController.Start().
    /// </summary>
    public class AyaTranscriptBuffer
    {
        private readonly List<string> _turns = new();
        private readonly StringBuilder _currentTurn = new();
        private readonly int _maxTurns;

        /// <summary>
        /// Creates a new transcript buffer with the specified maximum turn capacity.
        /// Oldest turns are discarded when the buffer exceeds this limit.
        /// </summary>
        /// <param name="maxTurns">Maximum number of completed turns to retain (default 5).</param>
        public AyaTranscriptBuffer(int maxTurns = 5)
        {
            _maxTurns = maxTurns;
        }

        /// <summary>
        /// Appends streaming transcription text to the current in-progress turn.
        /// Called on each PersonaSession.OnOutputTranscription event.
        /// </summary>
        public void AppendText(string text)
        {
            _currentTurn.Append(text);
        }

        /// <summary>
        /// Completes the current turn, adding it to the buffer. If the buffer exceeds
        /// maxTurns, the oldest turn is removed. No-op if the current turn is empty.
        /// Called on PersonaSession.OnTurnComplete.
        /// </summary>
        public void CompleteTurn()
        {
            if (_currentTurn.Length > 0)
            {
                _turns.Add(_currentTurn.ToString());
                _currentTurn.Clear();
                if (_turns.Count > _maxTurns)
                    _turns.RemoveAt(0);
            }
        }

        /// <summary>
        /// Returns the last N completed turns formatted as bullet points.
        /// Each turn appears as: - "turn text"
        /// Returns empty string if no turns have been completed.
        /// </summary>
        /// <param name="count">Number of recent turns to return.</param>
        public string GetRecentTurns(int count)
        {
            if (_turns.Count == 0)
                return string.Empty;

            int start = Mathf.Max(0, _turns.Count - count);
            var sb = new StringBuilder();
            for (int i = start; i < _turns.Count; i++)
            {
                sb.AppendLine($"- \"{_turns[i]}\"");
            }
            return sb.ToString();
        }
    }
}
