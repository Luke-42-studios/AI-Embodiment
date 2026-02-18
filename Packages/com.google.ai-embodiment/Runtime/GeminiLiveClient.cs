using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

            // Fire-and-forget receive loop
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

        /// <summary>
        /// Signal end of audio stream to flush cached audio on the server.
        /// Call when the microphone is muted or push-to-talk is released so the
        /// server's VAD processes the buffered audio and generates a response.
        /// </summary>
        public void SendAudioStreamEnd()
        {
            if (!IsConnected) return;

            var payload = new JObject
            {
                ["realtimeInput"] = new JObject
                {
                    ["audioStreamEnd"] = true
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

        /// <summary>Send a tool response back to Gemini for function call correlation.</summary>
        /// <param name="callId">The function call ID from the original toolCall event.</param>
        /// <param name="functionName">The function name matching the original call.</param>
        /// <param name="response">The response dictionary, or null for an empty response.</param>
        public void SendToolResponse(string callId, string functionName, IDictionary<string, object> response)
        {
            if (!IsConnected || string.IsNullOrEmpty(callId)) return;

            var responseObj = response != null ? JObject.FromObject(response) : new JObject();
            var payload = new JObject
            {
                ["toolResponse"] = new JObject
                {
                    ["functionResponses"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = callId,
                            ["name"] = functionName,
                            ["response"] = responseObj
                        }
                    }
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

            // Tools (function declarations for native function calling)
            if (_config.ToolsJson != null && _config.ToolsJson.Count > 0)
            {
                setupInner["tools"] = _config.ToolsJson;
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

        /// <summary>
        /// Background receive loop. Reads WebSocket frames, accumulates multi-frame
        /// messages via MemoryStream, detects JSON via first-byte check, and dispatches
        /// to HandleJsonMessage. Handles Close frames, cancellation, and errors.
        /// </summary>
        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];

            try
            {
                while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
                {
                    // Accumulate multi-frame message
                    var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _connected = false;
                            _setupComplete = false;
                            Enqueue(new GeminiEvent
                            {
                                Type = GeminiEventType.Disconnected,
                                Text = $"Server closed: {result.CloseStatus} - {result.CloseStatusDescription}"
                            });
                            return;
                        }
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    var bytes = ms.ToArray();

                    // Gemini sends ALL messages (including JSON) as Binary frames (Pitfall 3).
                    // Detect JSON by checking the first byte rather than relying on MessageType.
                    bool isJson = bytes.Length > 0 && (bytes[0] == '{' || bytes[0] == '[');

                    if (!isJson)
                    {
                        // Defensive: raw binary audio (Gemini typically wraps audio in JSON,
                        // but handle raw binary gracefully if it ever occurs)
                        Enqueue(new GeminiEvent
                        {
                            Type = GeminiEventType.Audio,
                            AudioData = new float[0],
                            AudioSampleRate = 24000
                        });
                        continue;
                    }

                    var text = Encoding.UTF8.GetString(bytes);
                    HandleJsonMessage(text);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown -- do nothing
            }
            catch (WebSocketException ex)
            {
                _connected = false;
                _setupComplete = false;
                Enqueue(new GeminiEvent { Type = GeminiEventType.Error, Text = ex.Message });
            }
            catch (Exception ex)
            {
                _connected = false;
                _setupComplete = false;
                Enqueue(new GeminiEvent { Type = GeminiEventType.Error, Text = ex.ToString() });
            }
        }

        /// <summary>
        /// Parse a JSON message from the Gemini Live API and dispatch typed events.
        /// Handles: setupComplete, serverContent (audio, transcriptions, turn lifecycle),
        /// toolCall (function calls), toolCallCancellation, and goAway.
        /// </summary>
        private void HandleJsonMessage(string json)
        {
            JObject msg;
            try
            {
                msg = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                Enqueue(new GeminiEvent { Type = GeminiEventType.Error, Text = "JSON parse error: " + ex.Message });
                return;
            }

            // setupComplete -> Connected
            if (msg["setupComplete"] != null)
            {
                _setupComplete = true;
                Enqueue(new GeminiEvent { Type = GeminiEventType.Connected, Text = "Session started" });
                return;
            }

            // serverContent
            var serverContent = msg["serverContent"] as JObject;
            if (serverContent != null)
            {
                // modelTurn.parts[] -- audio and text
                var modelTurn = serverContent["modelTurn"] as JObject;
                if (modelTurn != null)
                {
                    var parts = modelTurn["parts"] as JArray;
                    if (parts != null)
                    {
                        foreach (var part in parts)
                        {
                            // inlineData -> Audio event (base64 -> bytes -> float[])
                            var inlineData = part["inlineData"] as JObject;
                            if (inlineData != null)
                            {
                                var b64 = inlineData["data"]?.ToString();
                                if (!string.IsNullOrEmpty(b64))
                                {
                                    var audioBytes = Convert.FromBase64String(b64);

                                    // Convert 16-bit PCM bytes to float[] (Pitfall 2)
                                    int sampleCount = audioBytes.Length / 2;
                                    float[] floats = new float[sampleCount];
                                    for (int i = 0; i < sampleCount; i++)
                                    {
                                        short sample = (short)(audioBytes[i * 2] | (audioBytes[i * 2 + 1] << 8));
                                        floats[i] = sample / 32768f;
                                    }

                                    Enqueue(new GeminiEvent
                                    {
                                        Type = GeminiEventType.Audio,
                                        AudioData = floats,
                                        AudioSampleRate = 24000
                                    });
                                }
                            }

                            // text part -> TextPart (model text output, not spoken audio transcription)
                            var textToken = part["text"];
                            if (textToken != null)
                            {
                                var t = textToken.ToString();
                                if (!string.IsNullOrEmpty(t))
                                {
                                    Enqueue(new GeminiEvent
                                    {
                                        Type = GeminiEventType.TextPart,
                                        Text = t
                                    });
                                }
                            }
                        }
                    }
                }

                // turnComplete
                var turnComplete = serverContent["turnComplete"];
                if (turnComplete != null && turnComplete.Value<bool>())
                {
                    Enqueue(new GeminiEvent { Type = GeminiEventType.TurnComplete });
                }

                // interrupted
                var interrupted = serverContent["interrupted"];
                if (interrupted != null && interrupted.Value<bool>())
                {
                    Enqueue(new GeminiEvent { Type = GeminiEventType.Interrupted });
                }

                // outputTranscription (AI speech text -- AUD-04, inside serverContent)
                var outputTranscription = serverContent["outputTranscription"] as JObject;
                if (outputTranscription != null)
                {
                    var t = outputTranscription["text"]?.ToString();
                    if (!string.IsNullOrEmpty(t))
                    {
                        Enqueue(new GeminiEvent
                        {
                            Type = GeminiEventType.OutputTranscription,
                            Text = t
                        });
                    }
                }

                // inputTranscription (user STT -- AUD-03, inside serverContent)
                var inputTranscription = serverContent["inputTranscription"] as JObject;
                if (inputTranscription != null)
                {
                    var t = inputTranscription["text"]?.ToString();
                    if (!string.IsNullOrEmpty(t))
                    {
                        Enqueue(new GeminiEvent
                        {
                            Type = GeminiEventType.InputTranscription,
                            Text = t
                        });
                    }
                }
            }

            // toolCall (top-level, alternative to serverContent)
            var toolCall = msg["toolCall"] as JObject;
            if (toolCall != null)
            {
                var functionCalls = toolCall["functionCalls"] as JArray;
                if (functionCalls != null)
                {
                    foreach (var fc in functionCalls)
                    {
                        var name = fc["name"]?.ToString();
                        var args = fc["args"]?.ToString(Formatting.None) ?? "{}";

                        Enqueue(new GeminiEvent
                        {
                            Type = GeminiEventType.FunctionCall,
                            FunctionName = name,
                            FunctionArgsJson = args,
                            FunctionId = fc["id"]?.ToString()
                        });
                    }
                }
            }

            // toolCallCancellation -- cancel pending function calls (user interruption)
            var toolCallCancellation = msg["toolCallCancellation"] as JObject;
            if (toolCallCancellation != null)
            {
                var ids = toolCallCancellation["ids"] as JArray;
                if (ids != null)
                {
                    foreach (var id in ids)
                    {
                        Enqueue(new GeminiEvent
                        {
                            Type = GeminiEventType.FunctionCallCancellation,
                            FunctionId = id.ToString()
                        });
                    }
                }
            }

            // goAway -- informational: session ending soon
            if (msg["goAway"] != null)
            {
                Enqueue(new GeminiEvent
                {
                    Type = GeminiEventType.Error,
                    Text = "Server sent goAway -- session ending soon"
                });
            }
        }
    }
}
