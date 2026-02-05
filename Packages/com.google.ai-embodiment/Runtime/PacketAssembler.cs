using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Buffers incoming text and audio from Gemini Live into correlated
    /// <see cref="SyncPacket"/> instances emitted at sentence boundaries.
    ///
    /// <para>
    /// PacketAssembler is a plain C# class (not MonoBehaviour) that runs entirely
    /// on the main thread. It receives raw events from ProcessResponse via
    /// MainThreadDispatcher, buffers them per-turn with sentence boundary detection,
    /// and emits SyncPackets via a callback. An optional <see cref="ISyncDriver"/>
    /// controls release timing; when no driver is registered, packets release
    /// immediately (Gemini native audio path).
    /// </para>
    /// </summary>
    public class PacketAssembler
    {
        private Action<SyncPacket> _packetCallback;
        private ISyncDriver _syncDriver;

        // Turn state
        private int _nextTurnId;
        private int _currentTurnId;
        private int _sequence;
        private bool _turnActive;

        // Text buffering (sentence boundary)
        private readonly StringBuilder _textBuffer = new StringBuilder();
        private int _textCursor;

        // Audio accumulation
        private readonly List<float[]> _pendingAudio = new List<float[]>();

        // Time-based flush fallback
        private float _lastFlushTime;
        private const float FlushTimeoutSeconds = 0.5f;
        private const int MinFlushChars = 20;

        /// <summary>
        /// Registers the callback that receives assembled <see cref="SyncPacket"/> instances.
        /// </summary>
        /// <param name="callback">Callback invoked for each assembled packet.</param>
        public void SetPacketCallback(Action<SyncPacket> callback)
        {
            _packetCallback = callback;
        }

        /// <summary>
        /// Registers an optional sync driver for timing control. The driver gates
        /// when packets are released to the developer. If no driver is registered,
        /// packets release immediately through the packet callback.
        /// </summary>
        /// <param name="driver">The sync driver to register, or null to clear.</param>
        public void RegisterSyncDriver(ISyncDriver driver)
        {
            _syncDriver = driver;

            if (_syncDriver != null)
            {
                _syncDriver.SetReleaseCallback(packet => _packetCallback?.Invoke(packet));
            }
        }

        /// <summary>
        /// Begins a new turn. Increments the turn ID, resets the sequence counter,
        /// and clears all internal buffers for fresh accumulation.
        /// </summary>
        public void StartTurn()
        {
            _currentTurnId = _nextTurnId++;
            _sequence = 0;
            _turnActive = true;
            _textBuffer.Clear();
            _textCursor = 0;
            _pendingAudio.Clear();
            _lastFlushTime = Time.time;
        }

        /// <summary>
        /// Appends output transcription text and attempts a sentence boundary flush.
        /// Text is buffered until a sentence-ending punctuation (. ? !) followed by
        /// whitespace is detected, or a time-based fallback triggers.
        /// </summary>
        /// <param name="text">Incremental transcription text from Gemini.</param>
        public void AddTranscription(string text)
        {
            if (!_turnActive || string.IsNullOrEmpty(text)) return;

            _textBuffer.Append(text);
            TryFlush();
        }

        /// <summary>
        /// Accumulates an audio chunk into the pending list. Audio is merged into
        /// a single array when a sentence boundary triggers packet emission.
        /// </summary>
        /// <param name="samples">PCM audio samples at 24kHz mono from Gemini.</param>
        public void AddAudio(float[] samples)
        {
            if (!_turnActive || samples == null || samples.Length == 0) return;

            _pendingAudio.Add(samples);
        }

        /// <summary>
        /// Emits a FunctionCall-type <see cref="SyncPacket"/> immediately with the
        /// current turn ID. Stub for Phase 4 full implementation.
        /// </summary>
        /// <param name="name">Function name from the AI.</param>
        /// <param name="args">Function arguments.</param>
        /// <param name="id">Function call ID for response correlation.</param>
        public void AddFunctionCall(string name, IReadOnlyDictionary<string, object> args, string id)
        {
            var packet = new SyncPacket(
                type: SyncPacketType.FunctionCall,
                turnId: _currentTurnId,
                sequence: _sequence++,
                text: string.Empty,
                audio: null,
                functionName: name ?? string.Empty,
                functionArgs: args,
                functionId: id,
                isTurnEnd: false
            );

            ReleasePacket(packet);
        }

        /// <summary>
        /// Force-flushes ALL remaining buffered text and audio as a final packet
        /// with <see cref="SyncPacket.IsTurnEnd"/> set to true, then marks the turn
        /// inactive. Called when Gemini signals TurnComplete.
        /// </summary>
        public void FinishTurn()
        {
            if (!_turnActive) return;

            FlushAll(isTurnEnd: true);
            _turnActive = false;
        }

        /// <summary>
        /// Clears all buffers (text, audio, sentence state) without emitting any
        /// packets. Used on barge-in interruption to discard stale data from the
        /// interrupted turn.
        /// </summary>
        public void CancelTurn()
        {
            _turnActive = false;
            _textBuffer.Clear();
            _textCursor = 0;
            _pendingAudio.Clear();
        }

        /// <summary>
        /// Full reset: clears all buffers and resets the turn counter to 0.
        /// Used on session disconnect for clean state.
        /// </summary>
        public void Reset()
        {
            _turnActive = false;
            _nextTurnId = 0;
            _currentTurnId = 0;
            _sequence = 0;
            _textBuffer.Clear();
            _textCursor = 0;
            _pendingAudio.Clear();
            _lastFlushTime = 0f;
        }

        /// <summary>
        /// Attempts to flush buffered text at a sentence boundary. Falls back to
        /// a time-based flush (500ms timeout with 20+ chars) at word boundaries
        /// to prevent subtitle freezing during long unpunctuated sequences.
        /// </summary>
        private void TryFlush()
        {
            string text = _textBuffer.ToString();
            int boundary = FindSentenceBoundary(text, _textCursor);

            if (boundary > _textCursor)
            {
                // Sentence boundary found -- emit up to boundary
                EmitTextAudioPacket(text.Substring(_textCursor, boundary - _textCursor));
                _textCursor = boundary;
                _lastFlushTime = Time.time;
            }
            else if (Time.time - _lastFlushTime >= FlushTimeoutSeconds
                     && text.Length - _textCursor >= MinFlushChars)
            {
                // Time-based fallback: flush at last word boundary
                int lastSpace = text.LastIndexOf(' ', text.Length - 1, text.Length - _textCursor);
                if (lastSpace > _textCursor)
                {
                    EmitTextAudioPacket(text.Substring(_textCursor, lastSpace - _textCursor));
                    _textCursor = lastSpace + 1; // Skip the space
                    _lastFlushTime = Time.time;
                }
            }
        }

        /// <summary>
        /// Emits a TextAudio <see cref="SyncPacket"/> with the given text and all
        /// accumulated audio merged into a single array.
        /// </summary>
        /// <param name="text">The subtitle text for this packet segment.</param>
        private void EmitTextAudioPacket(string text, bool isTurnEnd = false)
        {
            float[] mergedAudio = MergeAudioChunks();

            var packet = new SyncPacket(
                type: SyncPacketType.TextAudio,
                turnId: _currentTurnId,
                sequence: _sequence++,
                text: text.Trim(),
                audio: mergedAudio,
                functionName: string.Empty,
                functionArgs: null,
                functionId: null,
                isTurnEnd: isTurnEnd
            );

            ReleasePacket(packet);
        }

        /// <summary>
        /// Merges all pending audio chunks into a single contiguous float array.
        /// Returns null if no audio chunks are pending.
        /// </summary>
        /// <returns>Merged audio array, or null if no audio pending.</returns>
        private float[] MergeAudioChunks()
        {
            if (_pendingAudio.Count == 0) return null;

            int totalLength = 0;
            for (int i = 0; i < _pendingAudio.Count; i++)
            {
                totalLength += _pendingAudio[i].Length;
            }

            float[] merged = new float[totalLength];
            int offset = 0;
            for (int i = 0; i < _pendingAudio.Count; i++)
            {
                Array.Copy(_pendingAudio[i], 0, merged, offset, _pendingAudio[i].Length);
                offset += _pendingAudio[i].Length;
            }

            _pendingAudio.Clear();
            return merged;
        }

        /// <summary>
        /// Force-flushes all remaining buffered text and audio as a single packet.
        /// Called from <see cref="FinishTurn"/> to handle unterminated sentences
        /// at turn end (Pitfall 3).
        /// </summary>
        /// <param name="isTurnEnd">True to mark the emitted packet as turn end.</param>
        private void FlushAll(bool isTurnEnd)
        {
            string text = _textBuffer.ToString();
            bool hasRemainingText = _textCursor < text.Length;
            bool hasRemainingAudio = _pendingAudio.Count > 0;

            if (hasRemainingText || hasRemainingAudio)
            {
                string remainingText = hasRemainingText
                    ? text.Substring(_textCursor)
                    : string.Empty;

                EmitTextAudioPacket(remainingText, isTurnEnd);
                _textCursor = text.Length;
            }

            _textBuffer.Clear();
            _textCursor = 0;
            _pendingAudio.Clear();
        }

        /// <summary>
        /// Routes a packet through the registered <see cref="ISyncDriver"/> if present,
        /// or directly to the packet callback for immediate release (Gemini native path).
        /// </summary>
        /// <param name="packet">The packet to release.</param>
        private void ReleasePacket(SyncPacket packet)
        {
            if (_syncDriver != null)
            {
                _syncDriver.OnPacketReady(packet);
            }
            else
            {
                _packetCallback?.Invoke(packet);
            }
        }

        /// <summary>
        /// Finds the index past the last sentence-ending punctuation (. ? !) that is
        /// followed by whitespace or end-of-string. Returns -1 if no boundary found.
        /// Handles abbreviations like "3.14" and "U.S.A" by requiring whitespace after
        /// the punctuation mark.
        /// </summary>
        /// <param name="text">The text to search for sentence boundaries.</param>
        /// <param name="startIndex">Index to start searching from.</param>
        /// <returns>Index past the sentence boundary, or -1 if none found.</returns>
        private static int FindSentenceBoundary(string text, int startIndex)
        {
            int lastBoundary = -1;

            for (int i = startIndex; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '.' || c == '?' || c == '!')
                {
                    // Valid boundary if followed by whitespace or end of text
                    if (i + 1 >= text.Length)
                    {
                        lastBoundary = i + 1;
                    }
                    else if (char.IsWhiteSpace(text[i + 1]))
                    {
                        lastBoundary = i + 1;
                    }
                    // NOT a boundary if followed by non-whitespace
                    // (handles "3.14", "U.S.A", etc.)
                }
            }

            return lastBoundary;
        }
    }
}
