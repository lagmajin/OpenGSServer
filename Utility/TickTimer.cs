using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace OpenGSServer
{

    public class TickTimer
    {



        TimerCallback _cb;
        Stopwatch _sw;
        int _dueTime;
        int _period;
        int _loop = 1;
        int _started;
        readonly Thread _task;

        public int IntervalMs => _period;

        public TickTimer(TimerCallback callback, object state, int dueTime, int period)
        {
            _cb = callback;
            _dueTime = dueTime;
            _period = period;
            _sw = new Stopwatch();
            _task = new Thread(OnTimer);
            Start(state);
        }
        public TickTimer(TimerCallback callback, int period)
        {
            _cb = callback;
            _period = period;
            _sw = new Stopwatch();
            _task = new Thread(OnTimer);
        }
        public void Start(object state = null)
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                throw new InvalidOperationException("A TickTimer instance can only be started once.");
            }

            Volatile.Write(ref _loop, 1);
            _dueTime = 0;
            _task.Start(state);
        }

        public void Stop()
        {
            Volatile.Write(ref _loop, 0);
            if (_task.IsAlive && Thread.CurrentThread != _task)
            {
                _task.Join();
            }
        }
        private void OnTimer(object state)
        {
            Thread.Sleep(_dueTime);
            
            //await Task.Delay(_dueTime);

            //Thread.Yield();
            _sw.Restart();
            while (Volatile.Read(ref _loop) != 0)
            {
                long msec = _sw.ElapsedMilliseconds;
                int rest = _period - (int)(msec % _period);
                // 200msecだけ余らせてスリープ
                if (rest > 200)
                {
                    Thread.Sleep(rest - 10);
                    
                    //await Task.Delay(rest - 10);
                    //Thread.Yield();
                }
                // 200msecの間、ちょうどになるまでループで待つ
                while (true)
                {
                    if (Volatile.Read(ref _loop) == 0 || _sw.ElapsedMilliseconds >= msec + rest)
                    {
                        break;
                    }
                }
                if (Volatile.Read(ref _loop) == 0) break;
                if (_cb != null)
                {
                    _cb(state);
                }
            }
            _sw.Stop();
        }
    }


}
