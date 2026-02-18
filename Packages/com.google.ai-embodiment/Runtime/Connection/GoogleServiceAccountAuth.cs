using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AIEmbodiment
{
    /// <summary>
    /// OAuth2 service account credential provider for Google Cloud APIs.
    /// Loads a service account JSON key, signs a JWT with RSA SHA-256,
    /// exchanges it for an access token at <c>oauth2.googleapis.com/token</c>,
    /// and caches the token with automatic refresh before expiry.
    ///
    /// Plain C# class (not MonoBehaviour). Implements <see cref="IDisposable"/>
    /// to clean up the RSA key material.
    ///
    /// Usage:
    /// <code>
    /// string json = AIEmbodimentSettings.Instance.LoadServiceAccountJson();
    /// using var auth = new GoogleServiceAccountAuth(json);
    /// string token = await auth.GetAccessTokenAsync();
    /// </code>
    /// </summary>
    public class GoogleServiceAccountAuth : IDisposable
    {
        private const int TOKEN_REFRESH_MARGIN_SECONDS = 300; // 5 minutes before expiry

        private readonly string _clientEmail;
        private readonly string _projectId;
        private readonly RSA _rsa;

        private string _cachedToken;
        private DateTime _tokenExpiry;
        private bool _disposed;

        /// <summary>
        /// Creates a new credential provider from a service account JSON key string.
        /// Parses the JSON, extracts credentials, and imports the RSA private key.
        /// </summary>
        /// <param name="serviceAccountJson">Full JSON content of a Google service account key file.</param>
        /// <exception cref="ArgumentException">If JSON is null/empty or missing required fields.</exception>
        public GoogleServiceAccountAuth(string serviceAccountJson)
        {
            if (string.IsNullOrEmpty(serviceAccountJson))
                throw new ArgumentException(
                    "Service account JSON must not be null or empty.",
                    nameof(serviceAccountJson));

            var json = JObject.Parse(serviceAccountJson);

            _clientEmail = json["client_email"]?.ToString();
            _projectId = json["project_id"]?.ToString();
            string privateKeyPem = json["private_key"]?.ToString();

            if (string.IsNullOrEmpty(_clientEmail))
                throw new ArgumentException(
                    "Service account JSON missing 'client_email' field.",
                    nameof(serviceAccountJson));

            if (string.IsNullOrEmpty(_projectId))
                throw new ArgumentException(
                    "Service account JSON missing 'project_id' field.",
                    nameof(serviceAccountJson));

            if (string.IsNullOrEmpty(privateKeyPem))
                throw new ArgumentException(
                    "Service account JSON missing 'private_key' field.",
                    nameof(serviceAccountJson));

            // Strip PEM headers and decode base64 to raw PKCS#8 bytes
            byte[] keyBytes = Convert.FromBase64String(
                privateKeyPem
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", ""));

            _rsa = RSA.Create();
            _rsa.ImportPkcs8PrivateKey(keyBytes, out _);
        }

        /// <summary>
        /// The Google Cloud project ID from the service account JSON.
        /// Used by ChirpTTSClient for the <c>x-goog-user-project</c> header.
        /// </summary>
        public string ProjectId => _projectId;

        /// <summary>
        /// Returns a cached OAuth2 access token, refreshing it if expired or
        /// about to expire within <see cref="TOKEN_REFRESH_MARGIN_SECONDS"/>.
        ///
        /// MUST be called from the main thread (UnityWebRequest requirement
        /// during token exchange).
        /// </summary>
        /// <returns>A valid OAuth2 bearer access token string.</returns>
        /// <exception cref="ObjectDisposedException">If the provider has been disposed.</exception>
        /// <exception cref="Exception">If token exchange fails.</exception>
        public async Awaitable<string> GetAccessTokenAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GoogleServiceAccountAuth));

            if (_cachedToken != null
                && DateTime.UtcNow.AddSeconds(TOKEN_REFRESH_MARGIN_SECONDS) < _tokenExpiry)
            {
                return _cachedToken;
            }

            string jwt = BuildSignedJwt();
            _cachedToken = await ExchangeJwtForToken(jwt);
            return _cachedToken;
        }

        /// <summary>
        /// Builds a signed JWT (RS256) for the Google OAuth2 token exchange.
        /// Header: <c>{"alg":"RS256","typ":"JWT"}</c>
        /// Claims: iss, scope, aud, iat, exp (1 hour lifetime).
        /// </summary>
        private string BuildSignedJwt()
        {
            long iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long exp = iat + 3600; // 1 hour

            string header = Base64UrlEncode(
                Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));

            string claims = Base64UrlEncode(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    iss = _clientEmail,
                    scope = "https://www.googleapis.com/auth/cloud-platform",
                    aud = "https://oauth2.googleapis.com/token",
                    iat = iat,
                    exp = exp
                })));

            string input = header + "." + claims;
            byte[] signature = _rsa.SignData(
                Encoding.UTF8.GetBytes(input),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            return input + "." + Base64UrlEncode(signature);
        }

        /// <summary>
        /// Base64url-encodes data per RFC 4648 section 5 (JWT requirement).
        /// CRITICAL: Must use Base64url, not standard Base64 (Research Pitfall 2).
        /// </summary>
        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Exchanges a signed JWT for an OAuth2 access token via form-encoded POST.
        /// CRITICAL: Must use form-encoded POST, NOT JSON body (Research Pitfall 4).
        /// </summary>
        private async Awaitable<string> ExchangeJwtForToken(string jwt)
        {
            var form = new WWWForm();
            form.AddField("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
            form.AddField("assertion", jwt);

            using var request = UnityWebRequest.Post(
                "https://oauth2.googleapis.com/token", form);

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"OAuth2 token exchange failed: {request.error}\n" +
                    $"Response: {request.downloadHandler?.text ?? "(no body)"}\n" +
                    "Verify that the service account JSON is valid and the " +
                    "Cloud Text-to-Speech API is enabled for the project.");
            }

            var response = JObject.Parse(request.downloadHandler.text);
            string accessToken = response["access_token"]?.ToString();
            int expiresIn = response["expires_in"]?.Value<int>() ?? 3600;

            if (string.IsNullOrEmpty(accessToken))
            {
                throw new Exception(
                    "OAuth2 token exchange response missing 'access_token' field.\n" +
                    $"Response: {request.downloadHandler.text}");
            }

            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
            return accessToken;
        }

        /// <summary>
        /// Disposes the RSA key material and invalidates the cached token.
        /// After disposal, <see cref="GetAccessTokenAsync"/> throws
        /// <see cref="ObjectDisposedException"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _rsa?.Dispose();
                _cachedToken = null;
            }
        }
    }
}
