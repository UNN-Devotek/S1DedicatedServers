namespace DedicatedServerMod.Shared.Networking
{
    /// <summary>
    /// Carries a transient server announcement to connected clients.
    /// </summary>
    internal sealed class ServerAnnouncementMessage
    {
        internal const int MaxMessageLength = 240;

        public string Message { get; set; }

        internal static bool IsValid(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                message.Length <= MaxMessageLength &&
                !message.Any(char.IsControl);
        }
    }
}
