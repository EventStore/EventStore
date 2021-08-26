using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {	
	public class QueuedHandler :
		QueuedHandlerChannel,
		IQueuedHandler {
		public static IQueuedHandler CreateQueuedHandler(
			IHandle<Message> consumer, 
			string name,
			QueueStatsManager queueStatsManager,
			bool watchSlowMsg = true,
			TimeSpan? slowMsgThreshold = null, 
			TimeSpan? stopWaitTimeout = null, 
			string groupName = null) {		
			
			return new QueuedHandlerChannel(consumer, name, queueStatsManager, watchSlowMsg, slowMsgThreshold, stopWaitTimeout, groupName);
		}

		public static readonly TimeSpan DefaultStopWaitTimeout = TimeSpan.FromSeconds(10);
		public static readonly TimeSpan VerySlowMsgThreshold = TimeSpan.FromSeconds(7);

		QueuedHandler(IHandle<Message> consumer,
			string name,
			QueueStatsManager queueStatsManager,
			bool watchSlowMsg = true,
			TimeSpan? slowMsgThreshold = null,
			TimeSpan? threadStopWaitTimeout = null,
			string groupName = null)
			: base(
				consumer, name, queueStatsManager, watchSlowMsg, slowMsgThreshold, threadStopWaitTimeout ?? DefaultStopWaitTimeout,
				groupName) {
		}		
	}
}
