

using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class AbstractServer
    {
        protected int max = 2;
        private int port = 45454;

        private string ip = "127.0.0.1";
        DateTime serverStartTime=DateTime.Now;
        private CancellationTokenSource? updateCts;

        protected int Port { get => port; set => port = value; }
        protected string Ip { get => ip; set => ip = value; }

        public AbstractServer(bool autoUpdate=false)
        {
           
            if (autoUpdate)
            {
                UpdateStart();
            }
        }

        private void UpdateStart()
        {
            updateCts = new CancellationTokenSource();
            var token = updateCts.Token;

            var _ = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    Update();
                    Thread.Sleep(10);
                }
            }, token);
        }

        protected  virtual void Update()
        {


            Console.WriteLine("ll");
        }

        public virtual void Listen()
        {

        }
        public virtual void Shutdown()
        {
            updateCts?.Cancel();
        }
        public static string ServerTimezoneName()
        {
            System.TimeZoneInfo tzi = System.TimeZoneInfo.Local;

            return tzi.StandardName;
        }
        public static DateTime ServerTime()
        {


            return DateTime.Now;
        }

        public DateTime ServerStartTime()
        {
            return serverStartTime;
        }

        
    }
}
