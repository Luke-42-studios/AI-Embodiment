using System;
using System.Collections.Generic;
using System.Text;

namespace AIEmbodiment
{
    /// <summary>
    /// Manages active conversational goals and composes urgency-framed instruction text
    /// for injection into the AI's system instruction. Plain C# class with no Unity
    /// dependencies.
    /// </summary>
    public class GoalManager
    {
        private readonly List<ConversationalGoal> _goals = new List<ConversationalGoal>();

        /// <summary>
        /// Returns true if any goals are currently active.
        /// </summary>
        public bool HasGoals => _goals.Count > 0;

        /// <summary>
        /// Adds a goal to the active goals list.
        /// </summary>
        /// <param name="goal">The goal to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="goal"/> is null.</exception>
        public void AddGoal(ConversationalGoal goal)
        {
            if (goal == null)
                throw new ArgumentNullException(nameof(goal));

            _goals.Add(goal);
        }

        /// <summary>
        /// Removes a goal by its identifier.
        /// </summary>
        /// <param name="goalId">The identifier of the goal to remove.</param>
        /// <returns>True if the goal was found and removed; false otherwise.</returns>
        public bool RemoveGoal(string goalId)
        {
            for (int i = 0; i < _goals.Count; i++)
            {
                if (_goals[i].Id == goalId)
                {
                    _goals.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves a goal by its identifier for inspection or reprioritization.
        /// </summary>
        /// <param name="goalId">The identifier of the goal to find.</param>
        /// <returns>The goal if found; null otherwise.</returns>
        public ConversationalGoal GetGoal(string goalId)
        {
            for (int i = 0; i < _goals.Count; i++)
            {
                if (_goals[i].Id == goalId)
                    return _goals[i];
            }

            return null;
        }

        /// <summary>
        /// Returns a read-only view of all active goals.
        /// </summary>
        /// <returns>A read-only list of active goals.</returns>
        public IReadOnlyList<ConversationalGoal> GetActiveGoals()
        {
            return _goals.AsReadOnly();
        }

        /// <summary>
        /// Composes a structured instruction text block from all active goals,
        /// ordered by priority descending (High first, then Medium, then Low).
        /// Each goal is framed with urgency text appropriate to its priority level.
        /// </summary>
        /// <returns>
        /// The composed goal instruction text, or an empty string if no goals are active.
        /// </returns>
        public string ComposeGoalInstruction()
        {
            if (_goals.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("CONVERSATIONAL GOALS:");

            // Emit goals ordered by priority descending: High, Medium, Low
            AppendGoalsForPriority(sb, GoalPriority.High);
            AppendGoalsForPriority(sb, GoalPriority.Medium);
            AppendGoalsForPriority(sb, GoalPriority.Low);

            return sb.ToString();
        }

        private void AppendGoalsForPriority(StringBuilder sb, GoalPriority priority)
        {
            for (int i = 0; i < _goals.Count; i++)
            {
                if (_goals[i].Priority != priority)
                    continue;

                sb.AppendLine();

                switch (priority)
                {
                    case GoalPriority.High:
                        sb.AppendLine("[HIGH PRIORITY - Act on this urgently]");
                        sb.AppendLine($"Goal: {_goals[i].Description}");
                        sb.AppendLine("You should actively steer the conversation toward this goal. Bring it up naturally but persistently. This is your top priority right now.");
                        break;

                    case GoalPriority.Medium:
                        sb.AppendLine("[MEDIUM PRIORITY - Work toward this when natural]");
                        sb.AppendLine($"Goal: {_goals[i].Description}");
                        sb.AppendLine("Look for natural openings to bring this up. Don't force it, but don't forget it either.");
                        break;

                    case GoalPriority.Low:
                        sb.AppendLine("[LOW PRIORITY - Keep in mind]");
                        sb.AppendLine($"Goal: {_goals[i].Description}");
                        sb.AppendLine("If the opportunity arises naturally, try to learn this. No need to push for it.");
                        break;
                }
            }
        }
    }
}
