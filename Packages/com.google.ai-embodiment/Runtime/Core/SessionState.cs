namespace AIEmbodiment
{
    /// <summary>
    /// Represents the lifecycle state of a PersonaSession connection.
    /// </summary>
    public enum SessionState
    {
        /// <summary>No active connection.</summary>
        Disconnected,

        /// <summary>Connection is being established.</summary>
        Connecting,

        /// <summary>Session is active and ready for communication.</summary>
        Connected,

        /// <summary>Session is performing async cleanup before reaching Disconnected.</summary>
        Disconnecting,

        /// <summary>An error occurred. Check error events for details.</summary>
        Error
    }
}
