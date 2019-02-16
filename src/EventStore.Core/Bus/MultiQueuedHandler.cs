using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public class MultiQueuedHandler : IHandle<Message>, IPublisher, IThreadSafePublisher {
		public readonly IQueuedHandler[] Queues;

		private readonly Func<Message, int> _queueHash;
		private int _nextQueueNum = -1;

		public MultiQueuedHandler(int queueCount,
			Func<int, IQueuedHandler> queueFactory,
			Func<Message, int> queueHash = null) {
			Ensure.Positive(queueCount, "queueCount");
			Ensure.NotNull(queueFactory, "queueFactory");

			Queues = new IQueuedHandler[queueCount];
			for (int i = 0; i < Queues.Length; ++i) {
				Queues[i] = queueFactory(i);
			}

			//TODO AN remove _queueHash function
			_queueHash = queueHash ?? NextQueueHash;
		}

		public MultiQueuedHandler(params QueuedHandler[] queues) : this(queues, null) {
			Ensure.Positive(queues.Length, "queues.Length");
		}

		public MultiQueuedHandler(IQueuedHandler[] queues, Func<Message, int> queueHash) {
			Ensure.NotNull(queues, "queues");
			Ensure.Positive(queues.Length, "queues.Length");

			Queues = queues;
			_queueHash = queueHash ?? NextQueueHash;
		}

		private int NextQueueHash(Message msg) {
			return Interlocked.Increment(ref _nextQueueNum);
		}

		public IEnumerable<Task> Start() {
			var tasks = new List<Task>();
			for (int i = 0; i < Queues.Length; ++i) {
				tasks.Add(Queues[i].Start());
			}

			return tasks;
		}

		public void Stop() {
			var stopTasks = new Task[Queues.Length];
			for (int i = 0; i < Queues.Length; ++i) {
				int queueNum = i;
				stopTasks[i] = Task.Factory.StartNew(() => Queues[queueNum].Stop());
			}

			Task.WaitAll(stopTasks);
		}

		public void Handle(Message message) {
			Publish(message);
		}

		public void Publish(Message message) {
			var affineMsg = message as IQueueAffineMessage;
			int queueHash = affineMsg != null ? affineMsg.QueueId : _queueHash(message);
			var queueNum = (int)((uint)queueHash % Queues.Length);
			Queues[queueNum].Publish(message);
		}

		public void PublishToAll(Message message) {
			for (int i = 0; i < Queues.Length; ++i) {
				Queues[i].Publish(message);
			}
		}
	}
}
