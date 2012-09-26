using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using NUnit.Framework;

namespace EventStore.Core.Tests.Integration
{
    public class EnvelopeCallback<T> : IHandle<T>, IDisposable where T : Message
    {
        private readonly ManualResetEvent _event = new ManualResetEvent(false);
        private T _receivedMessage;

        public void Wait()
        {
            Wait(_ => { });
        }

        public void Wait(Action<T> callback)
        {
            Wait(10000, callback);
        }

        public void Wait(int milliseconds, Action<T> callback)
        {
            if (!_event.WaitOne(milliseconds))
                Assert.Fail("Timed out while waiting for {0}", typeof(T).Name);

            callback(_receivedMessage);
        }

        public void Handle(T message)
        {
            _receivedMessage = message;
            _event.Set();
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }
}