// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus;

[TestFixture]
public abstract class queued_handler_should : QueuedHandlerTestWithNoopConsumer {
	protected queued_handler_should(Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory)
		: base(queuedHandlerFactory) {
	}

	[Test]
	public void throw_if_handler_is_null() {
		Assert.Throws<ArgumentNullException>(
			static () => new QueuedHandlerThreadPool(null, "throwing", new(), new(), watchSlowMsg: false));
	}

	[Test]
	public void throw_if_name_is_null() {
		Assert.Throws<ArgumentNullException>(
			() => new QueuedHandlerThreadPool(Consumer, null, new(), new(), watchSlowMsg: false));
	}
}

[TestFixture]
public class queued_handler_threadpool_should : queued_handler_should {
	public queued_handler_threadpool_should()
		: base((consumer, name, timeout) =>
			new QueuedHandlerThreadPool(consumer, name, new QueueStatsManager(), new(), false, null, timeout)) {
	}
}
