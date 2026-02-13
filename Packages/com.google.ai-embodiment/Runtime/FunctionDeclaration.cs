using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace AIEmbodiment
{
    /// <summary>
    /// Typed builder for Gemini function declarations with flat-primitive parameters.
    /// Supports both native JSON tool output (for WebSocket setup handshake) and
    /// human-readable prompt text (for prompt-based function calling fallback).
    /// </summary>
    /// <remarks>
    /// Parameter types are limited to flat primitives: string, int, float, bool, and enum.
    /// No nested objects are supported. Build declarations before registering them with
    /// <see cref="FunctionRegistry"/> and calling <c>PersonaSession.Connect()</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var decl = new FunctionDeclaration("play_emote", "Play a character animation")
    ///     .AddEnum("emote_name", "Animation to play", new[] { "wave", "bow", "laugh" });
    /// </code>
    /// </example>
    public class FunctionDeclaration
    {
        /// <summary>The function name used for registration and tool call matching.</summary>
        public string Name { get; }

        /// <summary>Human-readable description of what the function does.</summary>
        public string Description { get; }

        private readonly List<ParameterDef> _parameters = new List<ParameterDef>();

        /// <summary>
        /// Creates a new function declaration with the given name and description.
        /// </summary>
        /// <param name="name">The function name (must match the name used in registration).</param>
        /// <param name="description">Human-readable description for the AI model.</param>
        public FunctionDeclaration(string name, string description)
        {
            Name = name;
            Description = description;
        }

        /// <summary>Add a string parameter.</summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description for the AI model.</param>
        /// <param name="required">Whether this parameter is required (default: true).</param>
        /// <returns>This declaration for fluent chaining.</returns>
        public FunctionDeclaration AddString(string name, string description, bool required = true)
        {
            _parameters.Add(new ParameterDef(name, description, "STRING", required, null));
            return this;
        }

        /// <summary>Add an integer parameter.</summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description for the AI model.</param>
        /// <param name="required">Whether this parameter is required (default: true).</param>
        /// <returns>This declaration for fluent chaining.</returns>
        public FunctionDeclaration AddInt(string name, string description, bool required = true)
        {
            _parameters.Add(new ParameterDef(name, description, "INTEGER", required, null));
            return this;
        }

        /// <summary>Add a float/number parameter.</summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description for the AI model.</param>
        /// <param name="required">Whether this parameter is required (default: true).</param>
        /// <returns>This declaration for fluent chaining.</returns>
        public FunctionDeclaration AddFloat(string name, string description, bool required = true)
        {
            _parameters.Add(new ParameterDef(name, description, "NUMBER", required, null));
            return this;
        }

        /// <summary>Add a boolean parameter.</summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description for the AI model.</param>
        /// <param name="required">Whether this parameter is required (default: true).</param>
        /// <returns>This declaration for fluent chaining.</returns>
        public FunctionDeclaration AddBool(string name, string description, bool required = true)
        {
            _parameters.Add(new ParameterDef(name, description, "BOOLEAN", required, null));
            return this;
        }

        /// <summary>Add an enum parameter with a fixed set of allowed values.</summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description for the AI model.</param>
        /// <param name="values">The allowed enum values.</param>
        /// <param name="required">Whether this parameter is required (default: true).</param>
        /// <returns>This declaration for fluent chaining.</returns>
        public FunctionDeclaration AddEnum(string name, string description, string[] values, bool required = true)
        {
            _parameters.Add(new ParameterDef(name, description, "STRING", required, values));
            return this;
        }

        /// <summary>
        /// Builds the Gemini API JSON for native tool declaration.
        /// Produces the format expected inside <c>tools[].functionDeclarations[]</c>.
        /// </summary>
        /// <returns>
        /// A <see cref="JObject"/> with name, description, and optional parameters object.
        /// If no parameters are defined, the <c>parameters</c> key is omitted.
        /// </returns>
        public JObject ToToolJson()
        {
            var obj = new JObject
            {
                ["name"] = Name,
                ["description"] = Description
            };

            if (_parameters.Count > 0)
            {
                var properties = new JObject();
                var required = new JArray();

                foreach (var p in _parameters)
                {
                    var prop = new JObject
                    {
                        ["type"] = p.Type,
                        ["description"] = p.Description
                    };

                    if (p.EnumValues != null)
                    {
                        prop["enum"] = new JArray(p.EnumValues);
                    }

                    properties[p.Name] = prop;

                    if (p.Required)
                    {
                        required.Add(p.Name);
                    }
                }

                var parameters = new JObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = properties
                };

                if (required.Count > 0)
                {
                    parameters["required"] = required;
                }

                obj["parameters"] = parameters;
            }

            return obj;
        }

        /// <summary>
        /// Builds human-readable prompt text for prompt-based function calling fallback.
        /// Format: <c>- name(param1: type, param2: type [val1|val2]) - description</c>
        /// </summary>
        /// <returns>A single-line text description of this function and its parameters.</returns>
        public string ToPromptText()
        {
            var sb = new StringBuilder();
            sb.Append("- ");
            sb.Append(Name);
            sb.Append("(");

            for (int i = 0; i < _parameters.Count; i++)
            {
                if (i > 0) sb.Append(", ");

                var p = _parameters[i];
                sb.Append(p.Name);
                sb.Append(": ");
                sb.Append(PromptTypeName(p.Type));

                if (p.EnumValues != null)
                {
                    sb.Append(" [");
                    sb.Append(string.Join("|", p.EnumValues));
                    sb.Append("]");
                }
            }

            sb.Append(") - ");
            sb.Append(Description);

            return sb.ToString();
        }

        private static string PromptTypeName(string apiType)
        {
            switch (apiType)
            {
                case "STRING": return "string";
                case "INTEGER": return "int";
                case "NUMBER": return "float";
                case "BOOLEAN": return "bool";
                default: return apiType.ToLowerInvariant();
            }
        }

        /// <summary>Internal parameter definition.</summary>
        private class ParameterDef
        {
            public readonly string Name;
            public readonly string Description;
            public readonly string Type;
            public readonly bool Required;
            public readonly string[] EnumValues;

            public ParameterDef(string name, string description, string type, bool required, string[] enumValues)
            {
                Name = name;
                Description = description;
                Type = type;
                Required = required;
                EnumValues = enumValues;
            }
        }
    }
}
