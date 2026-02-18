using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AIEmbodiment
{
    /// <summary>
    /// HTTP client for Google Cloud Text-to-Speech (Chirp 3 HD) synthesis.
    /// Implements <see cref="ITTSProvider"/> for use with any TTS-routed voice backend.
    /// Sends text to the Cloud TTS REST API and returns a <see cref="TTSResult"/> at 24kHz mono.
    ///
    /// Plain C# class (not MonoBehaviour) -- follows the same pattern as
    /// <see cref="PacketAssembler"/>. Only Unity dependency is <see cref="UnityWebRequest"/>
    /// which must run on the main thread.
    ///
    /// Supports two authentication paths:
    /// <list type="bullet">
    /// <item><description>
    /// <b>API key</b> (v1 endpoint): Standard Chirp voices via <c>x-goog-api-key</c> header.
    /// </description></item>
    /// <item><description>
    /// <b>Bearer token</b> (v1beta1 endpoint): OAuth2 via <see cref="GoogleServiceAccountAuth"/>.
    /// Required for custom/cloned voices. Also works for standard voices.
    /// </description></item>
    /// </list>
    ///
    /// Handles both standard Chirp 3 HD voices (with SSML wrapping) and custom/cloned
    /// voices (plain text with voice cloning key passed at construction).
    /// </summary>
    public class ChirpTTSClient : IDisposable, ITTSProvider
    {
        private const string TTS_ENDPOINT_V1 = "https://texttospeech.googleapis.com/v1/text:synthesize";
        private const string TTS_ENDPOINT_V1BETA1 = "https://texttospeech.googleapis.com/v1beta1/text:synthesize";
        private const int SAMPLE_RATE = 24000;
        private const int WAV_HEADER_SIZE = 44;

        private readonly string _apiKey;
        private readonly GoogleServiceAccountAuth _auth;
        private readonly string _voiceCloningKey;
        private bool _disposed;

        /// <summary>
        /// Creates a new ChirpTTSClient with API key authentication (v1 endpoint).
        /// Caller obtains the API key via <c>AIEmbodimentSettings.Instance.ApiKey</c>.
        /// </summary>
        /// <param name="apiKey">Google Cloud API key with Cloud TTS enabled.</param>
        /// <param name="voiceCloningKey">Optional cloning key for custom voices. When provided,
        /// requests use <c>voiceClone</c> instead of a named voice.
        /// Note: custom voice cloning requires bearer auth -- use the
        /// <see cref="ChirpTTSClient(GoogleServiceAccountAuth, string)"/> constructor instead.</param>
        public ChirpTTSClient(string apiKey, string voiceCloningKey = null)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API key must not be null or empty.", nameof(apiKey));
            _apiKey = apiKey;
            _auth = null;
            _voiceCloningKey = voiceCloningKey;
        }

        /// <summary>
        /// Creates a new ChirpTTSClient with OAuth2 bearer token authentication (v1beta1 endpoint).
        /// Required for custom/cloned voices; also works for standard voices.
        /// The caller (typically <see cref="PersonaSession"/>) owns the lifetime of
        /// <paramref name="auth"/> -- this client does NOT dispose it.
        /// </summary>
        /// <param name="auth">Service account credential provider. Must not be null.
        /// See <see cref="GoogleServiceAccountAuth"/>.</param>
        /// <param name="voiceCloningKey">Optional cloning key for custom voices. When provided,
        /// requests use <c>voiceClone</c> instead of a named voice.</param>
        public ChirpTTSClient(GoogleServiceAccountAuth auth, string voiceCloningKey = null)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _apiKey = null;
            _voiceCloningKey = voiceCloningKey;
        }

        /// <summary>
        /// Synthesizes text to PCM audio via the Cloud TTS REST API.
        ///
        /// MUST be called from the main thread (UnityWebRequest requirement).
        /// Uses Unity 6 Awaitable for async HTTP without blocking.
        ///
        /// Standard voices use SSML wrapping; custom voices use plain text
        /// (custom/cloned voices do not support SSML -- Research Pitfall 7).
        ///
        /// The <paramref name="onAudioChunk"/> parameter is accepted for interface
        /// compatibility but is not used. Cloud TTS REST is non-streaming; the full
        /// result is always returned at completion.
        /// </summary>
        /// <param name="text">Text to synthesize.</param>
        /// <param name="voiceName">Full Cloud TTS voice name (e.g., "en-US-Chirp3-HD-Achernar").</param>
        /// <param name="languageCode">BCP-47 language code (e.g., "en-US").</param>
        /// <param name="onAudioChunk">Ignored -- REST-based provider returns full result only.</param>
        /// <returns>TTSResult at 24kHz mono, or default if disposed during request.</returns>
        /// <exception cref="ObjectDisposedException">If the client has been disposed.</exception>
        /// <exception cref="Exception">If the HTTP request fails.</exception>
        public async Awaitable<TTSResult> SynthesizeAsync(
            string text,
            string voiceName,
            string languageCode,
            Action<TTSResult> onAudioChunk = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChirpTTSClient));

            string json = BuildRequestJson(text, voiceName, languageCode, _voiceCloningKey);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            string endpoint = _auth != null ? TTS_ENDPOINT_V1BETA1 : TTS_ENDPOINT_V1;

            using var request = new UnityWebRequest(endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");

            if (_auth != null)
            {
                string token = await _auth.GetAccessTokenAsync();
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.SetRequestHeader("x-goog-user-project", _auth.ProjectId);
            }
            else
            {
                request.SetRequestHeader("x-goog-api-key", _apiKey);
            }

            await request.SendWebRequest();

            if (_disposed)
                return default;

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"Cloud TTS request failed ({endpoint}): {request.error}\n" +
                    $"Response: {request.downloadHandler?.text ?? "(no body)"}\n" +
                    (_auth != null
                        ? "Check: service account has Cloud TTS API access, project billing is enabled."
                        : "Check: API key has Cloud TTS API enabled in Google Cloud Console."));
            }

            // Parse response JSON to extract base64 audioContent
            string responseJson = request.downloadHandler.text;
            string audioBase64 = ExtractAudioContent(responseJson);

            if (string.IsNullOrEmpty(audioBase64))
            {
                throw new Exception(
                    "Cloud TTS response missing audioContent field.\n" +
                    $"Response: {responseJson}");
            }

            // Decode base64 to raw bytes
            byte[] audioBytes = Convert.FromBase64String(audioBase64);

            // Strip WAV header and convert LINEAR16 to float[]
            return new TTSResult(ConvertLinear16ToFloat(audioBytes), SAMPLE_RATE, 1);
        }

        /// <summary>
        /// Builds the JSON request body for Cloud TTS synthesis.
        /// Uses Newtonsoft.Json for safe serialization.
        /// </summary>
        private static string BuildRequestJson(
            string text,
            string voiceName,
            string languageCode,
            string voiceCloningKey)
        {
            bool isCustomVoice = !string.IsNullOrEmpty(voiceCloningKey);

            // Build input: SSML for standard voices, plain text for custom
            var input = new JObject();
            if (isCustomVoice)
            {
                input["text"] = text;
            }
            else
            {
                input["ssml"] = $"<speak>{text}</speak>";
            }

            // Build voice configuration
            var voice = new JObject();
            voice["languageCode"] = languageCode;

            if (isCustomVoice)
            {
                var voiceClone = new JObject();
                voiceClone["voiceCloningKey"] = voiceCloningKey;
                voice["voiceClone"] = voiceClone;
            }
            else
            {
                voice["name"] = voiceName;
            }

            // Build audio config: LINEAR16 at 24kHz (matches existing playback pipeline)
            var audioConfig = new JObject();
            audioConfig["audioEncoding"] = "LINEAR16";
            audioConfig["sampleRateHertz"] = SAMPLE_RATE;

            // Assemble top-level request
            var obj = new JObject();
            obj["input"] = input;
            obj["voice"] = voice;
            obj["audioConfig"] = audioConfig;

            return obj.ToString(Formatting.None);
        }

        /// <summary>
        /// Extracts the <c>audioContent</c> base64 string from the Cloud TTS JSON response.
        /// </summary>
        private static string ExtractAudioContent(string responseJson)
        {
            var response = JObject.Parse(responseJson);
            return response["audioContent"]?.ToString();
        }

        /// <summary>
        /// Strips the 44-byte WAV header (if present) from LINEAR16 audio bytes and
        /// converts 16-bit signed little-endian samples to float[-1..1] range.
        /// Validates the RIFF header; if absent, treats entire buffer as raw PCM.
        /// </summary>
        private static float[] ConvertLinear16ToFloat(byte[] audioBytes)
        {
            int pcmStart = 0;

            // Check for RIFF WAV header: bytes 0-3 should be 0x52 0x49 0x46 0x46
            if (audioBytes.Length >= WAV_HEADER_SIZE
                && audioBytes[0] == 0x52
                && audioBytes[1] == 0x49
                && audioBytes[2] == 0x46
                && audioBytes[3] == 0x46)
            {
                pcmStart = WAV_HEADER_SIZE;
            }
            else
            {
                Debug.LogWarning(
                    "ChirpTTSClient: Audio response missing RIFF header. " +
                    "Treating entire buffer as raw PCM.");
            }

            int byteCount = audioBytes.Length - pcmStart;
            int sampleCount = byteCount / 2; // 16-bit = 2 bytes per sample
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(audioBytes, pcmStart + i * 2);
                samples[i] = sample / 32768f;
            }

            return samples;
        }

        /// <summary>
        /// Disposes the client. After disposal, <see cref="SynthesizeAsync"/> throws
        /// <see cref="ObjectDisposedException"/>.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }
}
