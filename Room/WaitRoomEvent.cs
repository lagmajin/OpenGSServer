using System.Diagnostics;

namespace OpenGSServer.Room
{
    public class WaitRoomEvent
    {
        public void OnLoadingCompleted()
        {
            Debug.WriteLine("[WaitRoomEvent] Loading completed");
        }

        public void OnLoadingCanceled()
        {
            Debug.WriteLine("[WaitRoomEvent] Loading canceled");
        }
    }
}
