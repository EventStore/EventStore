// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Subscriptions {
	public interface IReaderSubscription : IHandle<ReaderSubscriptionMessage.CommittedEventDistributed>,
		IHandle<ReaderSubscriptionMessage.EventReaderIdle>,
		IHandle<ReaderSubscriptionMessage.EventReaderStarting>,
		IHandle<ReaderSubscriptionMessage.EventReaderEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionDeleted>,
		IHandle<ReaderSubscriptionMessage.EventReaderNotAuthorized>,
		IHandle<ReaderSubscriptionMessage.ReportProgress> {
		string Tag { get; }
		Guid SubscriptionId { get; }
		IEventReader CreatePausedEventReader(IPublisher publisher, IODispatcher ioDispatcher, Guid forkedEventReaderId);
	}
}
