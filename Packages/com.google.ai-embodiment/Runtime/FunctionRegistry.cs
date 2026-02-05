using System;
using System.Collections.Generic;
using System.Linq;
using Firebase.AI;

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
    /// Maps function names to <see cref="FunctionDeclaration"/> and <see cref="FunctionHandler"/>
    /// pairs. Registered before <c>PersonaSession.Connect()</c> and frozen at connect time.
    /// Produces a <see cref="Tool"/> array for the Firebase <c>GetLiveModel</c> call.
    /// Also tracks pending function call cancellations to avoid sending stale responses.
    /// </summary>
    public class FunctionRegistry
    {
        private readonly Dictionary<string, (FunctionDeclaration declaration, FunctionHandler handler)> _entries
            = new Dictionary<string, (FunctionDeclaration, FunctionHandler)>();

        private readonly HashSet<string> _cancelledIds = new HashSet<string>();

        private bool _frozen;

        /// <summary>
        /// Returns <c>true</c> if any functions have been registered.
        /// </summary>
        public bool HasRegistrations => _entries.Count > 0;

        /// <summary>
        /// Registers a function declaration and its handler.
        /// The name must match the <see cref="FunctionDeclaration"/>'s constructor name.
        /// Must be called before <c>PersonaSession.Connect()</c>; the registry is
        /// frozen after <see cref="BuildTools"/> is called.
        /// </summary>
        /// <param name="name">
        /// The function name used for lookup when the AI triggers a function call.
        /// </param>
        /// <param name="declaration">
        /// The Firebase <see cref="FunctionDeclaration"/> describing the function schema.
        /// </param>
        /// <param name="handler">
        /// The delegate invoked when the AI calls this function.
        /// </param>
        /// <remarks>
        /// The <paramref name="name"/> parameter MUST match the name passed to the
        /// <see cref="FunctionDeclaration"/> constructor. The SDK cannot validate this
        /// because FunctionDeclaration.Name is not publicly accessible. A mismatch will
        /// cause the AI to trigger a function name that does not resolve to the intended handler.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the registry is frozen (after <see cref="BuildTools"/> has been called).
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="name"/> is null or empty.
        /// </exception>
        public void Register(string name, FunctionDeclaration declaration, FunctionHandler handler)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    "FunctionRegistry: Cannot register functions after BuildTools() has been called. " +
                    "Register all functions before calling Connect().");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Function name must not be null or empty.", nameof(name));
            }

            _entries[name] = (declaration, handler);
        }

        /// <summary>
        /// Freezes the registry and builds a <see cref="Tool"/> array from all registered
        /// <see cref="FunctionDeclaration"/> instances. After this call, no further
        /// registrations are allowed.
        /// </summary>
        /// <returns>
        /// A single-element <see cref="Tool"/> array containing all declarations,
        /// or <c>null</c> if no functions are registered.
        /// </returns>
        public Tool[] BuildTools()
        {
            _frozen = true;

            if (_entries.Count == 0)
            {
                return null;
            }

            var declarations = _entries.Values.Select(e => e.declaration).ToArray();
            return new Tool[] { new Tool(declarations) };
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
                handler = entry.handler;
                return true;
            }

            handler = null;
            return false;
        }

        /// <summary>
        /// Marks a function call ID as cancelled. Called when the model sends a
        /// <c>LiveSessionToolCallCancellation</c> (e.g., due to user interruption).
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
