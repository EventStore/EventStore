// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Bus {
	public interface IMonitoredQueue {
		//NOTE: This interface provides direct access to a queue internals breaking encapsulation of these objects.  
		//      This is implemented this way to minimize impact on performance and to allow monitor detect problems

		//      The monitored queue can be represented as IHandle<PollQueueStatistics> unless this impl can interfere 
		//      with queue message handling itself
		string Name { get; }
		QueueStats GetStatistics();
	}
}
