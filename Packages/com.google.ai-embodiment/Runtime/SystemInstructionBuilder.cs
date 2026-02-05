using System;
using System.Text;
using Firebase.AI;

namespace AIEmbodiment
{
    /// <summary>
    /// Composes PersonaConfig fields into a Gemini system instruction.
    /// This is the single Firebase-touching boundary in the config layer.
    /// </summary>
    public static class SystemInstructionBuilder
    {
        /// <summary>
        /// Builds a <see cref="ModelContent"/> system instruction from the given persona configuration.
        /// </summary>
        /// <param name="config">The persona configuration to convert.</param>
        /// <returns>A <see cref="ModelContent"/> containing the formatted system instruction text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        public static ModelContent Build(PersonaConfig config)
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

            return ModelContent.Text(sb.ToString());
        }
    }
}
