namespace AIEmbodiment
{
    /// <summary>
    /// Defines the urgency level for a conversational goal.
    /// Higher priority goals receive stronger urgency framing in the system instruction.
    /// </summary>
    public enum GoalPriority
    {
        /// <summary>Mention if the opportunity arises naturally. No need to push.</summary>
        Low,

        /// <summary>Work toward this when natural openings appear.</summary>
        Medium,

        /// <summary>Actively steer the conversation toward this goal.</summary>
        High
    }
}
