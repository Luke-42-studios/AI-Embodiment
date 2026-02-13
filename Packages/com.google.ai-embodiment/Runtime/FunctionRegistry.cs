using System;
using System.Collections.Generic;

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
    /// Maps function names to <see cref="FunctionHandler"/> delegates.
    /// Registered before <c>PersonaSession.Connect()</c> and frozen at connect time.
    /// Also tracks pending function call cancellations to avoid sending stale responses.
    /// </summary>
    public class FunctionRegistry
    {
        private readonly Dictionary<string, FunctionHandler> _entries
            = new Dictionary<string, FunctionHandler>();

        private readonly HashSet<string> _cancelledIds = new HashSet<string>();

        private bool _frozen;

        /// <summary>
        /// Returns <c>true</c> if any functions have been registered.
        /// </summary>
        public bool HasRegistrations => _entries.Count > 0;

        /// <summary>
        /// Registers a function handler by name.
        /// Must be called before <c>PersonaSession.Connect()</c>; the registry is
        /// frozen at connect time.
        /// </summary>
        /// <param name="name">
        /// The function name used for lookup when the AI triggers a function call.
        /// </param>
        /// <param name="handler">
        /// The delegate invoked when the AI calls this function.
        /// </param>
        // TODO: Phase 10 -- add function declaration parameter back with WebSocket-native type (JObject schema)
        /// <exception cref="InvalidOperationException">
        /// Thrown if the registry is frozen (after connect time).
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="name"/> is null or empty.
        /// </exception>
        public void Register(string name, FunctionHandler handler)
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

            _entries[name] = handler;
        }

        // TODO: Phase 10 -- BuildToolsJson() returns JArray of tool declarations for WebSocket setup

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
            return _entries.TryGetValue(functionName, out handler);
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
    }
}
