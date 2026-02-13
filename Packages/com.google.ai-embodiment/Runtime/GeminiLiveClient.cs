using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AIEmbodiment
{
    /// <summary>Pure C# WebSocket client for the Gemini Live API.</summary>
    public class GeminiLiveClient : IDisposable
    {
        private const string BaseUrl =
            "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";

        private readonly GeminiLiveConfig _config;
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<GeminiEvent> _eventQueue = new ConcurrentQueue<GeminiEvent>();

        private volatile bool _connected;
        private volatile bool _setupComplete;

        /// <summary>Fired for every event. Called from ProcessEvents() on your thread.</summary>
        public event Action<GeminiEvent> OnEvent;

        /// <summary>True after WebSocket connects and setup handshake completes.</summary>
        public bool IsConnected => _connected && _setupComplete;

        public GeminiLiveClient(GeminiLiveConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Connect to the Gemini Live API and start the setup handshake.</summary>
        public async Task ConnectAsync()
        {
            if (_connected) return;

            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            var url = $"{BaseUrl}?key={_config.ApiKey}";
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            _connected = true;

            await SendSetupMessage();

            // Fire-and-forget receive loop (stub -- Plan 02 fills this in)
            _ = ReceiveLoop(_cts.Token);
        }

        /// <summary>Disconnect and clean up resources.</summary>
        public void Disconnect()
        {
            if (!_connected && _ws == null) return;

            _connected = false;
            _setupComplete = false;

            // Cancel CTS first so receive loop exits cleanly (Pitfall 5)
            _cts?.Cancel();

            try
            {
                if (_ws?.State == WebSocketState.Open || _ws?.State == WebSocketState.CloseReceived)
                {
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect",
                        CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch
            {
                // Swallow -- tearing down
            }

            _ws?.Dispose();
            _ws = null;
            _cts?.Dispose();
            _cts = null;

            Enqueue(new GeminiEvent { Type = GeminiEventType.Disconnected, Text = "Disconnected" });
        }

        /// <summary>Send raw PCM audio via realtimeInput.audio (non-deprecated format).</summary>
        public void SendAudio(byte[] pcm16Data)
        {
            if (!IsConnected || pcm16Data == null || pcm16Data.Length == 0) return;

            var encoded = Convert.ToBase64String(pcm16Data);
            var payload = new JObject
            {
                ["realtimeInput"] = new JObject
                {
                    ["audio"] = new JObject
                    {
                        ["mimeType"] = $"audio/pcm;rate={_config.AudioInputSampleRate}",
                        ["data"] = encoded
                    }
                }
            };
            _ = SendJsonAsync(payload);
        }

        /// <summary>Send a text message as clientContent with turnComplete.</summary>
        public void SendText(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(message)) return;

            var payload = new JObject
            {
                ["clientContent"] = new JObject
                {
                    ["turns"] = new JArray
                    {
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray { new JObject { ["text"] = message } }
                        }
                    },
                    ["turnComplete"] = true
                }
            };
            _ = SendJsonAsync(payload);
        }

        /// <summary>Drain the event queue and invoke OnEvent for each. Call from Update().</summary>
        public void ProcessEvents()
        {
            while (_eventQueue.TryDequeue(out var ev))
            {
                OnEvent?.Invoke(ev);
            }
        }

        /// <summary>Dispose calls Disconnect.</summary>
        public void Dispose()
        {
            Disconnect();
        }

        // =====================================================================
        // Setup message
        // =====================================================================

        private async Task SendSetupMessage()
        {
            var setupInner = new JObject();

            // Model -- CRITICAL: include "models/" prefix (Pitfall 4)
            setupInner["model"] = "models/" + _config.Model;

            // Generation config -- AUDIO-only modality
            var genConfig = new JObject
            {
                ["responseModalities"] = new JArray("AUDIO"),
                ["speechConfig"] = new JObject
                {
                    ["voiceConfig"] = new JObject
                    {
                        ["prebuiltVoiceConfig"] = new JObject
                        {
                            ["voiceName"] = _config.VoiceName ?? "Puck"
                        }
                    }
                }
            };
            setupInner["generationConfig"] = genConfig;

            // Enable transcription (AUD-03, AUD-04)
            setupInner["outputAudioTranscription"] = new JObject();
            setupInner["inputAudioTranscription"] = new JObject();

            // System instruction (optional)
            if (!string.IsNullOrEmpty(_config.SystemInstruction))
            {
                setupInner["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray { new JObject { ["text"] = _config.SystemInstruction } }
                };
            }

            var setup = new JObject { ["setup"] = setupInner };
            await SendJsonAsync(setup);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private async Task SendJsonAsync(JObject payload)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;

            var bytes = Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None));
            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true,
                    _cts?.Token ?? CancellationToken.None);
            }
            catch
            {
                // Connection may have been torn down; swallow
            }
        }

        private void Enqueue(GeminiEvent ev)
        {
            _eventQueue.Enqueue(ev);
        }

        /// <summary>Receive loop stub -- Plan 02 replaces this with the full implementation.</summary>
        private async Task ReceiveLoop(CancellationToken ct)
        {
            await Task.CompletedTask;
        }
    }
}
