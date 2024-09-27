// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

namespace EventStore.Projections.Core.Services.Processing.Strategies {
	public interface IResultWriter {
		//NOTE: subscriptionId should not be here.  Reconsider how to pass it to follower projection result writer
		void WriteEofResult(
			Guid subscriptionId, string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid,
			string correlationId);

		void WriteRunningResult(EventProcessedResult result);

		void AccountPartition(EventProcessedResult result);

		void EventsEmitted(EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId);

		void WriteProgress(Guid subscriptionId, float progress);
	}
}
