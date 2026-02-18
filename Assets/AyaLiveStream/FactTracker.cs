using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Shared fact dictionary for cross-system coherence during the livestream experience.
    /// Stores string-keyed boolean facts that subsystems can query to avoid contradictions
    /// (e.g., bots won't ask about a topic Aya has already covered).
    ///
    /// Plain C# class (NOT MonoBehaviour) -- created in LivestreamController.Start() and
    /// passed to subsystems that need it. Target 5-8 facts for the whole experience
    /// (Pitfall 7 from research: avoid scope creep).
    /// </summary>
    public class FactTracker
    {
        private readonly Dictionary<string, bool> _facts = new();

        /// <summary>
        /// Records a fact as established or clears it.
        /// Logs on change for debugging narrative flow.
        /// </summary>
        public void SetFact(string factId, bool value = true)
        {
            bool changed = !_facts.TryGetValue(factId, out bool existing) || existing != value;
            _facts[factId] = value;
            if (changed)
            {
                Debug.Log($"[FactTracker] {factId} = {value}");
            }
        }

        /// <summary>
        /// Checks if a fact has been established. Returns false if the fact
        /// has never been set or was explicitly set to false.
        /// </summary>
        public bool HasFact(string factId)
        {
            return _facts.TryGetValue(factId, out bool value) && value;
        }

        /// <summary>
        /// Returns a summary of all true facts formatted for prompt injection.
        /// Each fact appears as "- factId" on its own line.
        /// Returns empty string if no facts are established.
        /// </summary>
        public string GetFactsSummary()
        {
            var sb = new StringBuilder();
            foreach (var kvp in _facts)
            {
                if (kvp.Value)
                    sb.AppendLine($"- {kvp.Key}");
            }
            return sb.ToString();
        }
    }
}
