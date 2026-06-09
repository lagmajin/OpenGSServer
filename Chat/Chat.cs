using System;

namespace OpenGSServer
{
    public sealed class Chat
    {
        public string SenderId { get; }
        public string SenderName { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public Chat(string senderId, string senderName, string message)
        {
            SenderId = senderId ?? string.Empty;
            SenderName = senderName ?? string.Empty;
            Message = message ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }
    }
}
