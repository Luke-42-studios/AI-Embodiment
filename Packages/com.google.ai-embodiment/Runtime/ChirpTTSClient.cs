using System;
using System.Collections.Generic;
using System.Text;
using Google.MiniJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace AIEmbodiment
{
    /// <summary>
    /// HTTP client for Google Cloud Text-to-Speech (Chirp 3 HD) synthesis.
    /// Sends text to the Cloud TTS REST API and returns PCM float[] audio at 24kHz.
    ///
    /// Plain C# class (not MonoBehaviour) -- follows the same pattern as
    /// <see cref="PacketAssembler"/>. Only Unity dependency is <see cref="UnityWebRequest"/>
    /// which must run on the main thread.
    ///
    /// Handles both standard Chirp 3 HD voices (with SSML wrapping) and custom/cloned
    /// voices (plain text with <c>voiceCloningKey</c>).
    /// </summary>
    public class ChirpTTSClient : IDisposable
    {
        private const string TTS_ENDPOINT = "https://texttospeech.googleapis.com/v1/text:synthesize";
        private const int SAMPLE_RATE = 24000;
        private const int WAV_HEADER_SIZE = 44;

        private readonly string _apiKey;
        private bool _disposed;

        /// <summary>
        /// Fired when a TTS request fails. The caller (PersonaSession) subscribes
        /// to this for non-throwing error reporting so the conversation can continue
        /// even if a single synthesis fails.
        /// </summary>
        public event Action<Exception> OnError;

        /// <summary>
        /// Creates a new ChirpTTSClient with the given API key.
        /// Caller obtains the key via <c>Firebase.FirebaseApp.DefaultInstance.Options.ApiKey</c>.
        /// </summary>
        /// <param name="apiKey">Google Cloud API key with Cloud TTS enabled.</param>
        public ChirpTTSClient(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API key must not be null or empty.", nameof(apiKey));
            _apiKey = apiKey;
        }

        /// <summary>
        /// Synthesizes text to PCM audio via the Cloud TTS REST API.
        ///
        /// MUST be called from the main thread (UnityWebRequest requirement).
        /// Uses Unity 6 Awaitable for async HTTP without blocking.
        ///
        /// Standard voices use SSML wrapping; custom voices use plain text
        /// (custom/cloned voices do not support SSML -- Research Pitfall 7).
        /// </summary>
        /// <param name="text">Text to synthesize.</param>
        /// <param name="voiceName">Full Cloud TTS voice name (e.g., "en-US-Chirp3-HD-Achernar").</param>
        /// <param name="languageCode">BCP-47 language code (e.g., "en-US").</param>
        /// <param name="voiceCloningKey">Optional cloning key for custom voices. When provided,
        /// <paramref name="voiceName"/> is ignored and the request uses <c>voiceClone</c> instead.</param>
        /// <returns>PCM float array at 24kHz mono, or null if disposed.</returns>
        /// <exception cref="ObjectDisposedException">If the client has been disposed.</exception>
        /// <exception cref="Exception">If the HTTP request fails.</exception>
        public async Awaitable<float[]> SynthesizeAsync(
            string text,
            string voiceName,
            string languageCode,
            string voiceCloningKey = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChirpTTSClient));

            string json = BuildRequestJson(text, voiceName, languageCode, voiceCloningKey);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(TTS_ENDPOINT, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("x-goog-api-key", _apiKey);

            await request.SendWebRequest();

            if (_disposed)
                return null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                var ex = new Exception(
                    $"Cloud TTS request failed: {request.error}\n" +
                    $"Response: {request.downloadHandler?.text ?? "(no body)"}\n" +
                    "If you see 403: ensure Cloud Text-to-Speech API is enabled in " +
                    "Google Cloud Console and the API key is not restricted.");
                OnError?.Invoke(ex);
                throw ex;
            }

            // Parse response JSON to extract base64 audioContent
            string responseJson = request.downloadHandler.text;
            string audioBase64 = ExtractAudioContent(responseJson);

            if (string.IsNullOrEmpty(audioBase64))
            {
                var ex = new Exception(
                    "Cloud TTS response missing audioContent field.\n" +
                    $"Response: {responseJson}");
                OnError?.Invoke(ex);
                throw ex;
            }

            // Decode base64 to raw bytes
            byte[] audioBytes = Convert.FromBase64String(audioBase64);

            // Strip WAV header and convert LINEAR16 to float[]
            return ConvertLinear16ToFloat(audioBytes);
        }

        /// <summary>
        /// Builds the JSON request body for Cloud TTS synthesis.
        /// Uses MiniJSON for safe serialization.
        /// </summary>
        private static string BuildRequestJson(
            string text,
            string voiceName,
            string languageCode,
            string voiceCloningKey)
        {
            bool isCustomVoice = !string.IsNullOrEmpty(voiceCloningKey);

            // Build input: SSML for standard voices, plain text for custom
            var input = new Dictionary<string, object>();
            if (isCustomVoice)
            {
                input["text"] = text;
            }
            else
            {
                input["ssml"] = $"<speak>{text}</speak>";
            }

            // Build voice configuration
            var voice = new Dictionary<string, object>();
            voice["languageCode"] = languageCode;

            if (isCustomVoice)
            {
                var voiceClone = new Dictionary<string, object>();
                voiceClone["voiceCloningKey"] = voiceCloningKey;
                voice["voiceClone"] = voiceClone;
            }
            else
            {
                voice["name"] = voiceName;
            }

            // Build audio config: LINEAR16 at 24kHz (matches existing playback pipeline)
            var audioConfig = new Dictionary<string, object>();
            audioConfig["audioEncoding"] = "LINEAR16";
            audioConfig["sampleRateHertz"] = SAMPLE_RATE;

            // Assemble top-level request
            var requestBody = new Dictionary<string, object>();
            requestBody["input"] = input;
            requestBody["voice"] = voice;
            requestBody["audioConfig"] = audioConfig;

            return Json.Serialize(requestBody);
        }

        /// <summary>
        /// Extracts the <c>audioContent</c> base64 string from the Cloud TTS JSON response.
        /// </summary>
        private static string ExtractAudioContent(string responseJson)
        {
            if (Json.Deserialize(responseJson) is Dictionary<string, object> response
                && response.TryGetValue("audioContent", out object audioObj)
                && audioObj is string audioBase64)
            {
                return audioBase64;
            }

            return null;
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
