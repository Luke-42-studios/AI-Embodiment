using System;
using System.Collections.Generic;

namespace AIEmbodiment
{
    /// <summary>
    /// Provides typed access to function call arguments from the AI.
    /// Wraps the raw <see cref="IReadOnlyDictionary{TKey, TValue}"/> from
    /// function call arguments with accessor methods that handle
    /// JSON type coercion (e.g., all numbers deserialize as <c>double</c>).
    /// </summary>
    public class FunctionCallContext
    {
        /// <summary>The function name triggered by the AI.</summary>
        public string FunctionName { get; }

        /// <summary>
        /// The function call ID for response correlation.
        /// This must be included in the <c>FunctionResponsePart</c> sent back to the model.
        /// </summary>
        public string CallId { get; }

        /// <summary>
        /// Raw argument dictionary for advanced use cases not covered by the typed accessors.
        /// Values are JSON-deserialized types: <c>string</c>, <c>double</c>, <c>bool</c>,
        /// <c>Dictionary&lt;string, object&gt;</c>, or <c>List&lt;object&gt;</c>.
        /// </summary>
        public IReadOnlyDictionary<string, object> RawArgs { get; }

        /// <summary>
        /// Creates a new <see cref="FunctionCallContext"/>.
        /// </summary>
        /// <param name="functionName">The function name from the AI.</param>
        /// <param name="callId">The function call ID for response correlation.</param>
        /// <param name="args">The raw argument dictionary from <c>FunctionCallPart.Args</c>.</param>
        public FunctionCallContext(string functionName, string callId, IReadOnlyDictionary<string, object> args)
        {
            FunctionName = functionName;
            CallId = callId;
            RawArgs = args;
        }

        /// <summary>
        /// Returns the argument value as a <c>string</c>, or <paramref name="defaultValue"/>
        /// if the key is missing, null, or not convertible.
        /// </summary>
        /// <param name="key">The argument key.</param>
        /// <param name="defaultValue">Value returned when the key is missing or null.</param>
        /// <returns>The string value, or <paramref name="defaultValue"/>.</returns>
        public string GetString(string key, string defaultValue = null)
        {
            if (!RawArgs.TryGetValue(key, out var value)) return defaultValue;
            if (value == null) return defaultValue;
            try
            {
                return value.ToString();
            }
            catch (InvalidCastException)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Returns the argument value as an <c>int</c>, or <paramref name="defaultValue"/>
        /// if the key is missing, null, or not convertible.
        /// JSON deserializes all numbers as <c>double</c>, so this uses
        /// <see cref="Convert.ToInt32(object)"/> for safe coercion.
        /// </summary>
        /// <param name="key">The argument key.</param>
        /// <param name="defaultValue">Value returned when the key is missing or null.</param>
        /// <returns>The integer value, or <paramref name="defaultValue"/>.</returns>
        public int GetInt(string key, int defaultValue = 0)
        {
            if (!RawArgs.TryGetValue(key, out var value)) return defaultValue;
            if (value == null) return defaultValue;
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Returns the argument value as a <c>float</c>, or <paramref name="defaultValue"/>
        /// if the key is missing, null, or not convertible.
        /// JSON deserializes all numbers as <c>double</c>, so this uses
        /// <see cref="Convert.ToSingle(object)"/> for safe coercion.
        /// </summary>
        /// <param name="key">The argument key.</param>
        /// <param name="defaultValue">Value returned when the key is missing or null.</param>
        /// <returns>The float value, or <paramref name="defaultValue"/>.</returns>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (!RawArgs.TryGetValue(key, out var value)) return defaultValue;
            if (value == null) return defaultValue;
            try
            {
                return Convert.ToSingle(value);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Returns the argument value as a <c>bool</c>, or <paramref name="defaultValue"/>
        /// if the key is missing, null, or not convertible.
        /// JSON deserializes booleans directly as <c>bool</c>.
        /// </summary>
        /// <param name="key">The argument key.</param>
        /// <param name="defaultValue">Value returned when the key is missing or null.</param>
        /// <returns>The boolean value, or <paramref name="defaultValue"/>.</returns>
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (!RawArgs.TryGetValue(key, out var value)) return defaultValue;
            if (value == null) return defaultValue;
            try
            {
                return (bool)value;
            }
            catch (InvalidCastException)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Returns the argument value as a nested dictionary, or <c>null</c>
        /// if the key is missing or the value is not a dictionary.
        /// JSON deserializes objects as <c>Dictionary&lt;string, object&gt;</c>.
        /// </summary>
        /// <param name="key">The argument key.</param>
        /// <returns>The nested dictionary, or <c>null</c>.</returns>
        public IReadOnlyDictionary<string, object> GetObject(string key)
        {
            if (!RawArgs.TryGetValue(key, out var value)) return null;
            if (value == null) return null;
            try
            {
                return value as IReadOnlyDictionary<string, object>
                    ?? (value is Dictionary<string, object> dict ? dict : null);
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the argument value as a list, or <c>null</c>
        /// if the key is missing or the value is not a list.
        /// JSON deserializes arrays as <c>List&lt;object&gt;</c>.
        /// </summary>
        /// <param name="key">The argument key.</param>
        /// <returns>The list, or <c>null</c>.</returns>
        public IReadOnlyList<object> GetArray(string key)
        {
            if (!RawArgs.TryGetValue(key, out var value)) return null;
            if (value == null) return null;
            try
            {
                return value as IReadOnlyList<object>
                    ?? (value is List<object> list ? list : null);
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }
    }
}
