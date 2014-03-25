using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus
{
    // on Windows AutoReset version is much slower, but on Linux ManualResetEventSlim version is much slower
    public class QueuedHandler: 
#if __MonoCS__
        QueuedHandlerAutoReset,
#else
        QueuedHandlerMRES,
#endif
        IQueuedHandler
    {
        public static readonly TimeSpan DefaultStopWaitTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan VerySlowMsgThreshold = TimeSpan.FromSeconds(7);

        public QueuedHandler(IHandle<Message> consumer,
                             string name,
                             bool watchSlowMsg = true,
                             TimeSpan? slowMsgThreshold = null,
                             TimeSpan? threadStopWaitTimeout = null,
                             string groupName = null)
                : base(consumer, name, watchSlowMsg, slowMsgThreshold, threadStopWaitTimeout ?? DefaultStopWaitTimeout, groupName)
        {
        }
    }
}
