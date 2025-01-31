// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus;

[TestFixture]
public abstract class when_stopping_queued_handler : QueuedHandlerTestWithNoopConsumer {
	protected when_stopping_queued_handler(
		Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory)
		: base(queuedHandlerFactory) {
	}


	[Test]
	public void gracefully_should_not_throw() {
		Queue.Start();
		Assert.DoesNotThrowAsync(Queue.Stop);
	}

	[Test]
	public async Task gracefully_and_queue_is_not_busy_should_not_take_much_time() {
		var startTask = Queue.Start();
		var stopTask = Queue.Stop();
		await Task.WhenAll(startTask, stopTask).WaitAsync(TimeSpan.FromMilliseconds(5000))
			.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);

		Assert.IsTrue(stopTask.IsCompleted & stopTask.IsCompleted, "Could not stop queue in time.");
	}

	[Test]
	public void second_time_should_not_throw() {
		var startTask = Queue.Start();
		Assert.DoesNotThrowAsync(Queue.Stop);
		Assert.IsTrue(startTask.IsCompleted);
	}

	[Test]
	public async Task second_time_should_not_take_much_time() {
		var startTask = Queue.Start();
		var stopTask = Queue.Stop();
		await Task.WhenAll(startTask, stopTask).WaitAsync(TimeSpan.FromMilliseconds(5000))
			.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);

		Assert.IsTrue(stopTask.IsCompleted & stopTask.IsCompleted, "Could not stop queue in time.");
		Assert.IsTrue(Queue.Stop().IsCompletedSuccessfully);
	}

	[Test]
	public void while_queue_is_busy_should_crash_with_timeout() {
		var consumer = new WaitingConsumer(1);
		var busyQueue = new QueuedHandlerThreadPool(consumer, "busy_test_queue", new QueueStatsManager(), new(),
			watchSlowMsg: false,
			threadStopWaitTimeout: TimeSpan.FromMilliseconds(100));
		var waitHandle = new ManualResetEvent(false);
		var handledEvent = new ManualResetEvent(false);
		try {
			busyQueue.Start();
			busyQueue.Publish(new DeferredExecutionTestMessage(() => {
				handledEvent.Set();
				waitHandle.WaitOne();
			}));

			handledEvent.WaitOne();
			Assert.ThrowsAsync<TimeoutException>(busyQueue.Stop);
		} finally {
			waitHandle.Set();
			consumer.Wait();

			busyQueue.RequestStop();
			waitHandle.Dispose();
			handledEvent.Dispose();
			consumer.Dispose();
		}
	}
}

[TestFixture]
public class when_stopping_queued_handler_threadpool : when_stopping_queued_handler {
	public when_stopping_queued_handler_threadpool()
		: base((consumer, name, timeout) =>
			new QueuedHandlerThreadPool(consumer, name, new QueueStatsManager(), new(), false, null, timeout)) {
	}
}
