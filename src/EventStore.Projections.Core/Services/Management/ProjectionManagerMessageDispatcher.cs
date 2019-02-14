using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services.Management {
	public class ProjectionManagerMessageDispatcher
		: IHandle<PartitionProcessingResultBase>,
			IHandle<ReaderSubscriptionManagement.SpoolStreamReading>,
			IHandle<CoreProjectionManagementControlMessage>,
			IHandle<PartitionProcessingResultOutputBase> {
		private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionManager>();
		private readonly IDictionary<Guid, IPublisher> _queueMap;

		public ProjectionManagerMessageDispatcher(IDictionary<Guid, IPublisher> queueMap) {
			_queueMap = queueMap;
		}

		public void Handle(PartitionProcessingResultBase message) {
			DispatchWorkerMessage(message, message.WorkerId);
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

		public void Handle(PartitionProcessingResultOutputBase message) {
			DispatchWorkerMessage(message.AsInput(), message.WorkerId);
		}
	}
}
