using System;
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Core.Util
{
    public class PlatformEvent
    {
        private readonly AutoResetEvent _monoEvent;
        private readonly ManualResetEventSlim _windowsEvent;

        public PlatformEvent()
        {
            if (Runtime.IsWindows)
            {
                _windowsEvent = new ManualResetEventSlim();
            }
            else
            {
                _monoEvent = new AutoResetEvent(false);
            }
        }

        public bool Wait(TimeSpan timeout)
        {
            return _windowsEvent?.Wait(timeout) ?? _monoEvent.WaitOne(timeout);
        }

        public void Set()
        {
            if (_windowsEvent != null)
            {
                _windowsEvent.Set();
                return;
            }

            _monoEvent.Set();
        }

        public void Reset()
        {
            if (_windowsEvent != null)
            {
                _windowsEvent.Reset();
                return;
            }

            _monoEvent.Reset();
        }
    }
}