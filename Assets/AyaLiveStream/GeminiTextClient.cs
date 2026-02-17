using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// HTTP client for the Gemini <c>generateContent</c> REST API with structured JSON output.
    /// Sends a prompt and a response schema to the specified model and returns a typed,
    /// deserialized result.
    ///
    /// Plain C# class (not MonoBehaviour) -- follows the same pattern as
    /// <see cref="AIEmbodiment.ChirpTTSClient"/>. Only Unity dependency is
    /// <see cref="UnityWebRequest"/> which must run on the main thread.
    ///
    /// Authentication uses an API key passed via <c>x-goog-api-key</c> header.
    /// The caller obtains the key from <c>AIEmbodimentSettings.Instance.ApiKey</c>.
    /// </summary>
    public class GeminiTextClient : IDisposable
    {
        private readonly string _apiKey;
        private readonly string _endpoint;
        private bool _disposed;

        /// <summary>
        /// Creates a new GeminiTextClient with API key authentication.
        /// </summary>
        /// <param name="apiKey">Google AI API key with Gemini API enabled.
        /// Caller reads from <c>AIEmbodimentSettings.Instance.ApiKey</c>.</param>
        /// <param name="model">Gemini model name. Defaults to <c>"gemini-2.5-flash"</c>.</param>
        /// <exception cref="ArgumentException">If <paramref name="apiKey"/> is null or empty.</exception>
        public GeminiTextClient(string apiKey, string model = "gemini-2.5-flash")
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API key must not be null or empty.", nameof(apiKey));

            _apiKey = apiKey;
            _endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        }

        /// <summary>
        /// Sends a prompt with a response schema to the Gemini REST API and returns the
        /// deserialized structured output.
        ///
        /// MUST be called from the main thread (<see cref="UnityWebRequest"/> requirement).
        /// Uses Unity 6 <see cref="Awaitable{T}"/> for async HTTP without blocking.
        ///
        /// The <paramref name="responseSchema"/> uses OpenAPI-style UPPERCASE type constants
        /// (STRING, OBJECT, ARRAY, BOOLEAN, NUMBER, INTEGER) as required by the
        /// <c>v1beta</c> <c>responseSchema</c> field.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the structured JSON response into.</typeparam>
        /// <param name="prompt">The text prompt to send to Gemini.</param>
        /// <param name="responseSchema">A <see cref="JObject"/> describing the expected response
        /// structure using OpenAPI-style schema with UPPERCASE type names.</param>
        /// <returns>The deserialized response, or <c>default</c> if the client was disposed
        /// during the request.</returns>
        /// <exception cref="ObjectDisposedException">If the client has been disposed before the call.</exception>
        /// <exception cref="Exception">If the HTTP request fails or the response is malformed.</exception>
        public async Awaitable<T> GenerateAsync<T>(string prompt, JObject responseSchema)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GeminiTextClient));

            var requestBody = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["parts"] = new JArray { new JObject { ["text"] = prompt } }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json",
                    ["responseSchema"] = responseSchema
                }
            };

            byte[] bodyBytes = Encoding.UTF8.GetBytes(requestBody.ToString(Formatting.None));

            using var request = new UnityWebRequest(_endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", _apiKey);

            await request.SendWebRequest();

            if (_disposed)
                return default;

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"Gemini generateContent request failed ({_endpoint}): {request.error}\n" +
                    $"Response: {request.downloadHandler?.text ?? "(no body)"}\n" +
                    "Check: API key has Gemini API enabled in Google AI Studio.");
            }

            // Gemini wraps structured JSON inside candidates[0].content.parts[0].text
            var response = JObject.Parse(request.downloadHandler.text);
            string jsonText = response["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            if (jsonText == null)
            {
                throw new Exception(
                    "Gemini response missing structured output at candidates[0].content.parts[0].text.\n" +
                    $"Response: {request.downloadHandler.text}");
            }

            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        /// <summary>
        /// Disposes the client. After disposal, <see cref="GenerateAsync{T}"/> throws
        /// <see cref="ObjectDisposedException"/>.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }
}
