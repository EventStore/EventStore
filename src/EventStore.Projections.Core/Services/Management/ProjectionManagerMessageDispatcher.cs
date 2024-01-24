using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using ILogger = Serilog.ILogger;

namespace EventStore.Projections.Core.Services.Management;

public class ProjectionManagerMessageDispatcher : IHandle<ICoreProjectionManagementControlMessage> 
{
	private readonly ILogger _logger = Serilog.Log.ForContext<ProjectionManager>();
	private readonly IDictionary<Guid, IPublisher> _queueMap;

	public ProjectionManagerMessageDispatcher(IDictionary<Guid, IPublisher> queueMap) {
		_queueMap = queueMap;
	}

	public void Handle(ICoreProjectionManagementControlMessage message) {
		DispatchWorkerMessage(message, message.WorkerId);
	}

	private void DispatchWorkerMessage(Message message, Guid workerId) {
		IPublisher worker;
		if (_queueMap.TryGetValue(workerId, out worker))
			worker.Publish(message);
		else
			_logger.Information("Cannot find a worker with ID: {workerId}", workerId);
	}
}
