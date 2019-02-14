using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	// on Windows AutoReset version is much slower, but on Linux ManualResetEventSlim version is much slower
	public class QueuedHandler :
		QueuedHandlerMRES,
		IQueuedHandler {
		public static IQueuedHandler CreateQueuedHandler(IHandle<Message> consumer, string name,
			bool watchSlowMsg = true,
			TimeSpan? slowMsgThreshold = null, TimeSpan? threadStopWaitTimeout = null, string groupName = null) {
			//if (IntPtr.Size == 8)
			//{
			//    // optimized path, using much faster multi producer single consumer queue
			//    return new QueueHandlerUsingMpsc(consumer, name, watchSlowMsg, slowMsgThreshold, threadStopWaitTimeout,
			//        groupName);
			//}
			return new QueuedHandler(consumer, name, watchSlowMsg, slowMsgThreshold, threadStopWaitTimeout, groupName);
		}

		public static readonly TimeSpan DefaultStopWaitTimeout = TimeSpan.FromSeconds(10);
		public static readonly TimeSpan VerySlowMsgThreshold = TimeSpan.FromSeconds(7);

		QueuedHandler(IHandle<Message> consumer,
			string name,
			bool watchSlowMsg = true,
			TimeSpan? slowMsgThreshold = null,
			TimeSpan? threadStopWaitTimeout = null,
			string groupName = null)
			: base(
				consumer, name, watchSlowMsg, slowMsgThreshold, threadStopWaitTimeout ?? DefaultStopWaitTimeout,
				groupName) {
		}

		class QueueHandlerUsingMpsc :
			QueuedHandlerMresWithMpsc,
			IQueuedHandler {
			public QueueHandlerUsingMpsc(IHandle<Message> consumer,
				string name,
				bool watchSlowMsg = true,
				TimeSpan? slowMsgThreshold = null,
				TimeSpan? threadStopWaitTimeout = null,
				string groupName = null)
				: base(
					consumer, name, watchSlowMsg, slowMsgThreshold, threadStopWaitTimeout ?? DefaultStopWaitTimeout,
					groupName) {
			}
		}
	}
}
