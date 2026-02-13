using System;
using System.Text;

namespace AIEmbodiment
{
    /// <summary>
    /// Composes PersonaConfig fields into a Gemini system instruction string.
    /// </summary>
    public static class SystemInstructionBuilder
    {
        /// <summary>
        /// Builds a system instruction string from the given persona configuration.
        /// </summary>
        /// <param name="config">The persona configuration to convert.</param>
        /// <returns>The formatted system instruction text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        public static string Build(PersonaConfig config)
        {
            return BuildInstructionText(config);
        }

        /// <summary>
        /// Builds a system instruction string including conversational goals.
        /// </summary>
        /// <param name="config">The persona configuration to convert.</param>
        /// <param name="goalManager">The goal manager containing active conversational goals.</param>
        /// <returns>The formatted system instruction text with goal framing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        public static string Build(PersonaConfig config, GoalManager goalManager)
        {
            return BuildInstructionText(config, goalManager);
        }

        /// <summary>
        /// Builds the raw instruction text string from persona config.
        /// Internal so PersonaSession can access it for mid-session instruction updates.
        /// </summary>
        /// <param name="config">The persona configuration to convert.</param>
        /// <returns>The formatted system instruction text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        internal static string BuildInstructionText(PersonaConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var sb = new StringBuilder();

            sb.AppendLine($"You are {config.displayName}, a {config.archetype}.");

            if (!string.IsNullOrEmpty(config.backstory))
            {
                sb.AppendLine();
                sb.AppendLine("BACKSTORY:");
                sb.AppendLine(config.backstory);
            }

            if (config.personalityTraits != null && config.personalityTraits.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("PERSONALITY TRAITS:");
                foreach (var trait in config.personalityTraits)
                {
                    if (!string.IsNullOrEmpty(trait))
                        sb.AppendLine($"- {trait}");
                }
            }

            if (!string.IsNullOrEmpty(config.speechPatterns))
            {
                sb.AppendLine();
                sb.AppendLine("SPEECH PATTERNS:");
                sb.AppendLine(config.speechPatterns);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds instruction text including conversational goal framing when goals exist.
        /// Internal so PersonaSession can access it for mid-session instruction updates.
        /// </summary>
        /// <param name="config">The persona configuration to convert.</param>
        /// <param name="goalManager">The goal manager containing active conversational goals.</param>
        /// <returns>The formatted system instruction text with goal framing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        internal static string BuildInstructionText(PersonaConfig config, GoalManager goalManager)
        {
            var text = BuildInstructionText(config);
            if (goalManager != null && goalManager.HasGoals)
            {
                text += "\n\n" + goalManager.ComposeGoalInstruction();
            }
            return text;
        }
    }
}
