using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace AIEmbodiment
{
    /// <summary>
    /// Handler delegate invoked when the AI triggers a registered function call.
    /// Return a non-null dictionary to send the result back to the model automatically.
    /// Return <c>null</c> for fire-and-forget functions (no response sent).
    /// </summary>
    /// <param name="context">Typed accessor for function call arguments and metadata.</param>
    /// <returns>
    /// A response dictionary sent back to the model, or <c>null</c> for fire-and-forget.
    /// </returns>
    public delegate IDictionary<string, object> FunctionHandler(FunctionCallContext context);

    /// <summary>
    /// Maps function names to <see cref="FunctionDeclaration"/> and <see cref="FunctionHandler"/> pairs.
    /// Registered before <c>PersonaSession.Connect()</c> and frozen at connect time.
    /// Also tracks pending function call cancellations to avoid sending stale responses.
    /// Provides dual-path output: native JSON for WebSocket setup handshake and
    /// prompt text for prompt-based function calling fallback.
    /// </summary>
    public class FunctionRegistry
    {
        private readonly Dictionary<string, RegistryEntry> _entries
            = new Dictionary<string, RegistryEntry>();

        private readonly HashSet<string> _cancelledIds = new HashSet<string>();

        private bool _frozen;

        /// <summary>
        /// Returns <c>true</c> if any functions have been registered.
        /// </summary>
        public bool HasRegistrations => _entries.Count > 0;

        /// <summary>
        /// Registers a function with its typed declaration and handler.
        /// Must be called before <c>PersonaSession.Connect()</c>; the registry is
        /// frozen at connect time.
        /// </summary>
        /// <param name="name">
        /// The function name used for lookup when the AI triggers a function call.
        /// </param>
        /// <param name="declaration">
        /// The typed function declaration describing parameters and schema.
        /// </param>
        /// <param name="handler">
        /// The delegate invoked when the AI calls this function.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the registry is frozen (after connect time).
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="name"/> is null or empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="declaration"/> is null.
        /// </exception>
        public void Register(string name, FunctionDeclaration declaration, FunctionHandler handler)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    "FunctionRegistry: Cannot register functions after session has connected. " +
                    "Register all functions before calling Connect().");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Function name must not be null or empty.", nameof(name));
            }

            if (declaration == null)
            {
                throw new ArgumentNullException(nameof(declaration));
            }

            _entries[name] = new RegistryEntry(handler, declaration);
        }

        /// <summary>
        /// Builds the Gemini API <c>tools</c> JSON array for the WebSocket setup handshake.
        /// Format: <c>[{"functionDeclarations": [...]}]</c>
        /// </summary>
        /// <returns>
        /// A <see cref="JArray"/> containing tool declarations, or <c>null</c> if no
        /// functions are registered.
        /// </returns>
        public JArray BuildToolsJson()
        {
            if (_entries.Count == 0) return null;

            var declarations = new JArray();
            foreach (var entry in _entries)
            {
                declarations.Add(entry.Value.Declaration.ToToolJson());
            }

            return new JArray
            {
                new JObject
                {
                    ["functionDeclarations"] = declarations
                }
            };
        }

        /// <summary>
        /// Builds the prompt-based function calling instructions for system prompt injection.
        /// Used as a fallback when native tool calling is not available (e.g., audio-native models).
        /// </summary>
        /// <returns>
        /// Formatted prompt text with available functions, or <c>null</c> if no
        /// functions are registered.
        /// </returns>
        public string BuildPromptInstructions()
        {
            if (_entries.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("AVAILABLE FUNCTIONS:");
            sb.AppendLine("When you want to call a function, output EXACTLY this format:");
            sb.AppendLine("[CALL: function_name {\"param\": \"value\"}]");
            sb.AppendLine();
            sb.AppendLine("Functions:");
            foreach (var entry in _entries)
            {
                sb.AppendLine(entry.Value.Declaration.ToPromptText());
            }
            sb.AppendLine();
            sb.AppendLine("IMPORTANT: Output the [CALL: ...] tag exactly. Do not narrate it.");

            return sb.ToString();
        }

        /// <summary>
        /// Freezes the registry so no further registrations are allowed.
        /// Called at connect time.
        /// </summary>
        public void Freeze()
        {
            _frozen = true;
        }

        /// <summary>
        /// Looks up a handler by function name.
        /// </summary>
        /// <param name="functionName">The function name from the AI's function call.</param>
        /// <param name="handler">The handler delegate if found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a handler was found; otherwise <c>false</c>.</returns>
        public bool TryGetHandler(string functionName, out FunctionHandler handler)
        {
            if (_entries.TryGetValue(functionName, out var entry))
            {
                handler = entry.Handler;
                return true;
            }

            handler = null;
            return false;
        }

        /// <summary>
        /// Marks a function call ID as cancelled. Called when the model sends a
        /// tool call cancellation (e.g., due to user interruption).
        /// </summary>
        /// <param name="callId">The function call ID to cancel.</param>
        public void MarkCancelled(string callId)
        {
            _cancelledIds.Add(callId);
        }

        /// <summary>
        /// Checks whether a function call ID has been cancelled and clears the cancellation.
        /// This is a one-shot check: returns <c>true</c> once, then the ID is removed.
        /// Call this before sending a function response to avoid sending stale results.
        /// </summary>
        /// <param name="callId">The function call ID to check.</param>
        /// <returns><c>true</c> if the call was cancelled; otherwise <c>false</c>.</returns>
        public bool IsCancelled(string callId)
        {
            return _cancelledIds.Remove(callId);
        }

        /// <summary>Pairs a handler with its typed declaration.</summary>
        private class RegistryEntry
        {
            public readonly FunctionHandler Handler;
            public readonly FunctionDeclaration Declaration;

            public RegistryEntry(FunctionHandler handler, FunctionDeclaration declaration)
            {
                Handler = handler;
                Declaration = declaration;
            }
        }
    }
}
