using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public class MultiQueuedHandler : IPublisher {
	private readonly ReadOnlyMemory<IQueuedHandler> _queues;
	private int _nextQueueNum = -1;

	public MultiQueuedHandler(int queueCount,
		Func<int, IQueuedHandler> queueFactory) {
		Ensure.Positive(queueCount, "queueCount");
		Ensure.NotNull(queueFactory, "queueFactory");

		var queues = new IQueuedHandler[queueCount];
		for (int i = 0; i < queues.Length; ++i) {
			queues[i] = queueFactory(i);
		}

		_queues = queues;
	}

	private int NextQueueHash() => Interlocked.Increment(ref _nextQueueNum);

	public IEnumerable<Task> Start() {
		var queues = _queues.Span;
		var tasks = new Task[queues.Length];

		for (var i = 0; i < queues.Length; i++) {
			tasks[i] = queues[i].Start();
		}

		return tasks;
	}

	public void Stop() {
		var stopTasks = new Task[_queues.Length];
		var queues = _queues.Span;
		for (int i = 0; i < queues.Length; ++i) {
			stopTasks[i] = Task.Factory.StartNew(queues[i].Stop);
		}

		Task.WaitAll(stopTasks);
	}

	public void Publish(Message message) {
		int queueHash = (message as IQueueAffineMessage)?.QueueId ?? NextQueueHash();
		var queueNum = (int)((uint)queueHash % _queues.Length);
		_queues.Span[queueNum].Publish(message);
	}

	public void PublishToAll(Message message) {
		foreach (var queue in _queues.Span) {
			queue.Publish(message);
		}
	}
}
