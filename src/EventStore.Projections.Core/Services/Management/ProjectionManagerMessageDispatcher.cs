using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management {
	public class ProjectionManagerMessageDispatcher
		: IHandle<ReaderSubscriptionManagement.SpoolStreamReading>,
			IHandle<CoreProjectionManagementControlMessage> {
		private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionManager>();
		private readonly IDictionary<Guid, IPublisher> _queueMap;

		public ProjectionManagerMessageDispatcher(IDictionary<Guid, IPublisher> queueMap) {
			_queueMap = queueMap;
		}

		public void Handle(ReaderSubscriptionManagement.SpoolStreamReading message) {
			DispatchWorkerMessage(
				new ReaderSubscriptionManagement.SpoolStreamReadingCore(
					message.SubscriptionId,
					message.StreamId,
					message.CatalogSequenceNumber,
					message.LimitingCommitPosition),
				message.WorkerId);
		}

		public void Handle(CoreProjectionManagementControlMessage message) {
			DispatchWorkerMessage(message, message.WorkerId);
		}

		private void DispatchWorkerMessage(Message message, Guid workerId) {
			IPublisher worker;
			if (_queueMap.TryGetValue(workerId, out worker))
				worker.Publish(message);
			else
				_logger.Info("Cannot find a worker with ID: {workerId}", workerId);
		}
	}
}
