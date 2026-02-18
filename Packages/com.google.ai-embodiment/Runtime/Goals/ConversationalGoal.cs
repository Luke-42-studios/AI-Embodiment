using System;

namespace AIEmbodiment
{
    /// <summary>
    /// Represents a conversational goal that steers the AI persona's behavior.
    /// Goals have an immutable identity and description, with a mutable priority
    /// that can be changed at runtime as game state evolves.
    /// </summary>
    public class ConversationalGoal
    {
        /// <summary>
        /// Unique identifier for this goal. Immutable after construction.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Natural language description of what the AI should try to accomplish.
        /// Immutable after construction.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Urgency level for this goal. Mutable -- can be changed at runtime
        /// to reflect evolving game state.
        /// </summary>
        public GoalPriority Priority { get; set; }

        /// <summary>
        /// Creates a new conversational goal.
        /// </summary>
        /// <param name="id">Unique identifier for this goal. Must not be null or empty.</param>
        /// <param name="description">Natural language description of the goal. Must not be null or empty.</param>
        /// <param name="priority">Initial urgency level for this goal.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="description"/> is null or empty.</exception>
        public ConversationalGoal(string id, string description, GoalPriority priority)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Goal id must not be null or empty.", nameof(id));
            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("Goal description must not be null or empty.", nameof(description));

            Id = id;
            Description = description;
            Priority = priority;
        }
    }
}
