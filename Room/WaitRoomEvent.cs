using System.Diagnostics;
using System;

namespace OpenGSServer.Room
{
    public class WaitRoomEvent
    {
        public DateTime? LastLoadingCompletedAtUtc { get; private set; }
        public DateTime? LastLoadingCanceledAtUtc { get; private set; }
        public int CompletedCount { get; private set; }
        public int CanceledCount { get; private set; }

        public void OnLoadingCompleted()
        {
            LastLoadingCompletedAtUtc = DateTime.UtcNow;
            CompletedCount++;
            Debug.WriteLine("[WaitRoomEvent] Loading completed");
        }

        public void OnLoadingCanceled()
        {
            LastLoadingCanceledAtUtc = DateTime.UtcNow;
            CanceledCount++;
            Debug.WriteLine("[WaitRoomEvent] Loading canceled");
        }
    }
}
