// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Services.Monitoring;

public class StatsCollectorEnvelope : IEnvelope {
	private readonly StatsContainer _statsContainer;

	public StatsCollectorEnvelope(StatsContainer statsContainer) {
		_statsContainer = statsContainer;
		Ensure.NotNull(statsContainer, "statsContainer");
	}

	public void ReplyWith<T>(T message) where T : Message {
		var msg = message as MonitoringMessage.InternalStatsRequestResponse;
		if (msg != null)
			_statsContainer.Add(msg.Stats);
	}
}
