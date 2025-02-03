// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus.Helpers;

public abstract class QueuedHandlerTestWithNoopConsumer {
	private readonly Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> _queuedHandlerFactory;

	protected IQueuedHandler Queue;
	protected IHandle<Message> Consumer;

	protected QueuedHandlerTestWithNoopConsumer(
		Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory) {
		Ensure.NotNull(queuedHandlerFactory, "queuedHandlerFactory");
		_queuedHandlerFactory = queuedHandlerFactory;
	}

	[SetUp]
	public virtual void SetUp() {
		Consumer = new NoopConsumer();
		Queue = _queuedHandlerFactory(Consumer, "test_name", TimeSpan.FromMilliseconds(5000));
	}

	[TearDown]
	public virtual async Task TearDown() {
		await Queue.Stop();
		Queue = null;
		Consumer = null;
	}
}
